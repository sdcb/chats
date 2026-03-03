import { IconProps } from './types';

const IconDoorIn = (props: IconProps) => {
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
      <path d="M13 4h5a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-5" />
      {/* Door panel */}
      <path d="M11 4v16" />
      {/* Door knob */}
      <circle cx="15" cy="12" r="0.5" fill="currentColor" stroke="none" />
      {/* Arrow pointing into door */}
      <path d="M3 12h7" />
      <path d="M7 9l3 3-3 3" />
    </svg>
  );
};

export default IconDoorIn;
