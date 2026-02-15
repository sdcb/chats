import useTranslation from '@/hooks/useTranslation';

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
    <div className="my-2 mt-0 px-1 pt-1">
      <span className="text-sm whitespace-pre-wrap break-words m-0 p-0 bg-transparent text-red-600 dark:text-red-400">
        {errorMessage()}
      </span>
    </div>
  );
};

export default ChatError;
