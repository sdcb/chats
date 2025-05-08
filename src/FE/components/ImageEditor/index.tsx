import React, { useRef, useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Undo, Redo, X, Save, ZoomIn, ZoomOut, Eye } from 'lucide-react';

interface Point {
  x: number;
  y: number;
}

interface Stroke {
  points: Point[];
  color: string;
  width: number;
}

interface ImageEditorProps {
  imageUrl: string;
  maskPath?: React.ReactNode; // 变更为React.ReactNode，接受自定义元素
  onSave?: (editedMaskDataUrl: string) => void;
  brushSize?: number;
  brushColor?: string;
  defaultMaskEnabled?: boolean; // 是否启用默认蒙板
}

const ImageEditor: React.FC<ImageEditorProps> = ({
  imageUrl,
  maskPath,
  onSave,
  brushSize = 50,
  brushColor = '#FFFFFF',
  defaultMaskEnabled = false // 默认不启用全白蒙板
}) => {
  const imageCanvasRef = useRef<HTMLCanvasElement>(null);
  const maskCanvasRef = useRef<HTMLCanvasElement>(null); // 蒙板Canvas引用，用于绘制和存储
  const containerRef = useRef<HTMLDivElement>(null);
  const maskDataRef = useRef<ImageData | null>(null);
  
  const [isDrawing, setIsDrawing] = useState(false);
  const [currentStroke, setCurrentStroke] = useState<Stroke>({ points: [], color: brushColor, width: brushSize });
  const [strokes, setStrokes] = useState<Stroke[]>([]);
  const [history, setHistory] = useState<Stroke[][]>([]);
  const [redoStack, setRedoStack] = useState<Stroke[][]>([]);
  const [canvasSize, setCanvasSize] = useState({ width: 0, height: 0 });
  const [imageLoaded, setImageLoaded] = useState(false);
  const [zoomLevel, setZoomLevel] = useState(1);
  const [imageData, setImageData] = useState<HTMLImageElement | null>(null);
  const [debugMode, setDebugMode] = useState(false);

  // 固定比例计算改为根据图片比例计算
  const calculateCanvasSize = (containerWidth: number, image: HTMLImageElement) => {
    // 获取图片原始宽高比
    const imageRatio = image.naturalHeight / image.naturalWidth;
    // 根据容器宽度和图片比例计算画布高度
    const height = containerWidth * imageRatio;
    return { width: containerWidth, height };
  };

  // 加载图片
  useEffect(() => {
    const image = new Image();
    image.onload = () => {
      if (imageCanvasRef.current && containerRef.current) {
        const containerWidth = containerRef.current.clientWidth;
        // 使用图片原始比例
        const size = calculateCanvasSize(containerWidth, image);
        
        setCanvasSize(size);
        setImageData(image);
        
        // 设置所有Canvas的尺寸
        if (imageCanvasRef.current) {
          imageCanvasRef.current.width = size.width;
          imageCanvasRef.current.height = size.height;
        }
        
        if (maskCanvasRef.current) {
          maskCanvasRef.current.width = size.width;
          maskCanvasRef.current.height = size.height;
        }
        
        drawImageWithZoom(image, size);
        
        // 初始化蒙板
        initMaskCanvas(size);
      }
    };
    image.onerror = (e) => {
      console.error('图片加载失败:', e);
    };
    image.src = imageUrl;
  }, [imageUrl]);

  // 初始化蒙板Canvas
  const initMaskCanvas = (size: { width: number, height: number }) => {
    if (!maskCanvasRef.current) return;
    
    const maskCtx = maskCanvasRef.current.getContext('2d', { alpha: true });
    if (!maskCtx) return;
    
    // 清除画布
    maskCtx.clearRect(0, 0, size.width, size.height);
    
    // 默认情况下蒙板透明，只有在defaultMaskEnabled为true时创建全白蒙板
    if (defaultMaskEnabled) {
      maskCtx.fillStyle = 'rgba(255, 255, 255, 1)';
      maskCtx.fillRect(0, 0, size.width, size.height);
    } else {
      // 为了让涂鸦能够工作，创建一个全白但完全透明的蒙板
      maskCtx.fillStyle = 'rgba(255, 255, 255, 0.01)';
      maskCtx.fillRect(0, 0, size.width, size.height);
    }
    
    // 获取蒙板数据
    try {
      maskDataRef.current = maskCtx.getImageData(0, 0, size.width, size.height);
      console.log('蒙板初始化成功，尺寸:', size.width, size.height);
    } catch (error) {
      console.error('无法获取蒙板数据:', error);
    }
    
    setImageLoaded(true);
  };

  // 根据缩放级别绘制图片
  const drawImageWithZoom = (image: HTMLImageElement, size: { width: number, height: number }) => {
    if (!imageCanvasRef.current) return;
    
    const ctx = imageCanvasRef.current.getContext('2d');
    if (!ctx) return;
    
    ctx.clearRect(0, 0, size.width, size.height);
    
    // 直接按原始尺寸绘制图像，不再在这里应用缩放
    // 缩放改为通过CSS transform实现
    ctx.drawImage(image, 0, 0, size.width, size.height);
  };

  // 当缩放级别变化时重新绘制
  useEffect(() => {
    if (imageData) {
      drawImageWithZoom(imageData, canvasSize);
      
      // 更新蒙板显示
      if (maskDataRef.current && debugMode) {
        updateDebugMaskDisplay();
      }
      
      drawStrokes();
    }
  }, [zoomLevel, imageData, canvasSize]);

  // 当窗口大小变化时，更新所有Canvas尺寸
  useEffect(() => {
    const handleResize = () => {
      if (!imageData || !containerRef.current) return;
      
      const containerWidth = containerRef.current.clientWidth;
      const newSize = calculateCanvasSize(containerWidth, imageData);
      
      if (newSize.width !== canvasSize.width || newSize.height !== canvasSize.height) {
        // 更新所有Canvas尺寸
        if (imageCanvasRef.current) {
          imageCanvasRef.current.width = newSize.width;
          imageCanvasRef.current.height = newSize.height;
        }
        
        if (maskCanvasRef.current) {
          maskCanvasRef.current.width = newSize.width;
          maskCanvasRef.current.height = newSize.height;
        }
        
        setCanvasSize(newSize);
        
        // 重新初始化蒙板以保持尺寸一致
        initMaskCanvas(newSize);
        
        // 重新绘制图像
        drawImageWithZoom(imageData, newSize);
      }
    };
    
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [imageData, canvasSize]);

  // 每次笔画变化时重新绘制
  useEffect(() => {
    if (imageLoaded) {
      drawStrokes();
    }
  }, [strokes, imageLoaded, canvasSize, debugMode]);

  // 更新调试模式下的蒙板显示
  const updateDebugMaskDisplay = () => {
    if (!maskDataRef.current || !maskCanvasRef.current) return;
    
    const ctx = maskCanvasRef.current.getContext('2d', { alpha: true });
    if (!ctx) return;
    
    // 清除画布，保留已有的笔画
    const tempCanvas = document.createElement('canvas');
    tempCanvas.width = canvasSize.width;
    tempCanvas.height = canvasSize.height;
    const tempCtx = tempCanvas.getContext('2d', { alpha: true });
    if (!tempCtx) return;
    
    // 先绘制当前蒙板
    tempCtx.drawImage(maskCanvasRef.current, 0, 0);
    
    // 清除画布
    ctx.clearRect(0, 0, canvasSize.width, canvasSize.height);
    
    try {
      // 创建一个红色蒙板用于显示
      const tempMaskData = new ImageData(
        new Uint8ClampedArray(maskDataRef.current.data), 
        maskDataRef.current.width, 
        maskDataRef.current.height
      );
      
      for (let i = 0; i < tempMaskData.data.length; i += 4) {
        if (tempMaskData.data[i + 3] > 0) {
          tempMaskData.data[i] = 255; // R
          tempMaskData.data[i + 1] = 0; // G
          tempMaskData.data[i + 2] = 0; // B
          tempMaskData.data[i + 3] = 128; // Alpha (半透明)
        }
      }
      
      ctx.putImageData(tempMaskData, 0, 0);
      
      // 绘制回之前的笔画
      ctx.drawImage(tempCanvas, 0, 0);
    } catch (error) {
      console.error('调试模式下显示蒙板失败:', error);
    }
  };

  // 处理滚轮缩放
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    const delta = e.deltaY < 0 ? 0.1 : -0.1;
    setZoomLevel(prev => {
      const newZoom = Math.max(0.5, Math.min(3, prev + delta));
      return newZoom;
    });
  };

  // 放大按钮
  const handleZoomIn = () => {
    setZoomLevel(prev => Math.min(3, prev + 0.1));
  };

  // 缩小按钮
  const handleZoomOut = () => {
    setZoomLevel(prev => Math.max(0.5, prev - 0.1));
  };

  // 检查点是否在蒙板内部（非透明区域）
  const isPointInMask = (x: number, y: number): boolean => {
    // 先处理边界情况
    if (x < 0 || y < 0 || x >= canvasSize.width || y >= canvasSize.height) {
      console.log('点超出画布边界:', x, y, canvasSize);
      return false;
    }
    
    // 如果没有蒙板数据，默认允许涂鸦
    if (!maskDataRef.current) {
      return true;
    }
    
    try {
      // 考虑缩放因素，计算在蒙板上的实际像素位置
      const zoomOffset = {
        x: (canvasSize.width - canvasSize.width * zoomLevel) / 2,
        y: (canvasSize.height - canvasSize.height * zoomLevel) / 2
      };
      
      // 需要根据当前缩放水平调整检测点的坐标
      const maskX = Math.floor(x);
      const maskY = Math.floor(y);
      
      // 获取像素在maskData中的索引位置
      const index = (maskY * canvasSize.width + maskX) * 4 + 3; // alpha通道的索引
      
      // 确保索引在有效范围内
      if (index < 0 || !maskDataRef.current || index >= maskDataRef.current.data.length) {
        console.warn('无效的像素索引:', index, '位置:', maskX, maskY, '数据长度:', maskDataRef.current?.data.length || 0);
        return false;
      }
      
      // 由于我们使用了透明蒙板，这里始终返回true允许涂鸦
      return true;
      
      // 以下代码不再使用，因为我们始终允许涂鸦
      // const isInMask = maskDataRef.current.data[index] > 0;
      
      // 在调试模式下记录检测日志
      // if (debugMode && !isInMask) {
      //   console.log('点不在蒙板内:', maskX, maskY, '透明度:', maskDataRef.current.data[index]);
      // }
      
      // return isInMask;
    } catch (error) {
      console.error('检查点是否在蒙板内时出错:', error);
      return false; // 出错时不允许涂鸦，更安全
    }
  };

  // 实现线段与蒙板边界检测，解决高速移动鼠标时跳过边界的问题
  const isLineInMask = (startX: number, startY: number, endX: number, endY: number): boolean => {
    // 如果起点和终点都在蒙板内，则简单返回true
    if (isPointInMask(startX, startY) && isPointInMask(endX, endY)) {
      return true;
    }
    
    // 如果线段较长，采用插值检测多个点
    const dx = endX - startX;
    const dy = endY - startY;
    const distance = Math.sqrt(dx * dx + dy * dy);
    
    // 如果距离很短就直接判断起点
    if (distance < 5) {
      return isPointInMask(startX, startY);
    }
    
    // 对较长的线段进行采样检测
    const steps = Math.max(5, Math.ceil(distance / 5));  // 每5像素检测一个点
    
    for (let i = 0; i <= steps; i++) {
      const t = i / steps;
      const x = startX + dx * t;
      const y = startY + dy * t;
      
      if (!isPointInMask(x, y)) {
        return false;  // 任一点不在蒙板内，则整条线段不允许绘制
      }
    }
    
    return true;
  };

  // 绘制当前笔画
  const drawStrokes = () => {
    if (!maskCanvasRef.current || !imageLoaded) return;
    
    const ctx = maskCanvasRef.current.getContext('2d', { alpha: true });
    if (!ctx) return;
    
    // 清除画布并重新绘制笔画
    ctx.clearRect(0, 0, canvasSize.width, canvasSize.height);
    
    // 如果是调试模式，显示红色半透明蒙板
    if (debugMode && maskDataRef.current) {
      updateDebugMaskDisplay();
    } else {
      // 绘制所有笔画
      drawAllStrokes(ctx);
    }
  };
  
  const drawAllStrokes = (ctx: CanvasRenderingContext2D) => {
    // 绘制所有已保存的笔画
    strokes.forEach(stroke => {
      if (stroke.points.length < 2) return;
      
      ctx.beginPath();
      
      // 考虑缩放因素
      const zoomOffset = {
        x: (canvasSize.width - canvasSize.width * zoomLevel) / 2,
        y: (canvasSize.height - canvasSize.height * zoomLevel) / 2
      };
      
      // 调整第一个点的位置
      const firstPoint = {
        x: (stroke.points[0].x * zoomLevel) + zoomOffset.x,
        y: (stroke.points[0].y * zoomLevel) + zoomOffset.y
      };
      
      ctx.moveTo(firstPoint.x, firstPoint.y);
      
      for (let i = 1; i < stroke.points.length; i++) {
        // 调整后续点的位置
        const point = {
          x: (stroke.points[i].x * zoomLevel) + zoomOffset.x,
          y: (stroke.points[i].y * zoomLevel) + zoomOffset.y
        };
        ctx.lineTo(point.x, point.y);
      }
      
      ctx.strokeStyle = stroke.color;
      ctx.lineWidth = stroke.width * zoomLevel; // 笔画宽度也随缩放变化
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.stroke();
    });
  };

  // 处理鼠标/触摸事件
  const startDrawing = (e: React.MouseEvent | React.TouchEvent) => {
    if (!imageLoaded) return;
    
    const point = getPoint(e);
    
    // 检查起始点是否在蒙板内
    if (!isPointInMask(point.x, point.y)) return;
    
    setIsDrawing(true);
    setCurrentStroke({ points: [point], color: brushColor, width: brushSize });
  };

  const draw = (e: React.MouseEvent | React.TouchEvent) => {
    if (!isDrawing || !imageLoaded) return;
    
    e.preventDefault(); // 阻止默认行为，如触摸时的滚动
    
    const point = getPoint(e);
    
    // 从currentStroke获取上一个点坐标
    if (currentStroke.points.length > 0) {
      const lastPoint = currentStroke.points[currentStroke.points.length - 1];
      
      // 检查线段是否在蒙板内，如果整条线不在蒙板内则不添加该点
      if (!isLineInMask(lastPoint.x, lastPoint.y, point.x, point.y)) {
        return;
      }
    } else {
      // 如果是第一个点，直接检查点是否在蒙板内
      if (!isPointInMask(point.x, point.y)) {
        return;
      }
    }
    
    setCurrentStroke(prevStroke => {
      const newPoints = [...prevStroke.points, point];
      const newStroke = { ...prevStroke, points: newPoints };
      
      // 实时绘制当前笔画
      if (maskCanvasRef.current) {
        const ctx = maskCanvasRef.current.getContext('2d', { alpha: true });
        if (ctx && newPoints.length >= 2) {
          const lastPoint = newPoints[newPoints.length - 2];
          const currentPoint = point;
          
          // 计算缩放偏移
          const zoomOffset = {
            x: (canvasSize.width - canvasSize.width * zoomLevel) / 2,
            y: (canvasSize.height - canvasSize.height * zoomLevel) / 2
          };
          
          // 调整坐标以适应缩放
          const scaledLastPoint = {
            x: (lastPoint.x * zoomLevel) + zoomOffset.x,
            y: (lastPoint.y * zoomLevel) + zoomOffset.y
          };
          
          const scaledCurrentPoint = {
            x: (currentPoint.x * zoomLevel) + zoomOffset.x,
            y: (currentPoint.y * zoomLevel) + zoomOffset.y
          };
          
          ctx.beginPath();
          ctx.moveTo(scaledLastPoint.x, scaledLastPoint.y);
          ctx.lineTo(scaledCurrentPoint.x, scaledCurrentPoint.y);
          ctx.strokeStyle = brushColor;
          ctx.lineWidth = brushSize * zoomLevel; // 宽度也随缩放变化
          ctx.lineCap = 'round';
          ctx.lineJoin = 'round';
          ctx.stroke();
        }
      }
      
      return newStroke;
    });
  };

  const stopDrawing = () => {
    if (!isDrawing || !imageLoaded) return;
    
    setIsDrawing(false);
    if (currentStroke.points.length > 0) {
      // 将当前笔画添加到笔画列表
      setStrokes(prevStrokes => {
        const newStrokes = [...prevStrokes, currentStroke];
        // 添加到历史记录
        setHistory(prev => [...prev, prevStrokes]);
        // 清空重做栈
        setRedoStack([]);
        return newStrokes;
      });
      // 重置当前笔画
      setCurrentStroke({ points: [], color: brushColor, width: brushSize });
    }
  };

  // 获取鼠标或触摸点的坐标，考虑缩放
  const getPoint = (e: React.MouseEvent | React.TouchEvent): Point => {
    if (!maskCanvasRef.current) return { x: 0, y: 0 };
    
    // 获取相对于容器的位置
    const containerRect = containerRef.current?.getBoundingClientRect();
    if (!containerRect) return { x: 0, y: 0 };
    
    let clientX, clientY;
    
    if ('touches' in e) {
      // 触摸事件
      clientX = e.touches[0].clientX;
      clientY = e.touches[0].clientY;
    } else {
      // 鼠标事件
      clientX = e.clientX;
      clientY = e.clientY;
    }
    
    // 计算点击位置相对于缩放容器中心的偏移
    const containerCenterX = containerRect.left + containerRect.width / 2;
    const containerCenterY = containerRect.top + containerRect.height / 2;
    
    // 计算相对于中心的偏移
    const offsetX = clientX - containerCenterX;
    const offsetY = clientY - containerCenterY;
    
    // 转换为画布坐标
    const canvasCenterX = canvasSize.width / 2;
    const canvasCenterY = canvasSize.height / 2;
    
    // 计算实际画布坐标（考虑缩放）
    const x = canvasCenterX + (offsetX / zoomLevel);
    const y = canvasCenterY + (offsetY / zoomLevel);
    
    return { x, y };
  };

  // 撤销上一步操作
  const handleUndo = () => {
    if (history.length === 0) return;
    
    const lastState = history[history.length - 1];
    const newHistory = history.slice(0, -1);
    
    setRedoStack(prev => [...prev, strokes]);
    setStrokes(lastState);
    setHistory(newHistory);
  };

  // 重做上一步操作
  const handleRedo = () => {
    if (redoStack.length === 0) return;
    
    const nextState = redoStack[redoStack.length - 1];
    const newRedoStack = redoStack.slice(0, -1);
    
    setHistory(prev => [...prev, strokes]);
    setStrokes(nextState);
    setRedoStack(newRedoStack);
  };

  // 清空所有笔画
  const handleClear = () => {
    if (strokes.length === 0) return;
    
    setHistory(prev => [...prev, strokes]);
    setStrokes([]);
    setRedoStack([]);
  };

  // 保存编辑结果
  const handleSave = () => {
    if (!maskCanvasRef.current || !onSave) return;
    
    const dataUrl = maskCanvasRef.current.toDataURL('image/png');
    onSave(dataUrl);
  };

  // 切换调试模式
  const toggleDebugMode = () => {
    setDebugMode(!debugMode);
  };

  return (
    <div className="flex flex-col gap-4 w-full border-2 border-yellow-500">
      <div 
        className="relative" 
        ref={containerRef}
        onWheel={handleWheel}
      >
        <div 
          style={{ 
                overflow:'hidden',
            transform: `scale(${zoomLevel})`, 
            transformOrigin: 'center center',
            height: canvasSize.height + 'px', 
            width: canvasSize.width + 'px',
            position: 'relative',
            margin: '0 auto'
          }}
        >
          <canvas
            ref={imageCanvasRef}
            width={canvasSize.width}
            height={canvasSize.height}
            className="absolute top-0 left-0 z-0 border-2 border-blue-500"
          />
          <canvas
            ref={maskCanvasRef}
            width={canvasSize.width}
            height={canvasSize.height}
            className="absolute top-0 left-0 z-10 cursor-crosshair border-2 border-red-500"
            style={{ backgroundColor: 'transparent', overflow: 'hidden' }} // 确保蒙板层透明
            onMouseDown={startDrawing}
            onMouseMove={draw}
            onMouseUp={stopDrawing}
            onMouseLeave={stopDrawing}
            onTouchStart={startDrawing}
            onTouchMove={draw}
            onTouchEnd={stopDrawing}
          />
        </div>
        <div style={{ height: canvasSize.height * zoomLevel + 'px', width: '100%' }}></div>
      </div>
      
      <div className="flex gap-2 justify-center">
        <Button
          variant="outline"
          size="icon"
          onClick={handleZoomOut}
          disabled={zoomLevel <= 0.5}
        >
          <ZoomOut className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="icon"
          onClick={handleZoomIn}
          disabled={zoomLevel >= 3}
        >
          <ZoomIn className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="icon"
          onClick={handleUndo}
          disabled={history.length === 0}
        >
          <Undo className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="icon"
          onClick={handleRedo}
          disabled={redoStack.length === 0}
        >
          <Redo className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="icon"
          onClick={handleClear}
          disabled={strokes.length === 0}
        >
          <X className="h-4 w-4" />
        </Button>
        <Button
          variant={debugMode ? "destructive" : "outline"}
          size="icon"
          onClick={toggleDebugMode}
          title="显示/隐藏蒙板区域"
        >
          <Eye className="h-4 w-4" />
        </Button>
        {onSave && (
          <Button
            variant="default"
            size="icon"
            onClick={handleSave}
          >
            <Save className="h-4 w-4" />
          </Button>
        )}
      </div>
      <div className="text-center text-sm">缩放: {Math.round(zoomLevel * 100)}%</div>
    </div>
  );
};

export default ImageEditor;
