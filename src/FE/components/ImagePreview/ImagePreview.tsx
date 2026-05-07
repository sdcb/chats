import { useEffect, useState, useRef } from 'react';
import { IconX, IconChevronLeft, IconChevronRight } from '../Icons';
import { cn } from '@/lib/utils';

const MIN_SCALE = 1;
const MAX_SCALE = 5;

interface ImagePreviewProps {
  images: string[];
  initialIndex: number;
  isOpen: boolean;
  onClose: () => void;
  sourceElement?: HTMLImageElement | null;
}

const ImagePreview = ({ images, initialIndex, isOpen, onClose, sourceElement }: ImagePreviewProps) => {
  const [currentIndex, setCurrentIndex] = useState(initialIndex);
  const [isAnimating, setIsAnimating] = useState(false);
  const [hasEnterStarted, setHasEnterStarted] = useState(false);
  const [animationStyle, setAnimationStyle] = useState<React.CSSProperties>({});
  const [scale, setScale] = useState(MIN_SCALE);
  const [position, setPosition] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState({ x: 0, y: 0 });
  const [lastPosition, setLastPosition] = useState({ x: 0, y: 0 });
  const [isPinching, setIsPinching] = useState(false);
  const imageRef = useRef<HTMLImageElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const pinchStart = useRef({ distance: 0, scale: MIN_SCALE });

  const resetTransform = () => {
    setScale(MIN_SCALE);
    setPosition({ x: 0, y: 0 });
    setIsDragging(false);
    setIsPinching(false);
  };

  const clampScale = (nextScale: number) => {
    return Math.min(Math.max(MIN_SCALE, nextScale), MAX_SCALE);
  };

  const getDistance = (touch1: React.Touch, touch2: React.Touch) => {
    const dx = touch2.clientX - touch1.clientX;
    const dy = touch2.clientY - touch1.clientY;
    return Math.hypot(dx, dy);
  };

  const handleClose = () => {
    // 简单的淡出效果
    onClose();
  };

  useEffect(() => {
    setCurrentIndex(initialIndex);
    resetTransform();
  }, [initialIndex]);

  useEffect(() => {
    if (isOpen) {
      resetTransform();
    }
  }, [currentIndex, isOpen]);

  useEffect(() => {
    if (isOpen && sourceElement) {
      // 获取源图片的位置和尺寸
      const rect = sourceElement.getBoundingClientRect();
      
      // 设置初始动画样式（从源图片位置和尺寸开始）
      setHasEnterStarted(true);
      setAnimationStyle({
        position: 'fixed',
        top: `${rect.top}px`,
        left: `${rect.left}px`,
        width: `${rect.width}px`,
        height: `${rect.height}px`,
        maxWidth: `${rect.width}px`,
        maxHeight: `${rect.height}px`,
        transition: 'none',
        opacity: 1,
        objectFit: 'cover',
      });
      
      setIsAnimating(true);

      // 使用 requestAnimationFrame 确保初始样式被应用
      requestAnimationFrame(() => {
        requestAnimationFrame(() => {
          // 动画到全屏居中，同时放大
          setAnimationStyle({
            position: 'fixed',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            maxWidth: '90vw',
            maxHeight: '90vh',
            width: 'auto',
            height: 'auto',
            transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
            opacity: 1,
            objectFit: 'contain',
          });
        });
      });

      // 动画结束后清除动画状态
      const timer = setTimeout(() => {
        setIsAnimating(false);
        setAnimationStyle({});
      }, 300);

      return () => clearTimeout(timer);
    } else if (!isOpen) {
      setIsAnimating(false);
      setHasEnterStarted(false);
      setAnimationStyle({});
    }
  }, [isOpen, sourceElement]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        handleClose();
      } else if (e.key === 'ArrowLeft') {
        handlePrevious();
      } else if (e.key === 'ArrowRight') {
        handleNext();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, currentIndex, images.length]);

  useEffect(() => {
    if (!isOpen) return;

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.body.style.overflow = originalOverflow;
    };
  }, [isOpen]);

  useEffect(() => {
    if (!isDragging) return;

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
  }, [dragStart, isDragging, lastPosition]);

  const handlePrevious = () => {
    setCurrentIndex((prev) => (prev > 0 ? prev - 1 : images.length - 1));
  };

  const handleNext = () => {
    setCurrentIndex((prev) => (prev < images.length - 1 ? prev + 1 : 0));
  };

  const handleWheel = (e: React.WheelEvent<HTMLDivElement>) => {
    if (isAnimating || !containerRef.current) return;

    e.preventDefault();
    const delta = e.deltaY * -0.002;
    setScale((prev) => clampScale(prev + delta));
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    if (isAnimating || scale <= MIN_SCALE || e.button !== 0) return;

    setIsDragging(true);
    setDragStart({ x: e.clientX, y: e.clientY });
    setLastPosition(position);
    e.preventDefault();
  };

  const handleTouchStart = (e: React.TouchEvent<HTMLDivElement>) => {
    if (isAnimating || !containerRef.current) return;

    if (e.touches.length === 2) {
      const distance = getDistance(e.touches[0], e.touches[1]);
      pinchStart.current = { distance, scale };
      setIsPinching(true);
      setIsDragging(false);
      return;
    }

    if (e.touches.length === 1 && scale > MIN_SCALE) {
      setIsDragging(true);
      setDragStart({ x: e.touches[0].clientX, y: e.touches[0].clientY });
      setLastPosition(position);
    }
  };

  const handleTouchMove = (e: React.TouchEvent<HTMLDivElement>) => {
    if (isAnimating || !containerRef.current) return;

    if (isPinching && e.touches.length >= 2) {
      e.preventDefault();
      const distance = getDistance(e.touches[0], e.touches[1]);
      const factor = distance / pinchStart.current.distance;
      setScale(clampScale(pinchStart.current.scale * factor));
      return;
    }

    if (isDragging && e.touches.length === 1 && scale > MIN_SCALE) {
      e.preventDefault();
      const deltaX = e.touches[0].clientX - dragStart.x;
      const deltaY = e.touches[0].clientY - dragStart.y;
      setPosition({
        x: lastPosition.x + deltaX,
        y: lastPosition.y + deltaY,
      });
    }
  };

  const handleTouchEnd = (e: React.TouchEvent<HTMLDivElement>) => {
    if (e.touches.length < 2) {
      setIsPinching(false);
    }

    if (e.touches.length === 1 && scale > MIN_SCALE) {
      setIsDragging(true);
      setDragStart({ x: e.touches[0].clientX, y: e.touches[0].clientY });
      setLastPosition(position);
      return;
    }

    if (e.touches.length === 0) {
      setIsDragging(false);
    }
  };

  if (!isOpen) return null;

  return (
    <>
      {/* 背景蒙板 - 半灰色（常驻，不跟随图片动画切换，避免闪烁） */}
      <div
        className={"fixed inset-0 z-[9998] bg-black/70 backdrop-blur-sm"}
        onClick={handleClose}
      />
      
      {/* 内容层 */}
      <div className="fixed inset-0 z-[9999] flex items-center justify-center pointer-events-none">
        {/* 关闭按钮 */}
        {hasEnterStarted && (
        <button
          className={cn(
            "absolute top-4 right-4 text-white hover:text-gray-300 transition-all z-10 pointer-events-auto",
            isAnimating ? "opacity-0 scale-50" : "opacity-100 scale-100"
          )}
          onClick={handleClose}
          style={{ transitionDuration: '300ms', transitionDelay: isAnimating ? '0ms' : '120ms' }}
        >
          <IconX size={32} />
        </button>
        )}

        {/* 图片计数器 */}
        {hasEnterStarted && images.length > 1 && (
          <div 
            className={cn(
              "absolute top-4 left-1/2 -translate-x-1/2 text-white text-sm bg-black/50 px-3 py-1 rounded-full transition-all",
              isAnimating ? "opacity-0 -translate-y-4" : "opacity-100 translate-y-0"
            )}
            style={{ transitionDuration: '300ms', transitionDelay: isAnimating ? '0ms' : '150ms' }}
          >
            {currentIndex + 1} / {images.length}
          </div>
        )}

        {/* 左箭头 */}
        {hasEnterStarted && images.length > 1 && (
          <button
            className={cn(
              "absolute left-4 text-white hover:text-gray-300 transition-all z-10 p-2 bg-black/50 rounded-full pointer-events-auto",
              isAnimating ? "opacity-0 -translate-x-4" : "opacity-100 translate-x-0"
            )}
            onClick={(e) => {
              e.stopPropagation();
              handlePrevious();
            }}
            style={{ transitionDuration: '300ms', transitionDelay: isAnimating ? '0ms' : '150ms' }}
          >
            <IconChevronLeft size={32} />
          </button>
        )}

        {/* 图片 */}
        <div
          ref={containerRef}
          className="max-w-[90vw] max-h-[90vh] flex items-center justify-center pointer-events-auto overflow-hidden touch-none select-none"
          onClick={(e) => e.stopPropagation()}
          onWheel={handleWheel}
          onMouseDown={handleMouseDown}
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
          onTouchEnd={handleTouchEnd}
          style={{ cursor: scale > MIN_SCALE ? (isDragging ? 'grabbing' : 'grab') : 'default' }}
        >
          <div
            className="flex items-center justify-center"
            style={{
              transform: isAnimating
                ? undefined
                : `translate(${position.x}px, ${position.y}px) scale(${scale})`,
              transformOrigin: 'center center',
              transition: isAnimating || isDragging || isPinching ? 'none' : 'transform 0.15s ease-out',
            }}
          >
            <img
              ref={imageRef}
              src={images[currentIndex]}
              alt={`Preview ${currentIndex + 1}`}
              className="max-w-full max-h-[90vh] object-contain"
              style={{ ...(isAnimating ? animationStyle : {}), visibility: hasEnterStarted ? 'visible' : 'hidden' }}
            />
          </div>
        </div>

        {/* 右箭头 */}
        {hasEnterStarted && images.length > 1 && (
          <button
            className={cn(
              "absolute right-4 text-white hover:text-gray-300 transition-all z-10 p-2 bg-black/50 rounded-full pointer-events-auto",
              isAnimating ? "opacity-0 translate-x-4" : "opacity-100 translate-x-0"
            )}
            onClick={(e) => {
              e.stopPropagation();
              handleNext();
            }}
            style={{ transitionDuration: '300ms', transitionDelay: isAnimating ? '0ms' : '150ms' }}
          >
            <IconChevronRight size={32} />
          </button>
        )}

        {/* 缩略图导航 */}
        {hasEnterStarted && images.length > 1 && (
          <div 
            className={cn(
              "absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-2 bg-black/50 p-2 rounded-lg max-w-[90vw] overflow-x-auto transition-all pointer-events-auto",
              isAnimating ? "opacity-0 translate-y-4" : "opacity-100 translate-y-0"
            )}
            style={{ transitionDuration: '300ms', transitionDelay: isAnimating ? '0ms' : '180ms' }}
          >
          {images.map((img, index) => (
            <button
              key={index}
              className={cn(
                'w-16 h-16 flex-shrink-0 rounded overflow-hidden border-2 transition-all',
                index === currentIndex
                  ? 'border-white scale-110'
                  : 'border-transparent opacity-60 hover:opacity-100'
              )}
              onClick={(e) => {
                e.stopPropagation();
                setCurrentIndex(index);
              }}
            >
              <img
                src={img}
                alt={`Thumbnail ${index + 1}`}
                className="w-full h-full object-cover"
              />
            </button>
          ))}
        </div>
      )}
      </div>
    </>
  );
};

export default ImagePreview;
