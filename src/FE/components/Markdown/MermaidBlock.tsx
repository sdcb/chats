import { FC, memo, useEffect, useRef, useState, useMemo, useId } from 'react';
import mermaid from 'mermaid';
import { useTheme } from 'next-themes';
// removed unused syntax highlighter imports

import useTranslation from '@/hooks/useTranslation';

import { IconCheck, IconClipboard, IconArrowsDiagonal } from '@/components/Icons/index';
import { MermaidFullscreenDialog } from './MermaidFullscreenDialog';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
// Removed chat status dependency to always attempt rendering
import { CodeBlockCore } from './CodeBlockCore';

// --- Helpers: normalize streaming artifacts and cache rendered SVG ---
const STREAM_CURSOR_REGEX = /[▍]+/g; // common streaming cursor
const ZERO_WIDTH_REGEX = /[\u200B-\u200D\uFEFF]/g; // zero-width chars
const normalizeMermaid = (s: string) => s.replace(STREAM_CURSOR_REGEX, '').replace(ZERO_WIDTH_REGEX, '').trim();

// Cache svg by (theme + normalized code) to survive remounts during streaming
const svgCache = new Map<string, string>();
// Record codes that have been proven stable (per theme) to skip debounce across remounts
const stableKeySet = new Set<string>();

interface Props {
  value: string;
}

export const MermaidBlock: FC<Props> = memo(({ value }) => {
  const { t } = useTranslation();
  const { resolvedTheme } = useTheme();
  const [isCopied, setIsCopied] = useState<boolean>(false);
  // 初始化时若已有缓存，直接用缓存，避免初始回退到代码块导致闪烁
  const initialNormalized = normalizeMermaid(value);
  const initialCacheKey = `${resolvedTheme}|${initialNormalized}`;
  const [svgCode, setSvgCode] = useState<string>(() => svgCache.get(initialCacheKey) ?? '');
  const [error, setError] = useState<string>('');
  const [isFullscreenOpen, setIsFullscreenOpen] = useState<boolean>(false);
  const reactId = useId();
  const idRef = useRef<string>(`mermaid-${reactId.replace(/[:]/g, '')}`);
  const lastRenderedKeyRef = useRef<string | null>(null);
  // 组件挂载标记，用于避免卸载后 setState
  const isMountedRef = useRef<boolean>(true);
  const currentValueRef = useRef<string>(value);
  const lastStableMermaidCodeRef = useRef<string>('');
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // 取消外部聊天状态依赖：随时尝试渲染，失败则回退代码块

  // 根据主题获取Mermaid配置（使用 useMemo 避免重复创建对象）
  const mermaidThemeConfig = useMemo(() => {
    const isDark = resolvedTheme === 'dark';
    
    if (isDark) {
      return {
        theme: 'dark' as const,
        themeVariables: {
          primaryColor: '#374151',
          primaryTextColor: '#f9fafb',
          primaryBorderColor: '#6b7280',
          lineColor: '#9ca3af',
          secondaryColor: '#1f2937',
          tertiaryColor: '#111827',
          background: '#111827',
          backgroundAlt: '#1f2937',
          mainBkg: '#374151',
          secondBkg: '#1f2937',
          tertiaryBkg: '#111827',
          darkMode: true,
          // 流程图
          nodeBkg: '#374151',
          nodeBorder: '#6b7280',
          clusterBkg: '#1f2937',
          clusterBorder: '#6b7280',
          defaultLinkColor: '#9ca3af',
          titleColor: '#f9fafb',
          edgeLabelBackground: '#1f2937',
          // 序列图
          actorBkg: '#374151',
          actorBorder: '#6b7280',
          actorTextColor: '#f9fafb',
          actorLineColor: '#6b7280',
          signalColor: '#f9fafb',
          signalTextColor: '#f9fafb',
          // 甘特图
          gridColor: '#6b7280',
          section0: '#374151',
          section1: '#1f2937',
          section2: '#111827',
          section3: '#065f46',
          // 饼图
          pie1: '#3b82f6',
          pie2: '#10b981',
          pie3: '#f59e0b',
          pie4: '#ef4444',
          pie5: '#8b5cf6',
          pie6: '#ec4899',
          pie7: '#14b8a6',
          pie8: '#f97316',
          pie9: '#84cc16',
          pie10: '#6366f1',
          pie11: '#ef4444',
          pie12: '#8b5cf6',
          pieTitleTextSize: '16px',
          pieTitleTextColor: '#f9fafb',
          pieSectionTextSize: '16px',
          pieSectionTextColor: '#f9fafb',
          pieLegendTextSize: '16px',
          pieLegendTextColor: '#f9fafb',
        },
      };
    } else {
      return {
        theme: 'default' as const,
        themeVariables: {
          primaryColor: '#ffffff',
          primaryTextColor: '#000000',
          primaryBorderColor: '#cccccc',
          lineColor: '#666666',
          secondaryColor: '#f8f8f8',
          tertiaryColor: '#ffffff',
          background: '#ffffff',
          backgroundAlt: '#f8f8f8',
          mainBkg: '#ffffff',
          secondBkg: '#f8f8f8',
          tertiaryBkg: '#ffffff',
          darkMode: false,
          // 流程图
          nodeBkg: '#ffffff',
          nodeBorder: '#cccccc',
          clusterBkg: '#f8f8f8',
          clusterBorder: '#cccccc',
          defaultLinkColor: '#666666',
          titleColor: '#000000',
          edgeLabelBackground: '#ffffff',
          // 序列图
          actorBkg: '#ffffff',
          actorBorder: '#cccccc',
          actorTextColor: '#000000',
          actorLineColor: '#cccccc',
          signalColor: '#000000',
          signalTextColor: '#000000',
          // 甘特图
          gridColor: '#cccccc',
          section0: '#f8f8f8',
          section1: '#ffffff',
          section2: '#f0f0f0',
          section3: '#e8f5e8',
          // 饼图
          pie1: '#3b82f6',
          pie2: '#10b981',
          pie3: '#f59e0b',
          pie4: '#ef4444',
          pie5: '#8b5cf6',
          pie6: '#ec4899',
          pie7: '#14b8a6',
          pie8: '#f97316',
          pie9: '#84cc16',
          pie10: '#6366f1',
          pie11: '#ef4444',
          pie12: '#8b5cf6',
          pieTitleTextSize: '16px',
          pieTitleTextColor: '#000000',
          pieSectionTextSize: '16px',
          pieSectionTextColor: '#000000',
          pieLegendTextSize: '16px',
          pieLegendTextColor: '#000000',
        },
      };
    }
  }, [resolvedTheme]);

  // 只有当 mermaid 代码部分在100ms内没有变化时才触发渲染
  useEffect(() => {
    // 提取并归一化当前 mermaid 代码（去除流式游标、零宽字符等）
    const currentMermaidCode = normalizeMermaid(value);
    currentValueRef.current = value;
    const cacheKey = `${resolvedTheme}|${currentMermaidCode}`;
    
    // 若该代码已被标记为稳定，且有缓存，则直接使用缓存并跳过定时器
    if (stableKeySet.has(cacheKey)) {
      const cached = svgCache.get(cacheKey);
      if (cached && svgCode !== cached) {
        setSvgCode(cached);
        lastRenderedKeyRef.current = cacheKey;
      }
      return;
    }

    // 如果 mermaid 代码和上次稳定的代码相同，则不需要重新设置定时器
    if (currentMermaidCode === lastStableMermaidCodeRef.current) {
      return;
    }
    
    // 清除之前的定时器
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }
    
    // 设置新的定时器
    debounceTimerRef.current = setTimeout(() => {
      // 100ms后检查 mermaid 代码是否仍然和当前代码相同
      const latestMermaidCode = normalizeMermaid(currentValueRef.current);
      
      if (latestMermaidCode === currentMermaidCode && latestMermaidCode !== lastStableMermaidCodeRef.current) {
        lastStableMermaidCodeRef.current = latestMermaidCode;

        const sanitized = latestMermaidCode; // already normalized
        const renderKey = `${resolvedTheme}|${sanitized}`;

        // 优先使用缓存
        const cached = svgCache.get(renderKey);
        if (cached) {
          if (isMountedRef.current) {
            setSvgCode(cached);
            lastRenderedKeyRef.current = renderKey;
          }
          // 命中缓存即视为稳定
          stableKeySet.add(renderKey);
        } else {
          // 避免重复渲染同一内容
          if (lastRenderedKeyRef.current === renderKey && svgCode) {
            // already rendered same content in this theme
          } else {
            // 直接渲染，失败则静默吞掉错误（保持现有 SVG）
            Promise.resolve()
              .then(() => mermaid.render(idRef.current, sanitized))
              .then(({ svg }) => {
                svgCache.set(renderKey, svg);
                stableKeySet.add(renderKey);
                if (isMountedRef.current) {
                  setSvgCode(svg);
                  lastRenderedKeyRef.current = renderKey;
                }
              })
              .catch((err) => {
                // 保留已有 SVG；仅在没有缓存时回退
                if (!svgCache.has(renderKey) && isMountedRef.current) {
                  setSvgCode('');
                }
              });
          }
        }
      } else {
        // code changed during timer; skip render
      }
      debounceTimerRef.current = null;
    }, 100);
    
    return () => {
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
        debounceTimerRef.current = null;
      }
    };
  }, [value, resolvedTheme, svgCode]);

  // 初始化 mermaid 配置（仅在主题变化时）
  useEffect(() => {
    mermaid.initialize({
      suppressErrorRendering: true,
      startOnLoad: false,
      ...mermaidThemeConfig,
      flowchart: {
        useMaxWidth: true,
        htmlLabels: true,
      },
      sequence: {
        useMaxWidth: true,
      },
      journey: {
        useMaxWidth: true,
      },
      timeline: {
        useMaxWidth: true,
      },
      gitGraph: {
        useMaxWidth: true,
      },
      c4: {
        useMaxWidth: true,
      },
    });
  }, [mermaidThemeConfig]);

  // 当有缓存时优先显示缓存，避免因频繁重挂载导致的闪烁
  useEffect(() => {
    const normalized = normalizeMermaid(currentValueRef.current);
    const cacheKey = `${resolvedTheme}|${normalized}`;
    const cached = svgCache.get(cacheKey);
    if (cached) {
      // 如果有缓存并且当前未展示，则立刻展示缓存
      if (svgCode !== cached) {
        setSvgCode(cached);
        lastRenderedKeyRef.current = cacheKey;
        // Using cached mermaid SVG
        // 标记稳定，防止重复触发防抖
        stableKeySet.add(cacheKey);
        // 同步稳定代码引用，减少无意义的定时器
        if (lastStableMermaidCodeRef.current !== normalized) {
          lastStableMermaidCodeRef.current = normalized;
        }
      }
    }
  }, [resolvedTheme, value]);

  // 主题变化时，如果有稳定代码但未命中缓存，则主动渲染并写入缓存
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    const stable = lastStableMermaidCodeRef.current;
    if (!stable) return;
    const sanitized = normalizeMermaid(stable) || stable;
    const renderKey = `${resolvedTheme}|${sanitized}`;
    if (svgCache.has(renderKey)) return;

    Promise.resolve()
      .then(() => mermaid.render(idRef.current, sanitized))
      .then(({ svg }) => {
        svgCache.set(renderKey, svg);
        stableKeySet.add(renderKey);
        if (isMountedRef.current) {
          setSvgCode(svg);
          lastRenderedKeyRef.current = renderKey;
        }
      })
      .catch(() => {
        // ignore theme render failures silently
      });
  }, [resolvedTheme]);

  

  const copyToClipboard = (e: React.MouseEvent) => {
    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      return;
    }

    navigator.clipboard.writeText(value).then(() => {
      setIsCopied(true);

      setTimeout(() => {
        setIsCopied(false);
      }, 2000);
    });
    e.stopPropagation();
  };

  const handleCopy = () => {
    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      return;
    }

    navigator.clipboard.writeText(value).then(() => {
      setIsCopied(true);

      setTimeout(() => {
        setIsCopied(false);
      }, 2000);
    });
  };

  // 只有在没有任何 SVG 可展示时才回退到代码块
  if (!svgCode) {
    // 渲染失败或没有SVG内容：回退到共用的 CodeBlockCore
    return <CodeBlockCore language="mermaid" value={value} />;
  }

  return (
    <>
      <div className="codeblock relative font-sans text-base group">
        <div className="relative bg-white dark:bg-gray-900 rounded-lg overflow-hidden">
          <div className="absolute right-2 top-2 z-10 flex items-center gap-1 opacity-0 pointer-events-none transition-opacity duration-150 group-hover:opacity-100 group-hover:pointer-events-auto">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    className="flex items-center rounded bg-none p-1 text-xs text-muted-foreground hover:bg-muted/60"
                    onClick={copyToClipboard}
                  >
                    {isCopied ? (
                      <IconCheck stroke={'currentColor'} size={16} />
                    ) : (
                      <IconClipboard stroke={'currentColor'} size={16} />
                    )}
                  </button>
                </TooltipTrigger>
                <TooltipContent>
                  {isCopied ? t('Copied') : t('Click Copy')}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    className="flex items-center rounded bg-none p-1 text-xs text-muted-foreground hover:bg-muted/60"
                    onClick={() => setIsFullscreenOpen(true)}
                  >
                    <IconArrowsDiagonal stroke={'currentColor'} size={16} />
                  </button>
                </TooltipTrigger>
                <TooltipContent>
                  {t('Fullscreen')}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>

          <div className="absolute right-2 bottom-2 z-10 text-xs uppercase text-muted-foreground opacity-0 pointer-events-none transition-opacity duration-150 group-hover:opacity-100 group-hover:pointer-events-auto">
            MERMAID
          </div>

          <div
            className="p-4 overflow-x-auto flex justify-center items-center"
            style={{ minHeight: '100px' }}
          >
            <div
              dangerouslySetInnerHTML={{ __html: svgCode }}
              className="mermaid-diagram max-w-full"
            />
          </div>
        </div>
      </div>

      {/* 全屏对话框 */}
      <MermaidFullscreenDialog
        isOpen={isFullscreenOpen}
        onClose={() => setIsFullscreenOpen(false)}
        svgCode={svgCode}
        mermaidCode={value}
        onCopy={handleCopy}
        isCopied={isCopied}
      />
    </>
  );
});

MermaidBlock.displayName = 'MermaidBlock';
