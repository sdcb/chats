import React from 'react';

import SiteInfoConfig from '@/components/admin/global-configs/SiteInfoConfig';
import TencentSmsConfig from '@/components/admin/global-configs/TencentSmsConfig';
import TitleSummaryConfig from '@/components/admin/global-configs/TitleSummaryConfig';

export default function Configs() {
  return (
    <div className="space-y-6">
      <SiteInfoConfig />
      <TencentSmsConfig />
      <TitleSummaryConfig />
    </div>
  );
}
