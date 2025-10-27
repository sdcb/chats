import { useEffect, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { Checkbox } from '@/components/ui/checkbox';

const FeatureToggle = (props: {
  label: string;
  enable: boolean;
  onChange: (checked: boolean) => void;
  icon: React.ReactNode;
}) => {
  const { t } = useTranslation();
  const { label, enable, onChange, icon } = props;
  const [check, setCheck] = useState(enable);

  useEffect(() => {
    setCheck(enable);
  }, [enable]);

  return (
    <div className="flex flex-col">
      <div className="flex items-center justify-between gap-1">
        <span className='flex gap-1 items-center text-neutral-700 dark:text-neutral-400'>
          {icon}
          {label}
        </span>
        <div className="flex gap-1 items-center">
          <Checkbox
            defaultChecked={check}
            onCheckedChange={(state: boolean) => {
              onChange(state);
              setCheck(state);
            }}
            id={`feature-${label}`}
          />
          <label
            htmlFor={`feature-${label}`}
            className="text-neutral-900 dark:text-neutral-100"
          >
            {check ? t('Enable') : t('Close')}
          </label>
        </div>
      </div>
    </div>
  );
};

export default FeatureToggle;
