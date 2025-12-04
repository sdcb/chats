import { FC, ReactNode } from 'react';

interface Props {
  text: string;
  href: string;
  icon?: ReactNode;
  className?: string;
  action?: ReactNode;
  onClick?: (e: React.MouseEvent<HTMLAnchorElement>) => void;
}

const SidebarLink: FC<Props> = ({
  text,
  href,
  icon,
  className,
  action,
  onClick,
}) => {
  return (
    <a
      href={href}
      className="flex w-full justify-between cursor-pointer select-none items-center gap-2 hover:bg-muted rounded-md py-3 px-3 pl-[10px] text-[14px] leading-2 text-white transition-colors duration-200 no-underline"
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
    </a>
  );
};
export default SidebarLink;
