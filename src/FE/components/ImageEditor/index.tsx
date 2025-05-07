"use client";

import React, { useEffect, useRef, useState } from "react";
import { Button } from "../ui/button";
import { 
  ArrowLeft, 
  ArrowRight, 
  Circle, 
  Maximize, 
  Minimize, 
  MoveHorizontal,
  Pencil,
  RotateCcw,
  Droplets
} from "lucide-react";

interface Point {
  x: number;
  y: number;
}

interface DrawAction {
  points: Point[];
  size: number;
  color: string;
  opacity: number;
}

interface ImageEditorProps {
  /** 图片URL */
  imageUrl: string;
  /** 默认画笔大小 */
  defaultBrushSize?: number;
  /** 初始缩放级别 */
  initialZoom?: number;
  /** 默认画笔颜色 (CSS颜色值) */
  defaultColor?: string;
  /** 默认画笔透明度 (0-1) */
  defaultOpacity?: number;
}

const ImageEditor: React.FC<ImageEditorProps> = ({
  imageUrl,
  defaultBrushSize = 5,
  initialZoom = 1,
  defaultColor = "#FF0000",
  defaultOpacity = 0.5,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const cursorCanvasRef = useRef<HTMLCanvasElement>(null); // 鼠标光标画布
  const imgRef = useRef<HTMLImageElement | null>(null);
  
  const [isDrawing, setIsDrawing] = useState(false);
  const [brushSize, setBrushSize] = useState(defaultBrushSize);
  const [brushColor, setBrushColor] = useState(defaultColor);
  const [brushOpacity, setBrushOpacity] = useState(defaultOpacity);
  const [zoom, setZoom] = useState(initialZoom);
  const [actions, setActions] = useState<DrawAction[]>([]);
  const [redoStack, setRedoStack] = useState<DrawAction[]>([]);
  const [currentAction, setCurrentAction] = useState<DrawAction | null>(null);
  const [canvasPosition, setCanvasPosition] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [imageSize, setImageSize] = useState<{ width: number; height: number }>({
    width: 0,
    height: 0,
  });
  const [isImageLoaded, setIsImageLoaded] = useState(false);
  const [isImageLoading, setIsImageLoading] = useState(true);
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState<Point>({ x: 0, y: 0 });
  const [cursorPosition, setCursorPosition] = useState<Point>({ x: 0, y: 0 });
  const [isDragMode, setIsDragMode] = useState(false);
  const [showCursor, setShowCursor] = useState(false);

  // 加载图片
  useEffect(() => {
    setIsImageLoading(true);
    setIsImageLoaded(false);
    
    const img = new Image();
    
    // 设置加载和错误处理
    img.onload = () => {
      imgRef.current = img;
      setImageSize({
        width: img.width,
        height: img.height,
      });
      
      setIsImageLoaded(true);
      setIsImageLoading(false);
      
      if (canvasRef.current) {
        const canvas = canvasRef.current;
        canvas.width = img.width;
        canvas.height = img.height;
        
        // 确保在下一帧渲染图片
        requestAnimationFrame(() => {
          redrawCanvas();
          // 初始化时居中画布
          centerCanvas();
        });
      }
    };
    
    img.onerror = () => {
      console.error("图片加载失败:", imageUrl);
      setIsImageLoading(false);
    };
    
    // 设置src触发加载
    img.src = imageUrl;
    
    // 清理函数
    return () => {
      img.onload = null;
      img.onerror = null;
    };
  }, [imageUrl]);

  // 居中画布
  const centerCanvas = () => {
    if (!containerRef.current || !wrapperRef.current || !imageSize.width) return;
    
    const containerWidth = containerRef.current.clientWidth;
    const containerHeight = containerRef.current.clientHeight;
    
    // 计算居中位置
    const x = Math.max(0, (containerWidth - imageSize.width * zoom) / 2);
    const y = Math.max(0, (containerHeight - imageSize.height * zoom) / 2);
    
    setCanvasPosition({ x, y });
  };

  // 当缩放级别变化时重新居中画布
  useEffect(() => {
    if (isImageLoaded) {
      centerCanvas();
    }
  }, [zoom, isImageLoaded]);

  // 监听容器大小变化
  useEffect(() => {
    if (!containerRef.current) return;
    
    const resizeObserver = new ResizeObserver(() => {
      centerCanvas();
    });
    
    resizeObserver.observe(containerRef.current);
    
    return () => {
      resizeObserver.disconnect();
    };
  }, []);

  // 当缩放、图片加载状态或画布内容变化时，重绘画布
  useEffect(() => {
    if (isImageLoaded) {
      redrawCanvas();
    }
  }, [zoom, actions, isImageLoaded]);

  // 生成当前颜色的rgba格式字符串
  const getBrushColorRGBA = () => {
    // 如果已经是rgba格式，直接返回
    if (brushColor.startsWith('rgba')) {
      return brushColor;
    }
    
    // 如果是十六进制或RGB格式，转换为RGBA
    let r = 0, g = 0, b = 0;
    
    // 处理十六进制格式 (#RGB or #RRGGBB)
    if (brushColor.startsWith('#')) {
      const hex = brushColor.substring(1);
      if (hex.length === 3) {
        r = parseInt(hex[0] + hex[0], 16);
        g = parseInt(hex[1] + hex[1], 16);
        b = parseInt(hex[2] + hex[2], 16);
      } else if (hex.length === 6) {
        r = parseInt(hex.substring(0, 2), 16);
        g = parseInt(hex.substring(2, 4), 16);
        b = parseInt(hex.substring(4, 6), 16);
      }
    } 
    // 处理rgb格式
    else if (brushColor.startsWith('rgb')) {
      const rgbMatch = brushColor.match(/rgb\((\d+),\s*(\d+),\s*(\d+)\)/);
      if (rgbMatch) {
        r = parseInt(rgbMatch[1]);
        g = parseInt(rgbMatch[2]);
        b = parseInt(rgbMatch[3]);
      }
    }
    
    return `rgba(${r}, ${g}, ${b}, ${brushOpacity})`;
  };

  // 重绘画布函数
  const redrawCanvas = () => {
    if (!canvasRef.current || !imgRef.current || !isImageLoaded) return;
    
    const canvas = canvasRef.current;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    
    // 清除画布
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    // 绘制图片
    try {
      ctx.drawImage(imgRef.current, 0, 0, canvas.width, canvas.height);
      
      // 绘制所有涂鸦动作
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      
      actions.forEach((action) => {
        if (action.points.length < 2) return;
        
        // 绘制平滑的线条，不显示节点
        ctx.beginPath();
        ctx.moveTo(action.points[0].x, action.points[0].y);
        
        for (let i = 1; i < action.points.length; i++) {
          ctx.lineTo(action.points[i].x, action.points[i].y);
        }
        
        ctx.lineWidth = action.size; 
        
        // 使用动作自己的颜色和透明度
        const colorWithOpacity = action.color.startsWith('rgba') 
          ? action.color 
          : `rgba(${parseInt(action.color.substring(1, 3), 16)}, ${parseInt(action.color.substring(3, 5), 16)}, ${parseInt(action.color.substring(5, 7), 16)}, ${action.opacity})`;
        
        ctx.strokeStyle = colorWithOpacity;
        ctx.stroke();
      });
    } catch (error) {
      console.error("绘制图片时出错:", error);
    }
  };

  // 当画笔大小变化时，更新光标
  useEffect(() => {
    updateCursorCanvas();
  }, [brushSize, brushColor, brushOpacity]);

  // 更新鼠标光标画布
  const updateCursorCanvas = () => {
    if (!cursorCanvasRef.current) return;
    
    const canvas = cursorCanvasRef.current;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    
    // 设置画布大小为画笔大小的两倍，留出边缘空间
    const canvasSize = Math.max(40, brushSize * 2 + 10);
    canvas.width = canvasSize;
    canvas.height = canvasSize;
    
    // 清除画布
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    // 绘制圆形
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    
    // 获取当前的rgba颜色
    const currentColor = getBrushColorRGBA();
    const strokeColor = brushColor.startsWith('#') 
      ? brushColor 
      : `rgb(${parseInt(currentColor.split(',')[0].split('(')[1])}, ${parseInt(currentColor.split(',')[1])}, ${parseInt(currentColor.split(',')[2])})`;
    
    // 绘制外圈
    ctx.beginPath();
    ctx.arc(centerX, centerY, brushSize, 0, 2 * Math.PI);
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = 1.5;
    ctx.stroke();
    
    // 绘制内圈填充
    ctx.beginPath();
    ctx.arc(centerX, centerY, brushSize - 1, 0, 2 * Math.PI);
    ctx.fillStyle = currentColor;
    ctx.fill();
  };

  // 处理鼠标或触摸事件的辅助函数
  const getCanvasPoint = (clientX: number, clientY: number): Point => {
    if (!canvasRef.current || !containerRef.current) return { x: 0, y: 0 };
    
    const canvas = canvasRef.current;
    const rect = canvas.getBoundingClientRect();
    
    return {
      x: (clientX - rect.left) / zoom,
      y: (clientY - rect.top) / zoom,
    };
  };

  // 鼠标/触摸事件处理
  const handlePointerDown = (e: React.PointerEvent) => {
    if (!isImageLoaded) return;
    
    // 右键拖动整个画布或拖动模式下的左键
    if (e.button === 2 || (isDragMode && e.button === 0)) {
      e.preventDefault();
      setIsDragging(true);
      setDragStart({ x: e.clientX, y: e.clientY });
      return;
    }
    
    // 左键绘制 (非拖动模式)
    if (e.button === 0 && !isDragMode) {
      const point = getCanvasPoint(e.clientX, e.clientY);
      setIsDrawing(true);
      setCurrentAction({ 
        points: [point], 
        size: brushSize,
        color: brushColor,
        opacity: brushOpacity
      });
    }
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    // 更新鼠标位置
    const canvasPoint = getCanvasPoint(e.clientX, e.clientY);
    setCursorPosition(canvasPoint);
    
    // 拖动画布
    if (isDragging) {
      const dx = e.clientX - dragStart.x;
      const dy = e.clientY - dragStart.y;
      
      setCanvasPosition(prev => ({
        x: prev.x + dx,
        y: prev.y + dy
      }));
      
      setDragStart({ x: e.clientX, y: e.clientY });
      return;
    }
    
    // 绘制
    if (!isDrawing || !currentAction || !isImageLoaded || isDragMode) return;
    
    const point = getCanvasPoint(e.clientX, e.clientY);
    setCurrentAction((prev) => {
      if (!prev) return null;
      
      const newAction = { 
        ...prev, 
        points: [...prev.points, point] 
      };
      
      // 临时绘制当前正在进行的笔画 - 只绘制线条，不绘制节点圆圈
      if (canvasRef.current) {
        const canvas = canvasRef.current;
        const ctx = canvas.getContext("2d");
        if (ctx) {
          // 设置线条样式
          ctx.lineCap = "round";
          ctx.lineJoin = "round";
          ctx.lineWidth = brushSize;
          
          // 使用当前的颜色和透明度
          ctx.strokeStyle = getBrushColorRGBA();
          
          // 重绘整个当前动作的线条，确保平滑连贯
          // 首先清除上一次的临时绘制
          redrawCanvas(); // 重绘底层图片和已完成的动作
          
          // 然后重新绘制整个当前动作
          if (newAction.points.length >= 2) {
            ctx.beginPath();
            ctx.moveTo(newAction.points[0].x, newAction.points[0].y);
            
            for (let i = 1; i < newAction.points.length; i++) {
              ctx.lineTo(newAction.points[i].x, newAction.points[i].y);
            }
            
            ctx.stroke();
          }
        }
      }
      
      return newAction;
    });
  };

  const handlePointerUp = (e: React.PointerEvent) => {
    // 结束拖动
    if (isDragging) {
      setIsDragging(false);
      return;
    }
    
    // 结束绘制
    setIsDrawing(false);
    
    if (currentAction && currentAction.points.length > 0 && !isDragMode) {
      setActions((prev) => [...prev, currentAction]);
      setRedoStack([]); // 清空重做栈
      setCurrentAction(null);
    }
  };

  // 处理鼠标进入画布区域
  const handlePointerEnter = (e: React.PointerEvent) => {
    setShowCursor(true);
  };

  // 处理鼠标离开画布区域
  const handlePointerLeave = (e: React.PointerEvent) => {
    setShowCursor(false);
    handlePointerUp(e);
  };

  // 切换拖动模式
  const toggleDragMode = () => {
    setIsDragMode(!isDragMode);
    setIsDrawing(false);
  };

  // 防止右键菜单
  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    return false;
  };

  // 鼠标滚轮缩放处理
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    
    if (!containerRef.current || !isImageLoaded || !wrapperRef.current) return;
    
    // 获取容器和画布位置信息
    const containerRect = containerRef.current.getBoundingClientRect();
    const wrapperRect = wrapperRef.current.getBoundingClientRect();
    
    // 获取鼠标在容器中的位置
    const mouseX = e.clientX - containerRect.left;
    const mouseY = e.clientY - containerRect.top;
    
    // 计算鼠标相对于画布的位置（考虑当前画布位置和缩放）
    const canvasX = mouseX - canvasPosition.x;
    const canvasY = mouseY - canvasPosition.y;
    
    // 计算鼠标在图片原始坐标中的位置（不受缩放影响的坐标）
    const imagePointX = canvasX / zoom;
    const imagePointY = canvasY / zoom;
    
    // 计算缩放因子 - 使用较小的增量使缩放更平滑
    const scaleFactor = e.deltaY > 0 ? 0.95 : 1.05; // 向下滚动缩小，向上滚动放大
    const newZoom = Math.max(0.1, Math.min(5, zoom * scaleFactor));
    
    // 计算缩放后鼠标位置对应的新画布坐标
    const newCanvasX = imagePointX * newZoom;
    const newCanvasY = imagePointY * newZoom;
    
    // 为了保持鼠标指向同一个图像点，需要调整画布位置
    const newPositionX = mouseX - newCanvasX;
    const newPositionY = mouseY - newCanvasY;
    
    // 使用requestAnimationFrame优化性能
    window.requestAnimationFrame(() => {
      setZoom(newZoom);
      setCanvasPosition({
        x: newPositionX,
        y: newPositionY
      });
    });
  };

  // 撤销功能
  const handleUndo = () => {
    if (actions.length === 0) return;
    
    const newActions = [...actions];
    const removedAction = newActions.pop();
    
    if (removedAction) {
      setActions(newActions);
      setRedoStack((prev) => [...prev, removedAction]);
    }
  };

  // 重做功能
  const handleRedo = () => {
    if (redoStack.length === 0) return;
    
    const newRedoStack = [...redoStack];
    const actionToRestore = newRedoStack.pop();
    
    if (actionToRestore) {
      setRedoStack(newRedoStack);
      setActions((prev) => [...prev, actionToRestore]);
    }
  };

  // 调整画笔大小
  const increaseBrushSize = () => {
    setBrushSize((prev) => Math.min(50, prev + 2));
  };

  const decreaseBrushSize = () => {
    setBrushSize((prev) => Math.max(1, prev - 2));
  };

  // 强制重新渲染画布
  const handleForceRedraw = () => {
    if (isImageLoaded) {
      redrawCanvas();
    }
  };

  // 重置缩放和位置
  const resetZoomAndPosition = () => {
    if (!containerRef.current || !imageSize.width) return;
    
    const containerWidth = containerRef.current.clientWidth;
    const containerHeight = containerRef.current.clientHeight;
    
    // 计算居中位置，重置到100%
    const newZoom = 1;
    const x = Math.max(0, (containerWidth - imageSize.width * newZoom) / 2);
    const y = Math.max(0, (containerHeight - imageSize.height * newZoom) / 2);
    
    setZoom(newZoom);
    setCanvasPosition({ x, y });
  };

  // 处理颜色变化
  const handleColorChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setBrushColor(e.target.value);
  };

  // 处理透明度变化
  const handleOpacityChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setBrushOpacity(parseFloat(e.target.value));
  };

  return (
    <div className="flex flex-col gap-4 w-full">
      <div className="flex flex-wrap gap-2 mb-2">
        <Button
          variant="outline"
          size="sm"
          onClick={handleUndo}
          disabled={actions.length === 0 || !isImageLoaded}
          title="撤销"
        >
          <RotateCcw className="h-4 w-4 mr-1" />
          撤销
        </Button>
        
        <Button
          variant="outline"
          size="sm"
          onClick={handleRedo}
          disabled={redoStack.length === 0 || !isImageLoaded}
          title="重做"
        >
          <ArrowRight className="h-4 w-4 mr-1" />
          重做
        </Button>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm" 
            onClick={decreaseBrushSize}
            disabled={!isImageLoaded}
            title="减小画笔大小"
          >
            <Circle className="h-3 w-3" />
          </Button>
          
          <span className="text-sm">{brushSize}px</span>
          
          <Button
            variant="outline"
            size="sm"
            onClick={increaseBrushSize}
            disabled={!isImageLoaded}
            title="增大画笔大小"
          >
            <Circle className="h-5 w-5" />
          </Button>
        </div>

        <div className="flex items-center gap-2">
          <input
            type="color"
            value={brushColor}
            onChange={handleColorChange}
            disabled={!isImageLoaded}
            className="w-8 h-8 rounded cursor-pointer border border-gray-300"
            title="画笔颜色"
          />
          
          <div className="flex items-center gap-1">
            <Droplets className="h-4 w-4 text-gray-500" />
            <input
              type="range"
              min="0"
              max="1"
              step="0.05"
              value={brushOpacity}
              onChange={handleOpacityChange}
              disabled={!isImageLoaded}
              className="w-20"
              title="画笔透明度"
            />
            <span className="text-xs text-gray-500">{Math.round(brushOpacity * 100)}%</span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant={isDragMode ? "default" : "outline"}
            size="sm"
            onClick={toggleDragMode}
            disabled={!isImageLoaded}
            title={isDragMode ? "切换到绘制模式" : "切换到拖动模式"}
          >
            <MoveHorizontal className="h-4 w-4 mr-1" />
            {isDragMode ? "拖动" : "绘制"}
          </Button>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              const newZoom = Math.max(0.1, zoom - 0.1);
              setZoom(newZoom);
            }}
            disabled={!isImageLoaded}
            title="缩小"
          >
            <Minimize className="h-4 w-4" />
          </Button>
          
          <span className="text-sm">{Math.round(zoom * 100)}%</span>
          
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              const newZoom = Math.min(5, zoom + 0.1);
              setZoom(newZoom);
            }}
            disabled={!isImageLoaded}
            title="放大"
          >
            <Maximize className="h-4 w-4" />
          </Button>
          
          <Button
            variant="outline"
            size="sm"
            onClick={resetZoomAndPosition}
            disabled={!isImageLoaded}
            title="重置缩放"
          >
            100%
          </Button>
        </div>
      </div>

      <div 
        ref={containerRef}
        className="relative overflow-hidden border border-gray-200 rounded-md"
        style={{ 
          maxWidth: "100%",
          height: "70vh",
          cursor: isDragging ? "grabbing" : isDragMode ? "grab" : "none"
        }}
        onWheel={handleWheel}
        onContextMenu={handleContextMenu}
      >
        {isImageLoading && (
          <div className="absolute inset-0 flex items-center justify-center bg-gray-100 bg-opacity-50 z-10">
            <div className="text-gray-500">图片加载中...</div>
          </div>
        )}
        
        {!isImageLoading && !isImageLoaded && (
          <div className="absolute inset-0 flex items-center justify-center bg-gray-100 z-10">
            <div className="text-red-500">图片加载失败</div>
          </div>
        )}
        
        <div
          ref={wrapperRef}
          className="absolute"
          style={{
            transform: `translate(${canvasPosition.x}px, ${canvasPosition.y}px)`,
            width: imageSize.width * zoom || 300, 
            height: imageSize.height * zoom || 200,
            visibility: isImageLoaded ? 'visible' : 'hidden',
            willChange: 'transform', // 提高性能
            transformOrigin: 'center center' // 确保从中心缩放
          }}
        >
          <canvas
            ref={canvasRef}
            width={imageSize.width || 300}
            height={imageSize.height || 200}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            onPointerEnter={handlePointerEnter}
            onPointerLeave={handlePointerLeave}
            className="touch-none cursor-crosshair"
            style={{
              width: "100%",
              height: "100%",
            }}
          />
          
          {/* 鼠标光标预览 */}
          {showCursor && isImageLoaded && !isDragMode && !isDrawing && (
            <div
              className="pointer-events-none absolute"
              style={{
                left: (cursorPosition.x * zoom) - (cursorCanvasRef.current?.width || 0) / 2,
                top: (cursorPosition.y * zoom) - (cursorCanvasRef.current?.height || 0) / 2,
                transform: `scale(${1/zoom})`, // 缩放补偿
                transformOrigin: 'center center',
              }}
            >
              <canvas 
                ref={cursorCanvasRef} 
                width={40} 
                height={40}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default ImageEditor;
