import { IconProps } from './types';

const IconSendPlane = (props: IconProps) => {
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
      <path d="M10 14l11 -11" />
      <path d="M21 3l-6.5 18a.5 .5 0 0 1 -.9 .1l-3.6 -7.2l-7.2 -3.6a.5 .5 0 0 1 .1 -.9l18 -6.5" />
    </svg>
  );
};

export default IconSendPlane;
