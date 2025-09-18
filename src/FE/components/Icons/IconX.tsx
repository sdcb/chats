import { IconProps } from './types';

const IconX = (props: IconProps) => {
  const {
    className,
    size = 20,
    strokeWidth = 1.6,
    stroke,
    style,
    onClick,
  } = props;

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      onClick={onClick}
      style={style}
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
      <path d="M18 6l-12 12" />
      <path d="M6 6l12 12" />
    </svg>
  );

};

export default IconX;
