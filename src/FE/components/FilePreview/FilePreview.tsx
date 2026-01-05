import React from 'react';

import { FileDef, getFileUrl } from '@/types/chat';
import { 
  IconDownload, 
  IconFile, 
  IconFilePdf, 
  IconFileWord, 
  IconFileExcel, 
  IconFilePpt, 
  IconFileZip,
  IconFileText,
  IconCircleX
} from '@/components/Icons';

interface FilePreviewProps {
  file: FileDef | string;  // 支持 FileDef 对象或文件ID字符串
  maxWidth?: number;
  maxHeight?: number;
  className?: string;
  interactive?: boolean; // 是否允许组件内部交互（预览/下载/播放器等）。为 false 时仅展示，不处理点击。
  showDelete?: boolean;  // 是否显示删除按钮
  onDelete?: () => void;  // 删除回调
  onImageClick?: (imageUrl: string, allImages: string[], event: React.MouseEvent<HTMLImageElement>) => void;
}

// 判断是否为图片类型
const isImageType = (contentType: string): boolean => {
  return contentType.startsWith('image/');
};

// 判断是否为视频类型
const isVideoType = (contentType: string): boolean => {
  return contentType.startsWith('video/');
};

// 判断是否为音频类型
const isAudioType = (contentType: string): boolean => {
  return contentType.startsWith('audio/');
};

// 根据 contentType 和 fileName 获取对应的文件图标
const getFileIcon = (contentType: string, fileName: string | null) => {
  // PDF
  if (contentType.includes('pdf') || fileName?.toLowerCase().endsWith('.pdf')) {
    return IconFilePdf;
  }
  
  // Word
  if (contentType.includes('word') || contentType.includes('document') || 
      fileName?.toLowerCase().match(/\.(doc|docx)$/)) {
    return IconFileWord;
  }
  
  // Excel
  if (contentType.includes('excel') || contentType.includes('spreadsheet') ||
      fileName?.toLowerCase().match(/\.(xls|xlsx|csv)$/)) {
    return IconFileExcel;
  }
  
  // PowerPoint
  if (contentType.includes('powerpoint') || contentType.includes('presentation') ||
      fileName?.toLowerCase().match(/\.(ppt|pptx)$/)) {
    return IconFilePpt;
  }
  
  // Zip/压缩文件
  if (contentType.includes('zip') || contentType.includes('compressed') ||
      contentType.includes('rar') || contentType.includes('7z') ||
      fileName?.toLowerCase().match(/\.(zip|rar|7z|tar|gz)$/)) {
    return IconFileZip;
  }
  
  // 文本文件
  if (contentType.includes('text') || 
      fileName?.toLowerCase().match(/\.(txt|md|json|xml|csv)$/)) {
    return IconFileText;
  }
  
  // 默认文件图标
  return IconFile;
};

// 格式化文件大小（如果后端提供）
const formatFileSize = (bytes?: number): string => {
  if (!bytes) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const FilePreview = ({ file, maxWidth = 300, maxHeight = 300, className = '', interactive = true, showDelete = false, onDelete, onImageClick }: FilePreviewProps) => {
  // 处理降级场景：如果传入的是 string，显示为未知文件类型的警告状态
  if (typeof file === 'string') {
    return (
      <div 
        className={`rounded-md border-2 border-red-500 bg-red-50 dark:bg-red-950/20 ${className}`}
        style={{ maxWidth }}
      >
        <div className="flex items-center gap-3 p-3">
          <div className="flex-shrink-0 text-red-500">
            <IconFile size={32} />
          </div>
          <div className="flex-1 min-w-0">
            <div className="text-sm font-medium text-red-700 dark:text-red-400">
              Legacy file reference
            </div>
            <div className="text-xs text-red-600 dark:text-red-500 truncate">
              {file}
            </div>
            <div className="text-xs text-red-600 dark:text-red-500 mt-1">
              ⚠️ Please refactor to FileDef
            </div>
          </div>
        </div>
      </div>
    );
  }

  const fileUrl = getFileUrl(file);
  const { contentType, fileName } = file;

  const canPreviewImage = interactive && !!onImageClick;

  // 图片类型 - 可点击预览
  if (isImageType(contentType)) {
    // 注意：不能同时“固定 height”又“限制 maxWidth=100%”，否则在窄屏下宽度被压缩但高度不变，会导致图片拉伸。
    // 这里改为用 maxHeight 限制最大高度，并让浏览器用 auto 尺寸保持原始比例。
    const imageStyle: React.CSSProperties = interactive
      ? { maxHeight, width: 'auto', height: 'auto', maxWidth: '100%' }
      : { height: '100%', width: '100%', objectFit: 'cover' };

    return (
      <div
        className={`relative ${!interactive ? 'w-full h-full overflow-hidden' : ''} ${className}`}
      >
        <img
          className={`rounded-md transition-opacity ${canPreviewImage ? 'cursor-pointer hover:opacity-90' : ''}`}
          style={imageStyle}
          src={fileUrl}
          alt={fileName || 'Image'}
          onClick={canPreviewImage ? (e) => onImageClick?.(fileUrl, [fileUrl], e) : undefined}
        />
        {showDelete && onDelete && (
          <button
            onClick={(e) => {
              e.stopPropagation();
              onDelete();
            }}
            className="absolute top-[-4px] right-[-4px] z-10"
          >
            <IconCircleX
              className="bg-background rounded-full text-black/50 dark:text-white/50 hover:text-black dark:hover:text-white transition-colors"
              size={20}
            />
          </button>
        )}
      </div>
    );
  }

  // 视频类型 - 使用原生播放器
  if (interactive && isVideoType(contentType)) {
    return (
      <div className={`relative rounded-md overflow-hidden border border-border ${className}`} style={{ maxWidth }}>
        <video
          className="w-full"
          style={{ maxHeight }}
          controls
          preload="metadata"
        >
          <source src={fileUrl} type={contentType} />
          Your browser does not support the video tag.
        </video>
        {fileName && (
          <div className="px-3 py-2 bg-muted text-sm truncate">
            {fileName}
          </div>
        )}
        {showDelete && onDelete && (
          <button
            onClick={(e) => {
              e.stopPropagation();
              onDelete();
            }}
            className="absolute top-1 right-1 z-10"
          >
            <IconCircleX
              className="bg-background rounded-full text-black/50 dark:text-white/50 hover:text-black dark:hover:text-white transition-colors"
              size={20}
            />
          </button>
        )}
      </div>
    );
  }

  // 音频类型 - 使用原生播放器
  if (interactive && isAudioType(contentType)) {
    return (
      <div className={`relative rounded-md overflow-hidden border border-border ${className}`} style={{ maxWidth }}>
        <div className="p-3 bg-muted">
          <div className="flex items-center gap-2 mb-2">
            <IconFile className="text-muted-foreground" size={20} />
            <span className="text-sm font-medium truncate flex-1">
              {fileName || 'Audio file'}
            </span>
          </div>
          <audio
            className="w-full"
            controls
            preload="metadata"
          >
            <source src={fileUrl} type={contentType} />
            Your browser does not support the audio tag.
          </audio>
        </div>
        {showDelete && onDelete && (
          <button
            onClick={(e) => {
              e.stopPropagation();
              onDelete();
            }}
            className="absolute top-1 right-1 z-10"
          >
            <IconCircleX
              className="bg-background rounded-full text-black/50 dark:text-white/50 hover:text-black dark:hover:text-white transition-colors"
              size={20}
            />
          </button>
        )}
      </div>
    );
  }

  // 其他文件类型 - 显示文件卡片
  const fileIconComponent = getFileIcon(contentType, fileName);
  
  return (
    <div 
      className={`relative rounded-md border border-border hover:border-primary transition-colors ${className}`}
      style={{ maxWidth }}
    >
      {interactive ? (
        <a
          href={fileUrl}
          download={fileName || undefined}
          target="_blank"
          rel="noopener noreferrer"
          className="flex items-center hover:bg-muted/50 transition-colors"
        >
          <div className="flex-shrink-0">
            {React.createElement(fileIconComponent, { size: 32 })}
          </div>
          <div className="flex-1 min-w-0">
            <div className="text-sm font-medium truncate">
              {fileName || 'Unknown file'}
            </div>
          </div>
          <div className="flex-shrink-0 text-muted-foreground hover:text-primary">
            <IconDownload size={20} />
          </div>
        </a>
      ) : (
        <div className="flex items-center">
          <div className="flex-shrink-0">
            {React.createElement(fileIconComponent, { size: 32 })}
          </div>
          <div className="flex-1 min-w-0">
            <div className="text-sm font-medium truncate">
              {fileName || 'Unknown file'}
            </div>
          </div>
        </div>
      )}
      {showDelete && onDelete && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            onDelete();
          }}
          className="absolute top-1 right-1 z-10"
        >
          <IconCircleX
            className="bg-background rounded-full text-black/50 dark:text-white/50 hover:text-black dark:hover:text-white transition-colors"
            size={20}
          />
        </button>
      )}
    </div>
  );
};

export default FilePreview;
