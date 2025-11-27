import { useState } from 'react';

import { IconUser } from '@/components/Icons/index';
import UserMenuPopover from '@/components/UserMenuPopover/UserMenuPopover';

import SidebarButton from '../Sidebar/SidebarButton';

import { getUserBalanceOnly } from '@/apis/clientApis';
import { useUserInfo } from '@/providers/UserProvider';

const ChatBarSettings = () => {
  const user = useUserInfo();
  const [userBalance, setUserBalance] = useState<number>(0);

  const getUserBalance = () => {
    getUserBalanceOnly().then((data) => setUserBalance(data));
  };

  const handleClickUserMore = () => {
    getUserBalance();
  };

  return (
    <div className="flex flex-col items-center space-y-1 border-t border-black/5 dark:border-white/10 pt-2 text-sm">
      {user?.username && (
        <UserMenuPopover
          isAdminPage={false}
          trigger={
            <SidebarButton
              className="capitalize"
              text={user?.username}
              icon={<IconUser />}
              onClick={handleClickUserMore}
            />
          }
          onOpen={handleClickUserMore}
        />
      )}
    </div>
  );
};
export default ChatBarSettings;
