import { ReactElement, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { cn } from '@/lib/utils';

type Props = {
  label: string;
  value: string | number;
  tool?: ReactElement;
  isExpand: boolean;
  hidden?: boolean;
  onChangeToDefault: () => void;
  onChangeToCustom: () => void;
};

const ModelParams = (props: Props) => {
  const {
    label,
    value,
    tool,
    isExpand,
    hidden,
    onChangeToDefault,
    onChangeToCustom,
  } = props;
  const { t } = useTranslation();

  const [model, setModel] = useState(isExpand);

  const handleChangeValue = () => {
    if (model) onChangeToDefault();
    else onChangeToCustom();
    setModel(!model);
  };

  return (
    <>
      {hidden ? (
        <></>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex justify-between text-sm">
            <div>{label}</div>
            <div className="text-gray-600" onClick={handleChangeValue}>
              {model ? t('Custom') : t('Default')}
            </div>
          </div>
          <div className={cn('hidden', model && 'flex justify-between gap-2')}>
            {tool}
            <div className='text-sm text-gray-600'>{value}</div>
          </div>
        </div>
      )}
    </>
  );
};
export default ModelParams;
