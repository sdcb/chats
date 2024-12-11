import { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { Checkbox } from '@/components/ui/checkbox';

const EnableNetworkSearch = (props: {
  label: string;
  enable: boolean;
  onChange: (checked: boolean) => void;
}) => {
  const { t } = useTranslation();
  const { label, enable, onChange } = props;
  const [check, setCheck] = useState(enable);

  useEffect(() => {
    setCheck(enable);
  }, [enable]);

  return (
    <div className="flex flex-col">
      <label className="mb-2 text-left text-neutral-700 dark:text-neutral-400">
        {label}
      </label>
      <div className="flex items-center gap-1">
        <Checkbox
          defaultChecked={check}
          onCheckedChange={(state: boolean) => {
            onChange(state);
            setCheck(state);
          }}
          id="enable-search"
        />
        <label
          htmlFor="enable-search"
          className="text-neutral-900 dark:text-neutral-100"
        >
          {check ? t('Enable') : t('Close')}
        </label>
      </div>
    </div>
  );
};

export default EnableNetworkSearch;