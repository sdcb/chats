import { IconProps } from './types';

const IconTools = (props: IconProps) => {
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
      <path d="M3 21h4l13 -13a1.5 1.5 0 0 0 -4 -4l-13 13v4" />
      <path d="M14.5 5.5l4 4" />
      <path d="M12 8l-5 -5l-4 4l5 5" />
      <path d="M7 8l-1.5 1.5" />
      <path d="M16 12l5 5l-4 4l-5 -5" />
      <path d="M16 17l-1.5 1.5" />
    </svg>
  );
};

export default IconTools;
