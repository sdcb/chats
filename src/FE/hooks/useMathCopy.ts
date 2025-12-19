import { useEffect, useRef } from 'react';

// 全局标记，确保只注册一次事件监听器
let globalListenerRegistered = false;
let listenerCount = 0;

/**
 * Hook: 监听复制事件，将选中的数学公式替换为原始 LaTeX 代码
 * 配合 rehypeKatexDataMath 插件使用
 * 
 * 原理：当用户复制包含数学公式的内容时，检测选区内的 [data-math] 元素，
 * 将渲染后的 KaTeX HTML 替换为原始 LaTeX 代码
 * 
 * 性能说明：
 * - 全局只注册一个 copy 事件监听器（多个组件共享）
 * - 仅在用户复制时执行，不影响正常渲染性能
 */
export function useMathCopy() {
  const registeredRef = useRef(false);

  useEffect(() => {
    // 避免同一组件重复注册
    if (registeredRef.current) return;
    registeredRef.current = true;
    listenerCount++;

    // 只在首次调用时注册全局监听器
    if (!globalListenerRegistered) {
      globalListenerRegistered = true;
      document.addEventListener('copy', handleCopy);
    }

    return () => {
      registeredRef.current = false;
      listenerCount--;
      
      // 当没有组件使用时，移除监听器
      if (listenerCount === 0 && globalListenerRegistered) {
        globalListenerRegistered = false;
        document.removeEventListener('copy', handleCopy);
      }
    };
  }, []);
}

function handleCopy(e: ClipboardEvent) {
  const selection = window.getSelection();
  if (!selection || selection.isCollapsed) return;

  // 获取选区范围
  const range = selection.getRangeAt(0);
  
  // 克隆选区内容
  const clonedContent = range.cloneContents();
  
  // 查找所有带 data-math 的元素
  const mathElements = clonedContent.querySelectorAll('[data-math]');
  
  if (mathElements.length === 0) {
    // 没有数学公式，使用默认复制行为
    return;
  }

  // 替换数学公式为原始 LaTeX
  mathElements.forEach((el) => {
    const mathText = el.getAttribute('data-math');
    if (mathText) {
      const textNode = document.createTextNode(mathText);
      el.replaceWith(textNode);
    }
  });

  // 创建临时容器获取处理后的文本
  const tempDiv = document.createElement('div');
  tempDiv.appendChild(clonedContent);
  
  // 获取纯文本（保留换行）
  const plainText = getTextWithNewlines(tempDiv);
  
  // 获取 HTML（用于富文本粘贴）
  const htmlText = tempDiv.innerHTML;

  // 阻止默认复制，写入处理后的内容
  e.preventDefault();
  e.clipboardData?.setData('text/plain', plainText);
  e.clipboardData?.setData('text/html', htmlText);
}

/**
 * 从 DOM 元素获取文本，保留适当的换行
 */
function getTextWithNewlines(element: HTMLElement): string {
  let text = '';
  
  const walk = (node: Node) => {
    if (node.nodeType === Node.TEXT_NODE) {
      text += node.textContent;
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      const el = node as HTMLElement;
      const tagName = el.tagName.toLowerCase();
      
      // 块级元素前后添加换行
      const blockElements = ['div', 'p', 'br', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'li', 'tr'];
      const isBlock = blockElements.includes(tagName);
      
      if (isBlock && text.length > 0 && !text.endsWith('\n')) {
        text += '\n';
      }
      
      if (tagName === 'br') {
        text += '\n';
      } else {
        el.childNodes.forEach(walk);
      }
      
      // math-block 后面加换行（块级公式）
      if (el.classList?.contains('math-block')) {
        if (!text.endsWith('\n')) {
          text += '\n';
        }
      }
      
      if (isBlock && !text.endsWith('\n')) {
        text += '\n';
      }
    }
  };
  
  walk(element);
  
  // 清理多余的换行
  return text.replace(/\n{3,}/g, '\n\n').trim();
}

export default useMathCopy;
