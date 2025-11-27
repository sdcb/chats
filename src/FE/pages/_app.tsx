import { useEffect, useState } from 'react';
import { Toaster } from 'react-hot-toast';
import { QueryClient, QueryClientProvider } from 'react-query';

import type { AppProps } from 'next/app';
import { useRouter } from 'next/router';

import { getUserInfo } from '@/utils/user';

import { UserRole } from '@/types/adminApis';

import ErrorPage from './_error';
import AdminLayout from '@/components/admin/layout/AdminLayout';
import BuildLayout from '@/components/build/layout/BuildLayout';
import './globals.css';

import { ThemeProvider } from '@/providers/ThemeProvider';
import 'katex/dist/katex.min.css';

function App({ Component, pageProps }: AppProps<{}> | any) {
  const route = useRouter();
  const queryClient = new QueryClient();

  const [isClient, setIsClient] = useState(false);

  useEffect(() => {
    setIsClient(true);
    document.title = 'Chats';
  }, []);

  const isAdmin = () => {
    const user = getUserInfo();
    return user?.role === UserRole.admin;
  };

  return (
    <ThemeProvider
      attribute="class"
      enableSystem
      disableTransitionOnChange
    >
      <Toaster />
      {isClient && (
        <QueryClientProvider client={queryClient}>
          {route.pathname.includes('/admin') ? (
            isAdmin() ? (
              <AdminLayout>
                <Component {...pageProps} />
              </AdminLayout>
            ) : (
              <ErrorPage statusCode={404} />
            )
          ) : route.pathname.includes('/build') ? (
            <BuildLayout>
              <Component {...pageProps} />
            </BuildLayout>
          ) : route.pathname.includes('/authorizing') ? (
            <Component {...pageProps} />
          ) : (
            <Component {...pageProps} />
          )}
        </QueryClientProvider>
      )}
    </ThemeProvider>
  );
}

export default App;
