import { IconProps } from './types';

const IconMessageStar = (props: IconProps) => {
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
      {/* 消息气泡 - 缩小尺寸 */}
      <path d="M7 9h7" />
      <path d="M7 12h5" />
      <path d="M16 5a2.5 2.5 0 0 1 2.5 2.5v6a2.5 2.5 0 0 1 -2.5 2.5h-4l-4 2.5v-2.5h-1.5a2.5 2.5 0 0 1 -2.5 -2.5v-6a2.5 2.5 0 0 1 2.5 -2.5h10z" />
      {/* 右上角四芒星 - 闪光效果 */}
      <path 
        d="M19 0.5l1 3.5 3.5 1 -3.5 1 -1 3.5 -1 -3.5 -3.5 -1 3.5 -1z" 
        fill="currentColor"
        stroke="currentColor"
        strokeWidth="0.4"
      />
    </svg>
  );

};

export default IconMessageStar;
