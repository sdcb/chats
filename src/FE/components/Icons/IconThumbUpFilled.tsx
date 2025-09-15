import { IconProps } from './types';

const IconThumbUpFilled = (props: IconProps) => {
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
      <path stroke="none" d="M0 0h24v24H0z" fill="none" />
      {/* 左侧小矩形部分 - 仅描边 */}
      <path 
        d="M7 11v8a1 1 0 0 1 -1 1h-2a1 1 0 0 1 -1 -1v-7a1 1 0 0 1 1 -1h3" 
        fill="none"
        stroke="currentColor"
      />
      {/* 主要的拇指部分 - 填充 */}
      <path 
        d="M10 11a4 4 0 0 0 4 -4v-1a2 2 0 0 1 4 0v5h3a2 2 0 0 1 2 2l-1 5a2 3 0 0 1 -2 2h-7a3 3 0 0 1 -3 -3v-6z" 
        fill="currentColor"
        stroke="currentColor"
      />
      {/* 小圆点 - 仅描边 */}
      <circle cx="5" cy="16" r="1" fill="none" stroke="currentColor" />
    </svg>
  );
};

export default IconThumbUpFilled;