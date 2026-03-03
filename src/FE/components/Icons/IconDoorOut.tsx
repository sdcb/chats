import { IconProps } from './types';

const IconDoorOut = (props: IconProps) => {
  const { className, size = 20, strokeWidth = 1.6, stroke, onClick } = props;

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      onClick={onClick}
      className={className}
      width={size}
      height={size}
      strokeWidth={strokeWidth}
      stroke={stroke || 'currentColor'}
      viewBox="0 0 24 24"
      fill="none"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {/* Door frame */}
      <path d="M5 4h5" />
      <path d="M5 20h5" />
      <path d="M5 4v16" />
      {/* Door panel */}
      <path d="M10 4v16" />
      {/* Door knob */}
      <circle cx="8" cy="12" r="0.5" fill="currentColor" stroke="none" />
      {/* Arrow pointing out from door */}
      <path d="M14 12h7" />
      <path d="M18 9l3 3-3 3" />
    </svg>
  );
};

export default IconDoorOut;
