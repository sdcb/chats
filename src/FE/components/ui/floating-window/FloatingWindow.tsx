import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { cn } from '@/lib/utils';
import { useIsMobile } from '@/hooks/useMobile';
import { IconArrowsDiagonal, IconArrowsDiagonalMinimize, IconX } from '@/components/Icons';
import { Button } from '@/components/ui/button';

type Rect = { x: number; y: number; w: number; h: number };
type ResizeDir =
  | 'n'
  | 's'
  | 'e'
  | 'w'
  | 'ne'
  | 'nw'
  | 'se'
  | 'sw';

export type FloatingWindowProps = {
  open: boolean;
  title: React.ReactNode;
  onOpenChange: (open: boolean) => void;
  children: React.ReactNode;
  className?: string;
  defaultSize?: { width: number; height: number };
  minSize?: { width: number; height: number };
};

export default function FloatingWindow({
  open,
  title,
  onOpenChange,
  children,
  className,
  defaultSize = { width: 920, height: 680 },
  minSize = { width: 520, height: 420 },
}: FloatingWindowProps) {
  const isMobile = useIsMobile();
  const frameRef = useRef<HTMLDivElement | null>(null);
  const [isMaximized, setIsMaximized] = useState<boolean>(false);

  const clampRect = useCallback(
    (r: Rect) => {
      const vw = window.innerWidth;
      const vh = window.innerHeight;
      const w = Math.max(minSize.width, Math.min(r.w, vw));
      const h = Math.max(minSize.height, Math.min(r.h, vh));
      const x = Math.max(0, Math.min(r.x, vw - w));
      const y = Math.max(0, Math.min(r.y, vh - h));
      return { x, y, w, h };
    },
    [minSize.height, minSize.width],
  );

  const initialRect = useMemo<Rect>(() => {
    const vw = typeof window !== 'undefined' ? window.innerWidth : 1200;
    const vh = typeof window !== 'undefined' ? window.innerHeight : 800;
    const w = Math.min(defaultSize.width, vw);
    const h = Math.min(defaultSize.height, vh);
    const x = Math.max(0, Math.round((vw - w) / 2));
    const y = Math.max(0, Math.round((vh - h) / 2));
    return { x, y, w, h };
  }, [defaultSize.height, defaultSize.width]);

  const [rect, setRect] = useState<Rect>(initialRect);

  useEffect(() => {
    if (!open) return;
    setRect((r) => clampRect(r));
  }, [clampRect, open]);

  useEffect(() => {
    if (!open) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onOpenChange(false);
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onOpenChange, open]);

  useEffect(() => {
    if (!open) return;
    if (isMobile) {
      setIsMaximized(true);
    }
  }, [isMobile, open]);

  const startDrag = useCallback(
    (e: React.PointerEvent) => {
      if (isMobile || isMaximized) return;
      if (e.button !== 0) return;
      e.preventDefault();

      const start = { x: e.clientX, y: e.clientY };
      const startRect = rect;

      const onMove = (ev: PointerEvent) => {
        const dx = ev.clientX - start.x;
        const dy = ev.clientY - start.y;
        setRect((r) => clampRect({ ...r, x: startRect.x + dx, y: startRect.y + dy }));
      };

      const onUp = () => {
        window.removeEventListener('pointermove', onMove);
        window.removeEventListener('pointerup', onUp);
      };

      window.addEventListener('pointermove', onMove);
      window.addEventListener('pointerup', onUp);
    },
    [clampRect, isMaximized, isMobile, rect],
  );

  const startResize = useCallback(
    (dir: ResizeDir) => (e: React.PointerEvent) => {
      if (isMobile || isMaximized) return;
      if (e.button !== 0) return;
      e.preventDefault();
      e.stopPropagation();

      const start = { x: e.clientX, y: e.clientY };
      const startRect = rect;

      const onMove = (ev: PointerEvent) => {
        const dx = ev.clientX - start.x;
        const dy = ev.clientY - start.y;

        let next: Rect = { ...startRect };
        if (dir.includes('e')) next.w = startRect.w + dx;
        if (dir.includes('s')) next.h = startRect.h + dy;
        if (dir.includes('w')) {
          next.x = startRect.x + dx;
          next.w = startRect.w - dx;
        }
        if (dir.includes('n')) {
          next.y = startRect.y + dy;
          next.h = startRect.h - dy;
        }

        setRect(clampRect(next));
      };

      const onUp = () => {
        window.removeEventListener('pointermove', onMove);
        window.removeEventListener('pointerup', onUp);
      };

      window.addEventListener('pointermove', onMove);
      window.addEventListener('pointerup', onUp);
    },
    [clampRect, isMaximized, isMobile, rect],
  );

  if (!open) return null;

  const maximizedStyle = isMaximized
    ? { left: 0, top: 0, width: '100vw', height: '100vh' }
    : { left: rect.x, top: rect.y, width: rect.w, height: rect.h };

  return (
    <div className="fixed inset-0 z-50">
      <div
        className="absolute inset-0 bg-black/40"
        onMouseDown={() => onOpenChange(false)}
      />

      <div
        ref={frameRef}
        className={cn(
          'absolute bg-background border shadow-xl rounded-md overflow-hidden flex flex-col',
          isMaximized && 'rounded-none',
          className,
        )}
        style={maximizedStyle as any}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div
          className={cn(
            'flex items-center justify-between gap-2 px-3 h-10 border-b bg-muted/40 select-none',
            !isMobile && !isMaximized && 'cursor-move',
          )}
          onPointerDown={startDrag}
        >
          <div className="truncate text-sm font-medium">{title}</div>
          <div className="flex items-center gap-1">
            {!isMobile && (
              <Button
                variant="ghost"
                size="sm"
                className="h-7 w-7 p-0"
                onClick={() => setIsMaximized((v) => !v)}
                title={isMaximized ? 'Restore' : 'Maximize'}
              >
                {isMaximized ? (
                  <IconArrowsDiagonalMinimize size={16} />
                ) : (
                  <IconArrowsDiagonal size={16} />
                )}
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={() => onOpenChange(false)}
              title="Close"
            >
              <IconX size={16} />
            </Button>
          </div>
        </div>

        <div className="flex-1 overflow-auto">{children}</div>

        {!isMobile && !isMaximized && (
          <>
            <div
              className="absolute top-0 left-0 right-0 h-1 cursor-n-resize"
              onPointerDown={startResize('n')}
            />
            <div
              className="absolute bottom-0 left-0 right-0 h-1 cursor-s-resize"
              onPointerDown={startResize('s')}
            />
            <div
              className="absolute top-0 bottom-0 left-0 w-1 cursor-w-resize"
              onPointerDown={startResize('w')}
            />
            <div
              className="absolute top-0 bottom-0 right-0 w-1 cursor-e-resize"
              onPointerDown={startResize('e')}
            />
            <div
              className="absolute top-0 left-0 w-3 h-3 cursor-nw-resize"
              onPointerDown={startResize('nw')}
            />
            <div
              className="absolute top-0 right-0 w-3 h-3 cursor-ne-resize"
              onPointerDown={startResize('ne')}
            />
            <div
              className="absolute bottom-0 left-0 w-3 h-3 cursor-sw-resize"
              onPointerDown={startResize('sw')}
            />
            <div
              className="absolute bottom-0 right-0 w-3 h-3 cursor-se-resize"
              onPointerDown={startResize('se')}
            />
          </>
        )}
      </div>
    </div>
  );
}

