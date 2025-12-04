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
