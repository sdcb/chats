import Link from 'next/link';

import { Button } from '../ui/button';

const PageNotFound = () => {
  return (
    <main className="grid min-h-full place-items-center bg-white px-6 py-24 sm:py-32 lg:px-8">
      <div className="text-center">
        <p className="text-3xl font-semibold text-primary">404</p>
        <h1 className="mt-4 text-lg font-bold tracking-tight text-gray-900">
          抱歉，我们找不到您要找的页面。
        </h1>
        <div className="mt-10 flex items-center justify-center gap-x-6">
          <Link href="/">
            <Button className="h-8 w-32">返回首页</Button>
          </Link>
        </div>
      </div>
    </main>
  );
};

export default PageNotFound;