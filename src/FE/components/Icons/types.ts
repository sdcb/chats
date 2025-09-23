export interface IconProps {
  className?: string;
  size?: number | string;
  strokeWidth?: number;
  stroke?: string;
  onClick?: (e: React.MouseEvent<SVGSVGElement, MouseEvent>) => void;
  style?: React.CSSProperties;
}
