# FilePreview 组件

## 概述
`FilePreview` 是一个通用的文件预览组件，支持多种文件格式的智能渲染。

## 支持的文件类型

### 1. 图片文件 (image/*)
- **渲染方式**: 直接显示图片，支持点击预览放大
- **交互**: 点击可在 `ImagePreview` 组件中查看大图
- **示例**: JPG, PNG, GIF, WebP, SVG

### 2. 视频文件 (video/*)
- **渲染方式**: 使用原生 HTML5 video 播放器
- **特性**: 
  - 支持播放控制（播放/暂停/进度条/音量）
  - 预加载元数据
  - 显示文件名
- **示例**: MP4, WebM, OGG

### 3. 音频文件 (audio/*)
- **渲染方式**: 使用原生 HTML5 audio 播放器
- **特性**:
  - 显示文件图标和文件名
  - 完整的播放控制
  - 预加载元数据
- **示例**: MP3, WAV, OGG, M4A

### 4. 其他文件类型
- **渲染方式**: 文件卡片，显示文件信息
- **特性**:
  - 根据文件类型显示对应的图标
  - 显示文件名
  - 支持点击下载或在新标签页打开
- **支持的图标类型**:
  - 📕 PDF 文件 (IconFilePdf)
  - 📘 Word 文档 (IconFileWord) - .doc, .docx
  - 📗 Excel 表格 (IconFileExcel) - .xls, .xlsx, .csv
  - 📙 PowerPoint 演示文稿 (IconFilePpt) - .ppt, .pptx
  - 📦 压缩文件 (IconFileZip) - .zip, .rar, .7z, .tar, .gz
  - 📄 文本文件 (IconFileText) - .txt, .md, .json, .xml
  - 📋 通用文件 (IconFile) - 其他类型

## 使用方式

```tsx
import FilePreview from '@/components/FilePreview';

// 在组件中使用
<FilePreview
  file={fileDef}
  maxWidth={300}
  maxHeight={300}
  onImageClick={handleImageClick}
  className="custom-class"
/>
```

## Props

- `file: FileDef` - 必需，文件定义对象
- `maxWidth?: number` - 可选，最大宽度（默认 300px）
- `maxHeight?: number` - 可选，最大高度（默认 300px）
- `className?: string` - 可选，自定义 CSS 类
- `onImageClick?: (imageUrl, allImages, event) => void` - 可选，图片点击处理函数

## FileDef 接口

```typescript
interface FileDef {
  id: string;           // 文件 ID
  contentType: string;  // MIME 类型，如 "image/png", "video/mp4"
  fileName: string | null;  // 文件名
  url: string | null;   // 文件 URL（可选）
}
```

## 集成位置

该组件已集成到以下位置：

1. **UserMessage.tsx** - 用户消息中的文件显示
2. **ResponseMessage.tsx** - AI 响应消息中的文件显示

## 优势

1. **统一的文件处理**: 一个组件处理所有文件类型
2. **智能渲染**: 根据 contentType 和文件名自动选择最佳渲染方式
3. **语义化图标**: 使用专用图标清晰表达文件类型，无需颜色区分
4. **良好的用户体验**: 
   - 图片可预览放大
   - 视频/音频可直接播放
   - 其他文件显示友好的下载界面
   - 简洁的文件名显示，无下划线干扰
5. **可扩展性**: 易于添加新的文件类型图标支持
6. **一致的样式**: 统一的圆角、边框、悬停效果
7. **无障碍友好**: 语义化的 SVG 图标，主题色自适应

## 未来改进方向

- [ ] 支持 PDF 内嵌预览
- [ ] 支持代码文件语法高亮预览
- [ ] 支持 Office 文档预览
- [ ] 添加文件大小显示
- [ ] 添加下载进度指示
