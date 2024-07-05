import { BadRequest } from '@/utils/error';
import { weChatAuth } from '@/utils/weChat';

import { ChatsApiRequest } from '@/types/next-api';
import { PayServiceType } from '@/types/pay';

import { OrdersManager, PayServiceManager, WxPayManager } from '@/managers';
import { apiHandler } from '@/middleware/api-handler';
import requestIp from 'request-ip';

export const config = {
  api: {
    bodyParser: {
      sizeLimit: '1mb',
    },
  },
  maxDuration: 5,
};

const handler = async (req: ChatsApiRequest) => {
  if (req.method === 'POST') {
    const { orderId, code } = req.body as {
      orderId: string;
      code: string;
    };
    const order = await OrdersManager.findById(orderId);
    if (!order) {
      throw new BadRequest('订单不存在');
    }

    const payConfig = await PayServiceManager.findConfigsByType(
      PayServiceType.WeChatPay,
    );
    const { appId, secret } = JSON.parse(payConfig);

    const weChatAuthResult = await weChatAuth(appId, secret, code);
    if (!weChatAuthResult) {
      throw new BadRequest('微信授权失败');
    }

    const ipAddress = requestIp.getClientIp(req) || '127.0.0.1';
    const result = await WxPayManager.callWxJSApiPay({
      openId: weChatAuthResult.openid,
      ipAddress,
      orderId: order.id,
      amount: order.amount,
      outTradeNo: order.outTradeNo,
    });
    return result;
  }
};

export default apiHandler(handler);