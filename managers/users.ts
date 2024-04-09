import prisma from '@/db/prisma';
import bcrypt from 'bcryptjs';
import { UserBalancesManager, UserModelManager } from '.';
import Decimal from 'decimal.js';
import { ProviderType } from '@/types/user';
import { weChatAuth } from '@/utils/weChat';

export interface CreateUser {
  username: string;
  password: string;
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

export class UsersManager {
  static async findByUserId(id: string) {
    return await prisma.users.findUnique({ where: { id } });
  }

  static async findByUserByProvider(provider: string, sub: string) {
    return await prisma.users.findFirst({ where: { provider, sub } });
  }

  static async findByUsername(username: string) {
    return await prisma.users.findFirst({
      where: { username: username.toLowerCase() },
    });
  }

  static async findByUnique(value: string) {
    const _value = value.toLocaleLowerCase();
    return await prisma.users.findFirst({
      where: {
        OR: [
          {
            username: _value,
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

  static async singIn(username: string, password: string) {
    const user = await this.findByUnique(username);
    if (user) {
      const match = await bcrypt.compareSync(password, user.password);
      if (match) {
        return user;
      }
    }
    return null;
  }

  static async createUser(params: CreateUser) {
    const { password } = params;
    let hashPassword = await bcrypt.hashSync(password);
    return prisma.users.create({
      data: {
        ...params,
        password: hashPassword,
      },
    });
  }

  static async findUsers(query: string) {
    return await prisma.users.findMany({
      include: { userBalances: { select: { balance: true } } },
      where: {
        OR: [
          {
            username: { contains: query },
          },
          {
            phone: { contains: query },
          },
          {
            email: { contains: query },
          },
        ],
      },
      orderBy: {
        createdAt: 'desc',
      },
    });
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

  static async initialUser(userId: string, createUserId?: string) {
    await UserModelManager.createUserModel({
      userId: userId,
      models: '[]',
    });
    await UserBalancesManager.createBalance(
      userId,
      new Decimal(0),
      createUserId || userId
    );
  }

  static async weChatLogin(code: string) {
    const result = await weChatAuth(code);
    if (!result) {
      return null;
    }
    let user = await this.findByUserByProvider(
      ProviderType.WeChat,
      result.openid
    );
    if (!user) {
      user = await this.createUser({
        username: result.openid,
        password: '-',
        role: '-',
        provider: ProviderType.WeChat,
        sub: result.openid,
      });
      await this.initialUser(user.id);
    }
    return user;
  }
}
