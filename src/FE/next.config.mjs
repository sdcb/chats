import withSerwistInit from '@serwist/next';
import { createHash, randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';

const isDev = process.env?.NODE_ENV === 'development';
console.log('NODE_ENV', process.env?.NODE_ENV);
console.log('-------------------');

// Only precache the app shell icons; logos, login background, KaTeX fonts and
// Monaco (~15MB, copied by scripts/copy-monaco.mjs) are cached at runtime on demand.
// Listed explicitly because `additionalPrecacheEntries` replaces Serwist's public-dir glob.
const precachedPublicFiles = [
  'favicon.ico',
  'manifest.json',
  'icons/192x192.png',
  'icons/512x512.png',
  'icons/maskable-192x192.png',
  'icons/maskable-512x512.png',
];

const withSerwist = withSerwistInit({
  swSrc: 'service-worker/sw.ts',
  swDest: 'public/sw.js',
  disable: isDev,
  // A page left open with an unsaved draft must not be reloaded just because the network came back.
  reloadOnOnline: false,
  additionalPrecacheEntries: [
    ...precachedPublicFiles.map((file) => ({
      url: `/${file}`,
      revision: createHash('md5')
        .update(readFileSync(new URL(`./public/${file}`, import.meta.url)))
        .digest('hex'),
    })),
    { url: '/offline', revision: randomUUID() },
  ],
});

/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'export',
  reactStrictMode: false,
  images: {
    unoptimized: true,
  },
  env: {
    // 必须显式配置，否则没有 NEXT_PUBLIC_ 前缀的环境变量不会暴露给客户端
    // 开发时从 .env.local 读取，生产构建时为空字符串（前后端同源）
    API_URL: process.env.API_URL || '',
    // 开发时为 'local'，CI 构建时通过环境变量设置
    FE_VERSION: process.env.FE_VERSION || 'local',
  },
  turbopack: {},
  webpack(config) {
    config.experiments = {
      asyncWebAssembly: true,
      layers: true,
    };

    return config;
  },
};

export default withSerwist(nextConfig);
