import UsageTable from '@/components/usage/UsageTable';
import { UsageSource } from '@/types/chat';

interface UsageRecordsTabProps {
  fixedSource?: UsageSource;
  basePath?: string;
}

const UsageRecordsTab = ({ fixedSource, basePath }: UsageRecordsTabProps = {}) => {
  return <UsageTable mode="user" fixedSource={fixedSource} basePath={basePath} />;
};

export default UsageRecordsTab;
