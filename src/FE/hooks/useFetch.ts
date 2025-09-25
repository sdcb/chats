import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';

import { getApiUrl } from '@/utils/common';
import {
  getUserSession,
  redirectToHomePage,
  redirectToLoginPage,
} from '@/utils/user';

export type RequestModel = {
  params?: object;
  headers?: object;
  signal?: AbortSignal;
};

export type RequestWithBodyModel = RequestModel & {
  body?: object | FormData;
};

const readResponse = async (response: Response) => {
  if (!response.headers) {
    return response;
  }
  const contentType = response.headers.get('content-type');
  const contentDisposition = response.headers.get('content-disposition');

  if (contentType === null) {
    return null;
  } else if (contentType.indexOf('application/json') !== -1) {
    return await response.json();
  } else if (contentType.indexOf('text/plain') !== -1) {
    return await response.text();
  } else if (
    contentDisposition != null &&
    contentDisposition.indexOf('attachment') !== -1
  ) {
    return await response.blob();
  } else {
    return null;
  }
};

const handleErrorResponse = async (err: Response) => {
  const { t } = useTranslation();
  const error = await readResponse(err);
  let message = error?.message || error?.errMessage || error;

  switch (err.status) {
    case 500:
      message = 'Internal server error, Please try again later';
      break;
    case 401:
      {
        redirectToLoginPage();
        break;
      }
    case 403:
      message = 'Resource denial of authorized access';
      redirectToHomePage(1000);
      break;
    case 404:
      message = 'Resource not found';
      break;
    default:
      message =
        typeof message === 'string' && message !== ''
          ? message
          : 'Operation failed, Please try again later, or contact technical personnel';
  }
  const tMsg = t(message);
  tMsg && toast.error(tMsg);
  throw error;
};

export const useFetch = () => {
  const handleFetch = async (
    url: string,
    request: any,
    signal?: AbortSignal,
  ) => {
    const apiPrefix = getApiUrl();
    const requestUrl = `${apiPrefix}${url}${request?.params ? request.params : ''
      }`;

    const body = request?.body
      ? request.body instanceof FormData
        ? { ...request, body: request.body }
        : { ...request, body: JSON.stringify(request.body) }
      : request;

    const headers = {
      ...request?.headers,
      ...(!request?.body || !(request.body instanceof FormData)
        ? { 'Content-type': 'application/json' }
        : {}),
      Authorization: `Bearer ${getUserSession()}`,
    };

    return fetch(requestUrl, {
      ...body,
      headers,
      signal,
    })
      .then(async (response) => {
        if (!response.ok) {
          throw response;
        }

        const result = readResponse(response);
        return result;
      })
      .catch(async (err: Response) => {
        await handleErrorResponse(err);
      });
  };

  return {
    get: async <T>(url: string, request?: RequestModel): Promise<T> => {
      const searchParams = new URLSearchParams();
      let paramsStr = '';
      if (request?.params) {
        Object.entries(request.params).forEach(([key, value]) => {
          if (value !== undefined && value !== null) {
            searchParams.append(key, String(value));
          }
        });
        paramsStr = `?${searchParams.toString()}`;
        delete request?.params;
      }

      return handleFetch(`${url}${paramsStr}`, {
        ...request,
        method: 'get',
      });
    },
    post: async <T>(
      url: string,
      request?: RequestWithBodyModel,
    ): Promise<T> => {
      return handleFetch(url, { ...request, method: 'post' });
    },
    put: async <T>(url: string, request?: RequestWithBodyModel): Promise<T> => {
      return handleFetch(url, { ...request, method: 'put' });
    },
    patch: async <T>(
      url: string,
      request?: RequestWithBodyModel,
    ): Promise<T> => {
      return handleFetch(url, { ...request, method: 'patch' });
    },
    delete: async <T>(url: string, request?: RequestWithBodyModel): Promise<T> => {
      return handleFetch(url, { ...request, method: 'delete' });
    },
  };
};
