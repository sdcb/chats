import type { Root, Element } from 'hast';
import type { Plugin } from 'unified';

/**
 * rehype 插件：在 KaTeX 渲染后，给数学公式元素添加 data-math 属性
 * 这样用户复制时可以通过 copy 事件监听器获取原始 LaTeX 代码
 * （类似 Google Gemini 的实现方式）
 * 
 * 使用方法：放在 rehypeKatex 之后
 * rehypePlugins={[rehypeKatex, rehypeKatexDataMath]}
 */
export const rehypeKatexDataMath: Plugin<[], Root> = function () {
  return function (tree: Root) {
    processNode(tree);
  };
};

function processNode(node: any, parent?: any, index?: number) {
  if (node.type === 'element' && node.properties) {
    const className = node.properties.className;
    
    // 块级公式：katex-display
    if (Array.isArray(className) && className.includes('katex-display')) {
      const annotation = findAnnotation(node);
      if (annotation && parent && typeof index === 'number') {
        // 创建包装 div，添加 data-math 属性
        const wrapper: Element = {
          type: 'element',
          tagName: 'div',
          properties: {
            className: ['math-block'],
            'data-math': `$$${annotation}$$`,
          },
          children: [node],
        };
        parent.children[index] = wrapper;
        return; // 已处理，不再递归
      }
    }
    
    // 行内公式：span.katex（但不在 katex-display 内）
    if (
      node.tagName === 'span' &&
      Array.isArray(className) &&
      className.includes('katex') &&
      !className.includes('katex-display')
    ) {
      // 检查父节点是否是 katex-display（避免重复处理块级公式内部的 span.katex）
      const parentClassName = parent?.properties?.className;
      if (Array.isArray(parentClassName) && parentClassName.includes('katex-display')) {
        return;
      }
      
      const annotation = findAnnotation(node);
      if (annotation && parent && typeof index === 'number') {
        // 创建包装 span，添加 data-math 属性
        const wrapper: Element = {
          type: 'element',
          tagName: 'span',
          properties: {
            className: ['math-inline'],
            'data-math': `$${annotation}$`,
          },
          children: [node],
        };
        parent.children[index] = wrapper;
        return;
      }
    }
  }
  
  // 递归处理子节点
  if (node.children) {
    const children = [...node.children];
    children.forEach((child: any, i: number) => {
      processNode(child, node, i);
    });
  }
}

/**
 * 从节点中提取纯文本
 */
function extractText(node: any): string {
  if (node.type === 'text') {
    return node.value || '';
  }
  if (node.children) {
    return node.children.map(extractText).join('');
  }
  return '';
}

/**
 * 在 KaTeX 渲染的节点中查找 annotation 元素
 * KaTeX 会在 MathML semantics 中添加 <annotation encoding="application/x-tex">
 */
function findAnnotation(node: any): string | null {
  if (node.type === 'element' && node.tagName === 'annotation') {
    const encoding = node.properties?.encoding;
    if (encoding === 'application/x-tex') {
      return extractText(node);
    }
  }
  
  if (node.children) {
    for (const child of node.children) {
      const result = findAnnotation(child);
      if (result) return result;
    }
  }
  
  return null;
}

export default rehypeKatexDataMath;
