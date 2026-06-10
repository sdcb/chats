import { forwardRef, ReactNode } from 'react';

interface Props extends Omit<React.ComponentProps<'button'>, 'className'> {
  text: string;
  icon?: ReactNode;
  className?: string;
  action?: ReactNode;
  onClick: () => void;
}

const SidebarButton = forwardRef<HTMLButtonElement, Props>(
  ({ text, icon, className, action, onClick, ...props }, ref) => (
    <button
      ref={ref}
      type="button"
      className="flex w-full justify-between cursor-pointer select-none items-center gap-2 hover:bg-muted rounded-md py-3 px-3 pl-[10px] text-[14px] leading-2 text-white transition-colors duration-200"
      onClick={onClick}
      {...props}
    >
      <span className="flex text-black dark:text-white w-[80%] items-center">
        <span>{icon}</span>
        <span
          className={`${
            icon && 'pl-3'
          } whitespace-nowrap text-ellipsis text-black dark:text-white ${className} ${
            text?.length >= 8 && 'overflow-hidden'
          }`}
        >
          {text}
        </span>
      </span>
      <span className="text-black dark:text-white">{action}</span>
    </button>
  ),
);

SidebarButton.displayName = 'SidebarButton';

export default SidebarButton;
