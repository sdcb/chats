import UsageRecordsTab from '@/components/settings/tabs/UsageRecordsTab';

import { UsageSource } from '@/types/chat';

export default function BuildUsagePage() {
  return <UsageRecordsTab fixedSource={UsageSource.Api} basePath="/build/usage" />;
}
