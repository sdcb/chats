import { useContext } from 'react';

import { Sheet, SheetContent } from '@/components/ui/sheet';

import { setShowSetting } from '../../_actions/setting.actions';
import HomeContext from '../../_contexts/home.context';

type Props = {
  isOpen: boolean;
};
const SettingsSheet = (props: Props) => {
  const { isOpen } = props;

  const { settingDispatch } = useContext(HomeContext);

  return (
    <Sheet
      open={isOpen}
      onOpenChange={() => {
        settingDispatch(setShowSetting(false));
      }}
    >
      <SheetContent className="max-w-full sm:max-w-full w-full">
        <div></div>
      </SheetContent>
    </Sheet>
  );
};

export default SettingsSheet;
