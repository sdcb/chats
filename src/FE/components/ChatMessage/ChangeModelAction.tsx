import useTranslation from '@/hooks/useTranslation';

import { AdminModelDto } from '@/types/adminApis';

import ChatModelDropdownMenu from '@/components/ChatModelDropdownMenu/ChatModelDropdownMenu';
import Tips from '@/components/Tips/Tips';

interface Props {
  models: AdminModelDto[];
  readonly?: boolean;
  hidden?: boolean;
  showRegenerate?: boolean;
  onChangeModel: (model: AdminModelDto) => void;
  modelId: number;
  modelName: string;
}

export const ChangeModelAction = (props: Props) => {
  const { t } = useTranslation();
  const {
    models,
    modelId,
    modelName,
    readonly,
    hidden,
    showRegenerate,
    onChangeModel,
  } = props;

  return (
    <>
      {!hidden && (
        <Tips
          trigger={
            <ChatModelDropdownMenu
              models={models}
              readonly={readonly}
              onChangeModel={(model) => {
                onChangeModel && onChangeModel(model);
              }}
              modelId={modelId}
              modelName={modelName}
              showRegenerate={showRegenerate}
              content={modelName}
            />
          }
          content={t('Change Model')!}
        />
      )}
    </>
  );
};

export default ChangeModelAction;
