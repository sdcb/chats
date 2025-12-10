const isDev = process.env?.NODE_ENV === 'development';
console.log('NODE_ENV', process.env?.NODE_ENV);
console.log('-------------------');

const withPWA = require('next-pwa')({
  dest: 'public',
  register: !isDev,
  skipWaiting: !isDev,
  disable: isDev,
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

module.exports = withPWA(nextConfig);
