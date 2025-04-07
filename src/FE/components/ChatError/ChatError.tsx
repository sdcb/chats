import useTranslation from '@/hooks/useTranslation';

import { Alert, AlertDescription } from '@/components/ui/alert';

interface Props {
  error?: string;
}

const ChatError = (props: Props) => {
  const { error } = props;

  const { t } = useTranslation();

  function errorMessage() {
    let message = error
      ? t(error)
      : t(
          'There were some errors during the chat. You can switch models or try again later.',
        );
    return message;
  }

  return (
    <Alert
      variant="destructive"
      className="bg-card my-2 mt-0 border-none p-0 px-1 pt-1"
    >
      <AlertDescription className="flex items-center gap-1">
        {errorMessage()}
      </AlertDescription>
    </Alert>
  );
};

export default ChatError;
