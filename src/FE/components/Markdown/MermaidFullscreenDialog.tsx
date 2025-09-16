import { FC, useEffect, useState, useRef } from 'react';
import { createPortal } from 'react-dom';
import { useTheme } from 'next-themes';

import useTranslation from '@/hooks/useTranslation';

import { IconX, IconClipboard, IconCheck, IconChartHistogram } from '@/components/Icons/index';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Slider } from '@/components/ui/slider';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  svgCode: string;
  mermaidCode: string;
  onCopy: () => void;
  isCopied: boolean;
}

export const MermaidFullscreenDialog: FC<Props> = ({
  isOpen,
  onClose,
  svgCode,
  mermaidCode,
  onCopy,
  isCopied,
}) => {
  const { t } = useTranslation();
  const { resolvedTheme } = useTheme();
  const diagramRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  
  // 缩放和拖拽状态
  const [scale, setScale] = useState(1);
  const [position, setPosition] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState({ x: 0, y: 0 });
  const [lastPosition, setLastPosition] = useState({ x: 0, y: 0 });
  // 触摸手势状态（移动端）
  const [isPinching, setIsPinching] = useState(false);
  const pinchStart = useRef({ distance: 0, scale: 1 });

  // 处理ESC键关闭
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };

    if (isOpen) {
      document.addEventListener('keydown', handleEscape);
      // 阻止背景滚动
      document.body.style.overflow = 'hidden';
    }

    return () => {
      document.removeEventListener('keydown', handleEscape);
      document.body.style.overflow = 'unset';
    };
  }, [isOpen, onClose]);

  // 重置缩放和位置当对话框打开时
  useEffect(() => {
    if (isOpen) {
      setScale(1);
      setPosition({ x: 0, y: 0 });
    }
  }, [isOpen]);

  // 处理鼠标滚轮缩放
  const handleWheel = (e: React.WheelEvent) => {
    if (!containerRef.current) return;
    
    e.preventDefault();
    const delta = e.deltaY * -0.002; // 减小缩放步长，提高精细度
    const newScale = Math.min(Math.max(0.1, scale + delta), 5); // 限制缩放范围 0.1x 到 5x
    setScale(newScale);
  };

  // 处理鼠标拖拽开始
  const handleMouseDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return; // 只处理左键
    setIsDragging(true);
    setDragStart({ x: e.clientX, y: e.clientY });
    setLastPosition(position);
    e.preventDefault();
  };

  // 处理鼠标拖拽移动
  const handleMouseMove = (e: React.MouseEvent) => {
    if (!isDragging) return;
    
    const deltaX = e.clientX - dragStart.x;
    const deltaY = e.clientY - dragStart.y;
    
    setPosition({
      x: lastPosition.x + deltaX,
      y: lastPosition.y + deltaY,
    });
  };

  // 处理鼠标拖拽结束
  const handleMouseUp = () => {
    setIsDragging(false);
  };

  // 处理滑块缩放变化
  const handleSliderChange = (value: number[]) => {
    setScale(value[0]);
  };

  // 全局鼠标事件监听
  useEffect(() => {
    if (isDragging) {
      const handleGlobalMouseMove = (e: MouseEvent) => {
        const deltaX = e.clientX - dragStart.x;
        const deltaY = e.clientY - dragStart.y;
        
        setPosition({
          x: lastPosition.x + deltaX,
          y: lastPosition.y + deltaY,
        });
      };

      const handleGlobalMouseUp = () => {
        setIsDragging(false);
      };

      document.addEventListener('mousemove', handleGlobalMouseMove);
      document.addEventListener('mouseup', handleGlobalMouseUp);

      return () => {
        document.removeEventListener('mousemove', handleGlobalMouseMove);
        document.removeEventListener('mouseup', handleGlobalMouseUp);
      };
    }
  }, [isDragging, dragStart, lastPosition]);

  // 触摸：计算两点间距离
  const getDistance = (touch1: React.Touch, touch2: React.Touch) => {
    const dx = touch2.clientX - touch1.clientX;
    const dy = touch2.clientY - touch1.clientY;
    return Math.hypot(dx, dy);
  };

  // 触摸开始
  const handleTouchStart = (e: React.TouchEvent) => {
    if (!containerRef.current) return;
    if (e.touches.length === 2) {
      const dist = getDistance(e.touches[0], e.touches[1]);
      pinchStart.current = { distance: dist, scale };
      setIsPinching(true);
    } else if (e.touches.length === 1) {
      setIsDragging(true);
      setDragStart({ x: e.touches[0].clientX, y: e.touches[0].clientY });
      setLastPosition(position);
    }
  };

  // 触摸移动
  const handleTouchMove = (e: React.TouchEvent) => {
    if (!containerRef.current) return;
    if (isPinching && e.touches.length >= 2) {
      e.preventDefault(); // 阻止浏览器默认缩放
      const dist = getDistance(e.touches[0], e.touches[1]);
      const factor = dist / pinchStart.current.distance;
      const newScale = Math.min(Math.max(0.1, pinchStart.current.scale * factor), 5);
      setScale(newScale);
    } else if (isDragging && e.touches.length === 1) {
      e.preventDefault(); // 阻止页面滚动
      const deltaX = e.touches[0].clientX - dragStart.x;
      const deltaY = e.touches[0].clientY - dragStart.y;
      setPosition({ x: lastPosition.x + deltaX, y: lastPosition.y + deltaY });
    }
  };

  // 触摸结束
  const handleTouchEnd = (e: React.TouchEvent) => {
    if (e.touches.length < 2) setIsPinching(false);
    if (e.touches.length === 0) setIsDragging(false);
  };

  if (!isOpen) return null;

  const modalContent = (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* 背景遮罩 */}
      <div
        className="absolute inset-0 bg-black/80"
        onClick={onClose}
      />
      
      {/* 对话框内容 */}
      <div className="relative z-10 w-[95vw] h-[95vh] bg-white dark:bg-gray-900 rounded-lg shadow-2xl flex flex-col">
        {/* 标题栏 */}
        <div className="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center">
            <IconChartHistogram size={20} className="text-gray-700 dark:text-gray-300" />
          </div>
          <div className="flex items-center gap-2">
            {/* 复制按钮 */}
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    onClick={onCopy}
                    className="flex items-center p-1.5 bg-gray-100 dark:bg-gray-800 hover:bg-gray-200 dark:hover:bg-gray-700 rounded-md transition-colors"
                  >
                    {isCopied ? (
                      <IconCheck size={14} className="text-green-600" />
                    ) : (
                      <IconClipboard size={14} />
                    )}
                  </button>
                </TooltipTrigger>
                <TooltipContent>
                  {isCopied ? t('Copied') : t('Click Copy')}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
            
            {/* 关闭按钮 */}
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    onClick={onClose}
                    className="p-1.5 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md transition-colors"
                  >
                    <IconX size={16} />
                  </button>
                </TooltipTrigger>
                <TooltipContent>
                  {t('Close')}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
        </div>
        
        {/* 图表内容区域 */}
        <div 
          ref={containerRef}
          className="flex-1 overflow-hidden relative flex items-center justify-center bg-gray-50 dark:bg-gray-800 touch-none"
          onWheel={handleWheel}
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
          onTouchEnd={handleTouchEnd}
          style={{ cursor: isDragging ? 'grabbing' : 'grab' }}
        >
          {svgCode ? (
            <div 
              ref={diagramRef}
              dangerouslySetInnerHTML={{ __html: svgCode }}
              className="mermaid-diagram select-none"
              style={{
                transform: `translate(${position.x}px, ${position.y}px) scale(${scale})`,
                transformOrigin: 'center center',
                transition: isDragging ? 'none' : 'transform 0.1s ease-out',
                maxWidth: 'none',
                maxHeight: 'none',
              }}
            />
          ) : (
            <div className="text-gray-500 dark:text-gray-400">
              {t('Loading...')}
            </div>
          )}
          
          {/* 右侧缩放滑块 */}
          <div className="absolute right-4 top-1/2 transform -translate-y-1/2 bg-white/90 dark:bg-gray-900/90 backdrop-blur-sm rounded-lg p-3 shadow-lg">
            <div className="flex flex-col items-center gap-3 h-40 w-8">
              <div className="text-xs text-gray-600 dark:text-gray-400 font-medium">
                {Math.round(scale * 100)}%
              </div>
              <Slider
                orientation="vertical"
                value={[scale]}
                onValueChange={handleSliderChange}
                min={0.1}
                max={5}
                step={0.05}
                className="h-24"
              />
              <div className="text-xs text-gray-500 dark:text-gray-500">
                缩放
              </div>
            </div>
          </div>
          
          {/* 操作提示 */}
          <div className="absolute bottom-4 left-4 bg-black/50 backdrop-blur-sm text-white px-2 py-1 rounded text-xs">
            滚轮缩放 • 拖拽移动
          </div>
        </div>
      </div>
    </div>
  );

  // 使用 Portal 渲染到 body
  return createPortal(modalContent, document.body);
};

MermaidFullscreenDialog.displayName = 'MermaidFullscreenDialog';