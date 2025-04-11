import { useRouter } from 'next/router';

import useTranslation from '@/hooks/useTranslation';

import { UserRole } from '@/types/adminApis';

import { IconModelSearch } from '@/components/Icons';
import { Button } from '@/components/ui/button';

import { useUserInfo } from '@/providers/UserProvider';

const NoModel = () => {
  const { t } = useTranslation();
  const user = useUserInfo();
  const router = useRouter();

  return (
    <div className="w-full flex items-center flex-wrap justify-center gap-10">
      <div className="grid gap-2 w-64">
        <div className="w-20 h-20 mx-auto">
          <IconModelSearch size={64} />
        </div>
        <div>
          <h2 className="text-center text-lg font-semibold leading-relaxed">
            {t("You don't have any models yet")}
          </h2>
          <Button
            variant="link"
            onClick={() => {
              user?.role === UserRole.admin && router.push('/admin');
            }}
            className="text-center text-sm font-normal leading-snug"
          >
            {t('You can contact the administrator or create your first model')}
          </Button>
        </div>
      </div>
    </div>
  );
};

export default NoModel;
