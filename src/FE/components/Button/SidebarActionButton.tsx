import { MouseEventHandler, ReactElement } from 'react';

interface Props {
  handleClick: MouseEventHandler<HTMLButtonElement>;
  children: ReactElement;
  disabled?: boolean;
}

const SidebarActionButton = ({ handleClick, children, disabled }: Props) => (
  <button
    className="min-w-[20px] p-1 text-black dark:text-white hover:opacity-50 disabled:opacity-30 disabled:cursor-not-allowed"
    onClick={handleClick}
    disabled={disabled}
  >
    {children}
  </button>
);

export default SidebarActionButton;
