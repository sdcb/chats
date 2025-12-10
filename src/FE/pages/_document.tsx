import { Head, Html, Main, NextScript } from 'next/document';

export default function ChatsDocument() {
  return (
    <Html>
      <Head>
        <meta name="mobile-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-title" content="Chats"></meta>
        <link
          rel="manifest"
          href="/manifest.json"
          crossOrigin="use-credentials"
        />
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
