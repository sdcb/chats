import { useEffect, useMemo, useRef, useState } from 'react';

import { cn } from '@/lib/utils';
import useTranslation from '@/hooks/useTranslation';
import { IconChevronDown } from '@/components/Icons';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

type Props = {
  value: string;
  onChange: (value: string) => void;
  options: string[];
  placeholder?: string;
  disabled?: boolean;
};

export default function ImageComboBox({
  value,
  onChange,
  options,
  placeholder,
  disabled,
}: Props) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => {
      if (!wrapRef.current) return;
      if (wrapRef.current.contains(e.target as Node)) return;
      setOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  const filtered = useMemo(() => {
    const q = value.trim().toLowerCase();
    if (!q) return options;
    return options.filter((x) => x.toLowerCase().includes(q));
  }, [options, value]);

  return (
    <div ref={wrapRef} className="relative">
      <div className="flex">
        <Input
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          disabled={disabled}
          className="rounded-r-none"
        />
        <Button
          type="button"
          variant="secondary"
          className="rounded-l-none border-l-0 px-2"
          disabled={disabled}
          onClick={() => setOpen((v) => !v)}
          title={t('Select image')}
        >
          <IconChevronDown size={14} />
        </Button>
      </div>

      {open && (
        <div
          className={cn(
            'absolute z-20 mt-1 w-full rounded-md border bg-background shadow-md',
            'max-h-56 overflow-auto',
          )}
        >
          {filtered.length === 0 ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">
              {t('No matching images')}
            </div>
          ) : (
            filtered.slice(0, 200).map((x) => (
              <button
                key={x}
                type="button"
                className="w-full text-left px-3 py-2 text-sm hover:bg-accent"
                onClick={() => {
                  onChange(x);
                  setOpen(false);
                }}
                title={x}
              >
                {x}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
