import React from 'react';

import Link from 'next/link';

import useTranslation from '@/hooks/useTranslation';

import { getApiUrl } from '@/utils/common';

import CopyButton from '@/components/Button/CopyButton';
import { Card } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

interface ApiDoc {
  name: string;
  endpoint: string;
  method: string;
  docUrl: string;
}

function ApiTable({ apis, baseUrl }: { apis: ApiDoc[]; baseUrl: string }) {
  const { t } = useTranslation();

  return (
    <Card className="overflow-x-auto w-full rounded-md border-none">
      <Table>
        <TableHeader>
          <TableRow className="pointer-events-none">
            <TableHead>{t('API Name')}</TableHead>
            <TableHead>{t('Endpoint')}</TableHead>
            <TableHead>{t('Documentation')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {apis.map((api, index) => {
            const fullUrl = baseUrl + api.endpoint;
            return (
              <TableRow key={index}>
                <TableCell className="py-3 font-medium">{api.name}</TableCell>
                <TableCell className="py-3">
                  <div className="flex items-center gap-2">
                    <span
                      className={`px-2 py-1 rounded text-xs font-mono ${
                        api.method === 'GET'
                          ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
                          : 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200'
                      }`}
                    >
                      {api.method}
                    </span>
                    <span className="text-sm font-mono">{fullUrl}</span>
                    <CopyButton value={fullUrl} />
                  </div>
                </TableCell>
                <TableCell className="py-3">
                  <Link
                    href={api.docUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-blue-600 dark:text-blue-500 hover:underline"
                  >
                    {t('View Documentation')}
                  </Link>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Card>
  );
}

export default function BuildDocsPage() {
  const { t } = useTranslation();
  const baseUrl = getApiUrl() || location.origin;

  const openaiApis: ApiDoc[] = [
    {
      name: 'Chat Completions',
      endpoint: '/v1/chat/completions',
      method: 'POST',
      docUrl: 'https://platform.openai.com/docs/api-reference/chat/create',
    },
    {
      name: 'List Models',
      endpoint: '/v1/models',
      method: 'GET',
      docUrl: 'https://platform.openai.com/docs/api-reference/models/list',
    },
    {
      name: 'Image Generations',
      endpoint: '/v1/images/generations',
      method: 'POST',
      docUrl: 'https://platform.openai.com/docs/api-reference/images/create',
    },
    {
      name: 'Image Edit',
      endpoint: '/v1/images/edits',
      method: 'POST',
      docUrl: 'https://platform.openai.com/docs/api-reference/images/createEdit',
    },
  ];

  const anthropicApis: ApiDoc[] = [
    {
      name: 'Messages',
      endpoint: '/v1/messages',
      method: 'POST',
      docUrl: 'https://docs.anthropic.com/en/api/messages',
    },
    {
      name: 'Count Tokens',
      endpoint: '/v1/messages/count_tokens',
      method: 'POST',
      docUrl: 'https://docs.anthropic.com/en/api/messages-count-tokens',
    },
  ];

  return (
    <div className="w-full space-y-6">
      <div>
        <p className="text-sm text-muted-foreground">
          {t(
            'API documentation for supported endpoints. Click on the documentation link for detailed usage instructions.',
          )}
        </p>
      </div>

      <div>
        <h2 className="text-lg font-semibold mb-3">
          {t('OpenAI Compatible APIs')}
        </h2>
        <ApiTable apis={openaiApis} baseUrl={baseUrl} />
      </div>

      <div>
        <h2 className="text-lg font-semibold mb-3">
          {t('Anthropic Compatible APIs')}
        </h2>
        <ApiTable apis={anthropicApis} baseUrl={baseUrl} />
      </div>
    </div>
  );
}
