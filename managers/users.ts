import { weChatAuth } from '@/utils/weChat';

import { LoginType, UserInitialModel } from '@/types/user';

import { UserBalancesManager, UserModelManager } from '.';
import { LoginServiceManager } from './loginService';

import prisma from '@/prisma/prisma';
import { Prisma } from '@prisma/client';
import bcrypt from 'bcryptjs';
import Decimal from 'decimal.js';

export interface CreateUser {
  account?: string;
  username?: string;
  password?: string;
  role: string;
  email?: string;
  phone?: string;
  avatar?: string;
  provider?: string;
  sub?: string;
}

export interface UpdateUser {
  id: string;
  username: string;
  password: string;
  role: string;
  email?: string;
  phone?: string;
  enabled?: boolean;
}

export interface IWeChatAuthResult {
  access_token: string;
  expires_in: number;
  refresh_token: string;
  openid: string;
  scope: string;
  errcode: string;
  errmsg: string;
}

export interface CreateUserInitialConfig {
  name: string;
  price: Decimal;
  models: string;
  loginType: string;
  invitationCodeId: string;
}
export interface UpdateUserInitialConfig extends CreateUserInitialConfig {
  id: string;
}

export class UsersManager {
  static async findByUserId(id: string) {
    return await prisma.users.findUnique({ where: { id } });
  }

  static async findByUserByProvider(provider: string, sub: string) {
    return await prisma.users.findFirst({ where: { provider, sub } });
  }

  static async findByAccount(account: string) {
    return await prisma.users.findFirst({
      where: {
        account: account?.toLowerCase(),
      },
    });
  }

  static async findByPhone(phone: string) {
    return await prisma.users.findFirst({
      where: {
        phone,
      },
    });
  }

  static async findByUnique(value: string) {
    const _value = value.toLocaleLowerCase();
    return await prisma.users.findFirst({
      where: {
        OR: [
          {
            account: _value,
          },
          {
            phone: _value,
          },
          {
            email: _value,
          },
        ],
        AND: [
          {
            enabled: true,
          },
        ],
      },
    });
  }

  static async singIn(account: string, password: string) {
    const user = await this.findByUnique(account);
    if (user && user.password) {
      const match = await bcrypt.compareSync(password, user.password);
      if (match) {
        return user;
      }
    }
    return null;
  }

  static async createUser(params: CreateUser) {
    const { password } = params;
    let hashPassword = null;
    if (password) hashPassword = await bcrypt.hashSync(password);
    return prisma.users.create({
      data: {
        ...params,
        password: hashPassword,
      },
    });
  }

  static async findUsers(query: string, page: number, pageSize: number) {
    const where: Prisma.UsersWhereInput = query
      ? {
          OR: [
            {
              username: { equals: query },
            },
            {
              account: { equals: query },
            },
            {
              phone: { equals: query },
            },
            {
              email: { equals: query },
            },
          ],
        }
      : {};
    const users = await prisma.users.findMany({
      include: {
        userBalances: { select: { balance: true } },
        UserModels: { select: { models: true, id: true } },
      },
      where,
      skip: (page - 1) * pageSize,
      take: pageSize,
      orderBy: {
        createdAt: 'desc',
      },
    });
    const count = await prisma.users.count({
      where,
    });
    return { rows: users, count };
  }

  static async updateUserPassword(id: string, password: string) {
    return await prisma.users.update({ where: { id }, data: { password } });
  }

  static async updateUser(params: UpdateUser) {
    return await prisma.users.update({
      where: { id: params.id },
      data: { ...params },
    });
  }

  static async initialUser(
    userId: string,
    LoginType: LoginType | string,
    invitationCodeId?: string | null,
    createUserId?: string,
  ) {
    const configs = await prisma.userInitialConfig.findMany({
      where: {
        OR: [
          {
            loginType: { in: ['-', ...(LoginType ? [LoginType] : [])] },
          },
          {
            invitationCodeId: {
              equals: invitationCodeId,
            },
          },
        ],
      },
      orderBy: { createdAt: 'desc' },
    });

    const configScores = configs
      .map((config, index) => {
        let score = 0;
        if (config.invitationCodeId === invitationCodeId) {
          score += 10;
        } else if (config.invitationCodeId === null) {
          score += 1;
        }
        if (config.loginType === LoginType) {
          score += 10;
        } else if (config.loginType === '-') {
          score += 1;
        }

        return { index, score };
      })
      .sort((a, b) => b.score - a.score);

    let config = null;

    if (configScores.length > 0) {
      config = configs[configScores[0].index];
    }

    let models = [] as UserInitialModel[];
    let initialPrice = new Decimal(0);
    if (config) {
      initialPrice = config.price;
      models = JSON.parse(config.models) || [];
    }
    const userModels = models.map((x) => ({
      ...x,
      enabled: true,
    }));

    await UserModelManager.createUserModel({
      userId: userId,
      models: JSON.stringify(userModels),
    });
    await UserBalancesManager.createBalance(
      userId,
      initialPrice,
      createUserId || userId,
    );
  }

  static async weChatLogin(code: string) {
    const configs = await LoginServiceManager.findConfigsByType(
      LoginType.WeChat,
    );
    const result = await weChatAuth(configs.appId, configs.secret, code);
    if (!result) {
      return null;
    }
    let user = await this.findByUserByProvider(LoginType.WeChat, result.openid);
    if (!user) {
      user = await this.createUser({
        account: result.openid,
        username: '微信用户',
        password: '-',
        role: '-',
        provider: LoginType.WeChat,
        sub: result.openid,
      });
      await this.initialUser(user.id, LoginType.WeChat);
    }
    return user;
  }

  static async createUserInitialConfig(params: CreateUserInitialConfig) {
    await prisma.userInitialConfig.create({
      data: {
        ...params,
      },
    });
  }

  static async updateUserInitialConfig(params: UpdateUserInitialConfig) {
    await prisma.userInitialConfig.update({
      where: { id: params.id },
      data: {
        ...params,
      },
    });
  }

  static async getUserInitialConfig() {
    const configs = await prisma.userInitialConfig.findMany({
      include: {
        InvitationCode: { select: { value: true, id: true } },
      },
    });
    return configs.map((x) => ({
      id: x.id,
      name: x.name,
      loginType: x.loginType,
      models: JSON.parse(x.models),
      price: x.price,
      invitationCodeId: x.InvitationCode?.id || '-',
      invitationCode: x.InvitationCode?.value || '-',
    }));
  }

  static async deleteUserInitialConfig(id: string) {
    await prisma.userInitialConfig.delete({ where: { id } });
  }
}
