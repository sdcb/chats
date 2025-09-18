import Head from 'next/head';

import HomeContent from '@/components/Home/HomeContent';

import { UserProvider } from '@/providers/UserProvider';

const Home = () => {
  return (
    <UserProvider>
      <Head>
        <title>Chats</title>
        <meta name="description" content="" />
        <meta
          name="viewport"
          content="height=device-height ,width=device-width, initial-scale=1, user-scalable=no"
        />
        <link rel="icon" href="/favicon.ico" />
      </Head>
      <main>
        <HomeContent />
      </main>
    </UserProvider>
  );
};

export default Home;
