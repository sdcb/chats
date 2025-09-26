import React from 'react';
import { cn } from '@/lib/utils';

interface CollapsiblePanelProps {
  open: boolean;
  children: React.ReactNode;
  className?: string;
  /**
   * 可选：提供一个在折叠时用于回退聚焦的元素（例如 header 按钮）
   * 如果不提供，会尝试自动查找前一个兄弟元素中的第一个可聚焦元素
   */
  focusFallbackRef?: React.RefObject<HTMLElement>;
}

export default function CollapsiblePanel({
  open,
  children,
  className,
  focusFallbackRef,
}: CollapsiblePanelProps) {
  const panelInnerRef = React.useRef<HTMLDivElement>(null);
  // 为了避免浏览器警告：在一个仍然拥有焦点的区域直接加 aria-hidden
  // 我们延迟隐藏：先移走焦点，再设置 hidden state
  const [hiddenState, setHiddenState] = React.useState(!open);

  // 当展开时立即显示（移除 aria-hidden/inert）
  React.useEffect(() => {
    if (open) {
      setHiddenState(false);
    } else {
      // 折叠时：如果内部有焦点，先把焦点转移到回退元素，再隐藏
      const doHide = () => setHiddenState(true);
      const active = document.activeElement as HTMLElement | null;
      if (panelInnerRef.current && active && panelInnerRef.current.contains(active)) {
        let fallback: HTMLElement | null | undefined = focusFallbackRef?.current;
        if (!fallback) {
          // 尝试自动从前一个兄弟节点里找一个可聚焦按钮/链接
            const prev = panelInnerRef.current.parentElement?.previousElementSibling as HTMLElement | null;
            if (prev) {
              fallback = prev.querySelector<HTMLElement>(
                'button, [href], [tabindex]:not([tabindex="-1"])'
              ) || prev;
            }
        }
        if (fallback && typeof fallback.focus === 'function') {
          fallback.focus();
        } else {
          // 兜底：把焦点移到 body，避免仍留在被隐藏区域
          (document.body as HTMLElement).focus?.();
        }
        // 使用 setTimeout 让焦点转移先发生，然后再设置 hiddenState
        setTimeout(doHide, 0);
      } else {
        doHide();
      }
    }
  }, [open, focusFallbackRef]);

  return (
    <div
      className={cn(
        'grid transition-[grid-template-rows] duration-300 ease-in-out',
        open ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]',
        className
      )}
    >
      <div
        ref={panelInnerRef}
        className={"min-h-0 overflow-hidden"}
        // 当 hiddenState 为 true 时：对可访问性与交互完全关闭
        aria-hidden={hiddenState}
        // inert 让其不可聚焦与点击（现代浏览器支持，SSR 下安全）
        {...(hiddenState ? { inert: '' as any } : {})}
      >
        {children}
      </div>
    </div>
  );
}