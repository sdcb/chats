import { IconProps } from './types';

const IconColumns = (props: IconProps) => {
  const { className, size = 20, strokeWidth = 1.6, stroke, onClick } = props;

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      onClick={onClick}
      className={className}
      width={size}
      height={size}
      strokeWidth={strokeWidth}
      stroke={stroke || 'var(--foreground)'}
      viewBox="0 0 24 24"
      fill="none"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path stroke="none" d="M0 0h24v24H0z" fill="none" />
      <rect x="3" y="5" width="4.5" height="14" rx="1.5" />
      <rect x="9.75" y="5" width="4.5" height="14" rx="1.5" />
      <rect x="16.5" y="5" width="4.5" height="14" rx="1.5" />
    </svg>
  );
};

export default IconColumns;