import { Head, Html, Main, NextScript } from 'next/document';

export default function ChatsDocument() {
  return (
    <Html lang="zh-CN">
      <Head>
        <meta name="application-name" content="Chats" />
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-title" content="Chats" />
        <meta name="apple-mobile-web-app-status-bar-style" content="default" />
        <meta
          name="theme-color"
          media="(prefers-color-scheme: light)"
          content="#f8f9fa"
        />
        <meta
          name="theme-color"
          media="(prefers-color-scheme: dark)"
          content="#202021"
        />
        <link rel="manifest" href="/manifest.json" />
        <link rel="apple-touch-icon" href="/icons/192x192.png" />
        <link rel="shortcut icon" href="/icons/192x192.png" />
      </Head>
      <body>
        <Main />
        <NextScript />
      </body>
    </Html>
  );
}
