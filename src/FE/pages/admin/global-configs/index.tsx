import React, { useEffect, useState } from 'react';
import toast from 'react-hot-toast';

import useTranslation from '@/hooks/useTranslation';
import { GetConfigsResult } from '@/types/adminApis';
import { getConfigs } from '@/apis/adminApis';

import SiteInfoConfig from '@/components/admin/global-configs/SiteInfoConfig';
import TencentSmsConfig from '@/components/admin/global-configs/TencentSmsConfig';

export default function Configs() {
  const { t } = useTranslation();
  const [configs, setConfigs] = useState<GetConfigsResult[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    init();
  }, []);

  const init = () => {
    getConfigs().then((data) => {
      setConfigs(data);
      setLoading(false);
    }).catch(() => {
      toast.error('Failed to fetch configs');
      setLoading(false);
    });
  };

  if (loading) {
    return <div>{t('Loading...')}</div>;
  }

  return (
    <div className="space-y-6">
      <SiteInfoConfig configs={configs} onConfigsUpdate={init} />
      <TencentSmsConfig configs={configs} onConfigsUpdate={init} />
    </div>
  );
}
