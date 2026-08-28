import { ReactNode } from 'react';

type Props = {
  label: string;
  children: ReactNode;
  mono?: boolean;
  className?: string;
};

export default function CatalogCardField({
  label,
  children,
  mono = false,
  className,
}: Props) {
  return (
    <div className={className}>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className={mono ? 'mt-1 break-all font-mono text-xs' : 'mt-1'}>
        {children}
      </dd>
    </div>
  );
}
