import { FC } from 'react';

interface Props {
  text: string;
  icon?: JSX.Element;
  className?: string;
  action?: JSX.Element;
  onClick: () => void;
}

const SidebarButton: FC<Props> = ({
  text,
  icon,
  className,
  action,
  onClick,
}) => {
  return (
    <div
      className="flex w-full justify-between cursor-pointer select-none items-center gap-2 hover:bg-muted rounded-md py-3 px-3 pl-[10px] text-[14px] leading-2 text-white transition-colors duration-200"
      onClick={onClick}
    >
      <div className="flex text-black dark:text-white w-[80%] items-center">
        <div>{icon}</div>
        <span
          className={`${
            icon && 'pl-3'
          } whitespace-nowrap text-ellipsis text-black dark:text-white ${className} ${
            text?.length >= 8 && 'overflow-hidden'
          }`}
        >
          {text}
        </span>
      </div>
      <div className="text-black dark:text-white">{action}</div>
    </div>
  );
};
export default SidebarButton;
