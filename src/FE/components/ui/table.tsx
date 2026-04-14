import * as React from 'react';
import { ReactElement } from 'react-markdown/lib/react-markdown';

import useTranslation from '@/hooks/useTranslation';

import { Skeleton } from './skeleton';

import { cn } from '@/lib/utils';

const Table = React.forwardRef<
  HTMLTableElement,
  React.HTMLAttributes<HTMLTableElement> & {
    containerClassName?: string;
  }
>(({ className, containerClassName, ...props }, ref) => (
  <div className={cn('relative w-full overflow-auto', containerClassName)}>
    <table
      ref={ref}
      className={cn('w-full caption-bottom text-sm', className)}
      {...props}
    />
  </div>
));
Table.displayName = 'Table';

const TableHeader = React.forwardRef<
  HTMLTableSectionElement,
  React.HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <thead ref={ref} className={cn('[&_tr]:border-b', className)} {...props} />
));
TableHeader.displayName = 'TableHeader';

export interface TableBodyProps
  extends React.InputHTMLAttributes<HTMLTableSectionElement> {
  isLoading?: boolean;
  loadingContent?: ReactElement;
  isEmpty?: boolean;
  emptyContent?: ReactElement;
  emptyText?: ReactElement | string;
}

const TableBody = React.forwardRef<HTMLTableSectionElement, TableBodyProps>(
  (
    {
      className,
      isLoading,
      loadingContent,
      isEmpty,
      emptyContent,
      emptyText,
      ...props
    },
    ref,
  ) => {
    const { t } = useTranslation();
    return isLoading ? (
      <tbody>
        <tr>
          <td className="text-center align-middle p-4 h-32" colSpan={100}>
            {loadingContent || (
              <div className="flex flex-col space-y-3">
                <Skeleton className="h-[125px] w-full rounded-xl" />
                <div className="space-y-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-[90%]" />
                </div>
              </div>
            )}
          </td>
        </tr>
      </tbody>
    ) : isEmpty ? (
      <tbody>
        <tr>
          <td className="text-center align-middle p-4 h-32" colSpan={100}>
            {emptyContent || (
              <div className="flex flex-col space-y-3 text-muted-foreground">
                {emptyText || t('No data')}
              </div>
            )}
          </td>
        </tr>
      </tbody>
    ) : (
      <tbody
        ref={ref}
        className={cn('[&_tr:last-child]:border-0', className)}
        {...props}
      />
    );
  },
);
TableBody.displayName = 'TableBody';

const TableFooter = React.forwardRef<
  HTMLTableSectionElement,
  React.HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <tfoot
    ref={ref}
    className={cn(
      'border-t bg-muted/50 font-medium [&>tr]:last:border-b-0',
      className,
    )}
    {...props}
  />
));
TableFooter.displayName = 'TableFooter';

const TableRow = React.forwardRef<
  HTMLTableRowElement,
  React.HTMLAttributes<HTMLTableRowElement>
>(({ className, ...props }, ref) => (
  <tr
    ref={ref}
    className={cn(
      'border-b transition-colors hover:bg-muted/50 data-[state=selected]:bg-muted',
      className,
    )}
    {...props}
  />
));
TableRow.displayName = 'TableRow';

const TableHead = React.forwardRef<
  HTMLTableCellElement,
  React.ThHTMLAttributes<HTMLTableCellElement>
>(({ className, ...props }, ref) => (
  <th
    ref={ref}
    className={cn(
      'h-12 px-4 text-left align-middle font-medium text-muted-foreground [&:has([role=checkbox])]:pr-0',
      className,
    )}
    {...props}
  />
));
TableHead.displayName = 'TableHead';

const TableCell = React.forwardRef<
  HTMLTableCellElement,
  React.TdHTMLAttributes<HTMLTableCellElement>
>(({ className, ...props }, ref) => (
  <td
    ref={ref}
    className={cn('p-4 align-middle [&:has([role=checkbox])]:pr-0', className)}
    {...props}
  />
));
TableCell.displayName = 'TableCell';

const TableCaption = React.forwardRef<
  HTMLTableCaptionElement,
  React.HTMLAttributes<HTMLTableCaptionElement>
>(({ className, ...props }, ref) => (
  <caption
    ref={ref}
    className={cn('mt-4 text-sm text-muted-foreground', className)}
    {...props}
  />
));
TableCaption.displayName = 'TableCaption';

export {
  Table,
  TableHeader,
  TableBody,
  TableFooter,
  TableHead,
  TableRow,
  TableCell,
  TableCaption,
};
