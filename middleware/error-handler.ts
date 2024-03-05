import { NextApiRequest, NextApiResponse } from 'next';

export function apiErrorHandler(handler: any) {
  return async (req: NextApiRequest, res: NextApiResponse) => {
    try {
      await handler(req, res);
    } catch (error) {
      console.error(error);
      // res.status(500).json({ statusCode: 500, message: error.message });
    }
  };
}
