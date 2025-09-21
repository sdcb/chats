import { IconProps } from './types';

const IconCamera = (props: IconProps) => {
  const { className, size = 20, strokeWidth = 1.6, stroke, onClick, style } = props;

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
      style={style}
    >
      <path stroke="none" d="M0 0h24v24H0z" fill="none" />
      <path d="M10 6h4l2 2h3a2 2 0 0 1 2 2v7a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-7a2 2 0 0 1 2 -2h3l2 -2z" />
      <circle cx="12" cy="13" r="4" />
      <path d="M5 7h2" />
    </svg>
  );
};

export default IconCamera;
