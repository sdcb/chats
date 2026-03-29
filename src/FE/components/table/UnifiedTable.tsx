import { ReactNode } from 'react';

import { IconColumns } from '@/components/Icons';
import PaginationContainer from '@/components/Pagination/Pagination';
import Tips from '@/components/Tips/Tips';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import useTranslation from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

export const UNIFIED_TABLE_PAGE_SIZE = 20;
export const COLUMN_QUERY_SEPARATOR = '~';

export interface UnifiedTableColumn<T, TColumnKey extends string = string> {
  key: TColumnKey;
  title: string;
  className?: string;
  cell: (row: T) => ReactNode;
}

export interface UnifiedTableAction {
  key: string;
  element: ReactNode;
  desktopOnly?: boolean;
}

export interface UnifiedTableQueryState<TFilters, TColumnKey extends string = string> {
  page: number;
  filters: TFilters;
  columns: TColumnKey[];
}

export const getFirstQueryValue = (value: string | string[] | undefined) =>
  Array.isArray(value) ? value[0] : value;

export const parseQueryPage = (value: string | undefined, fallback = 1) => {
  const parsed = parseInt(value || String(fallback), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
};

export const parseColumnQuery = <TColumnKey extends string>(
  value: string | undefined,
  allColumns: Array<{ key: TColumnKey }>,
  defaultColumns: TColumnKey[],
) => {
  if (!value) {
    return defaultColumns;
  }

  const keys = value
    .split(COLUMN_QUERY_SEPARATOR)
    .map((item) => item.trim())
    .filter(
      (item): item is TColumnKey =>
        allColumns.some((column) => column.key === item),
    );

  return keys.length > 0 ? keys : defaultColumns;
};

export const buildColumnQuery = <TColumnKey extends string>(
  columns: TColumnKey[],
  defaultColumns: TColumnKey[],
) => {
  if (columns.join(',') === defaultColumns.join(',')) {
    return undefined;
  }

  return columns.join(COLUMN_QUERY_SEPARATOR);
};

type UnifiedColumnSelectorProps<TColumnKey extends string> = {
  allColumns: Array<{ key: TColumnKey; title: string }>;
  selectedColumns: TColumnKey[];
  onToggleColumn: (key: TColumnKey, checked: boolean) => void;
};

export const UnifiedColumnSelector = <TColumnKey extends string,>({
  allColumns,
  selectedColumns,
  onToggleColumn,
}: UnifiedColumnSelectorProps<TColumnKey>) => {
  const { t } = useTranslation();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <div className="hidden sm:block">
          <Tips
            trigger={
              <Button
                variant="ghost"
                size="icon"
                className="h-9 w-9"
                aria-label={t('Select columns')}
                title={t('Select columns')}
              >
                <IconColumns size={18} />
              </Button>
            }
            side="bottom"
            content={t('Select columns')}
          />
        </div>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="flex items-center gap-2">
          <IconColumns size={16} />
          <span>{t('Select columns')}</span>
        </DropdownMenuLabel>
        {allColumns.map((column) => (
          <DropdownMenuCheckboxItem
            key={column.key}
            checked={selectedColumns.includes(column.key)}
            onSelect={(event) => event.preventDefault()}
            onCheckedChange={(checked) => onToggleColumn(column.key, !!checked)}
          >
            {t(column.title)}
          </DropdownMenuCheckboxItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
};

type UnifiedTableProps<T, TColumnKey extends string> = {
  filters: ReactNode;
  actions?: UnifiedTableAction[];
  columns: Array<UnifiedTableColumn<T, TColumnKey>>;
  rows: T[];
  loading: boolean;
  page: number;
  totalCount: number;
  rowKey: (row: T) => string | number;
  onPageChange: (page: number) => void;
  mobileContent?: ReactNode;
  footer?: ReactNode;
  rowClassName?: (row: T) => string | undefined;
  onRowClick?: (row: T) => void;
  emptyText?: ReactNode;
  tableCardClassName?: string;
};

export const UnifiedTable = <T, TColumnKey extends string>({
  filters,
  actions = [],
  columns,
  rows,
  loading,
  page,
  totalCount,
  rowKey,
  onPageChange,
  mobileContent,
  footer,
  rowClassName,
  onRowClick,
  emptyText,
  tableCardClassName,
}: UnifiedTableProps<T, TColumnKey>) => {
  return (
    <div className="space-y-4">
      <Card className="border-none p-3">
        <div className="flex flex-wrap items-end gap-3">
          {filters}
          {actions.length > 0 && (
            <div className="ml-auto flex items-center gap-2 self-end">
              {actions.map((action) => (
                <div
                  key={action.key}
                  className={cn(action.desktopOnly && 'hidden sm:block')}
                >
                  {action.element}
                </div>
              ))}
            </div>
          )}
        </div>
      </Card>

      {mobileContent && <div className="block sm:hidden">{mobileContent}</div>}

      <div className={cn(mobileContent && 'hidden sm:block')}>
        <Card className={cn('overflow-x-auto border-none', tableCardClassName)}>
          <Table>
            <TableHeader>
              <TableRow>
                {columns.map((column) => (
                  <TableHead
                    key={column.key}
                    className={cn(
                      'px-1 py-1 text-foreground first:pl-4 last:pr-4',
                      column.className,
                    )}
                  >
                    {column.title}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody
              isLoading={loading}
              isEmpty={rows.length === 0}
              emptyContent={
                emptyText === undefined ? undefined : (
                  <div className="flex flex-col space-y-3 text-muted-foreground">
                    {emptyText}
                  </div>
                )
              }
            >
              {rows.map((row) => (
                <TableRow
                  key={rowKey(row)}
                  className={cn(
                    onRowClick && 'cursor-pointer',
                    rowClassName?.(row),
                  )}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                >
                  {columns.map((column) => (
                    <TableCell
                      key={column.key}
                      className={cn(
                        'px-1 py-1 text-muted-foreground first:pl-4 last:pr-4',
                        column.className,
                      )}
                    >
                      {column.cell(row)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
            {footer}
          </Table>

          {rows.length > 0 && (
            <PaginationContainer
              page={page}
              pageSize={UNIFIED_TABLE_PAGE_SIZE}
              currentCount={rows.length}
              totalCount={totalCount}
              onPagingChange={(nextPage) => onPageChange(nextPage)}
            />
          )}
        </Card>
      </div>
    </div>
  );
};
