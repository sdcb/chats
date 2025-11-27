import UsageRecordsTab from '@/components/settings/tabs/UsageRecordsTab';

import { UsageSource } from '@/types/chat';

export default function BuildUsagePage() {
  return <UsageRecordsTab fixedSource={UsageSource.API} basePath="/build/usage" />;
}
