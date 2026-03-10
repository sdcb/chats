import { IconProps } from './types';

const IconCompare = (props: IconProps) => {
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
      <rect x="2" y="6" width="5" height="12" rx="1.5" />
      <rect x="17" y="6" width="5" height="12" rx="1.5" />
      <path d="M9 9 L15 9 M13 7 L15 9 L13 11" />
      <path d="M15 15 L9 15 M11 13 L9 15 L11 17" />
    </svg>
  );
};

export default IconCompare;