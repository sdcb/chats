import React, { useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { getUserInfo } from '@/utils/user';

const Dashboard = () => {
  const { t } = useTranslation();
  const [name] = useState(() => getUserInfo()?.username || '');

  return (
    <div className="font-semibold text-lg">
      {t('Welcome back.')}
      {name}
    </div>
  );
};

export default Dashboard;
