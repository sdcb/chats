import { useMemo } from 'react';
import { useRouter } from 'next/router';
import ByUserTab from '@/components/admin/user-models/ByUserTab';

const UserModelsPage = () => {
  const router = useRouter();

  const { username, query } = router.query;

  const focusUsername = useMemo(() => {
    if (typeof username === 'string') return username;
    if (Array.isArray(username)) return username[0];
    return undefined;
  }, [username]);

  const queryParam = useMemo(() => {
    if (typeof query === 'string') return query;
    if (Array.isArray(query)) return query[0];
    return undefined;
  }, [query]);

  return (
    <div className="space-y-4">
      <ByUserTab focusUsername={focusUsername} queryParam={queryParam} />
    </div>
  );
};

export default UserModelsPage;
