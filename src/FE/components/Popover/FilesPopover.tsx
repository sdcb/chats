import { useEffect, useState } from 'react';

import { FileDef } from '@/types/chat';
import { GetUserFilesResult } from '@/types/clientApis';

import PaginationContainer from '@/components/Pagination/Pagination';
import FilePreview from '@/components/FilePreview/FilePreview';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

import { getUserFiles } from '@/apis/clientApis';
import { cn } from '@/lib/utils';

interface FilesPopoverProps {
  selectedFiles?: FileDef[];
  onSelect?: (file: GetUserFilesResult) => void;
  trigger: React.ReactNode;
}

const FilesPopover = ({
  onSelect,
  selectedFiles,
  trigger,
}: FilesPopoverProps) => {
  const [files, setFiles] = useState<GetUserFilesResult[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 9,
  });
  const [isOpen, setIsOpen] = useState(false);

  // 只在 popover 打开时加载数据
  useEffect(() => {
    if (!isOpen) return;
    
    getUserFiles({
      page: pagination.page,
      pageSize: pagination.pageSize,
    }).then((res) => {
      setFiles(res.rows);
      setTotalCount(res.count);
    });
  }, [isOpen, pagination.page, pagination.pageSize]);

  const handlePageChange = (page: number) => {
    setPagination({ ...pagination, page });
  };

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>
        {trigger}
      </PopoverTrigger>
      <PopoverContent className="min-w-80 max-w-lg">
        <div className="grid grid-cols-3 gap-2 justify-items-center">
          {files.map((file) => (
            <div
              key={file.id}
              className={cn(
                'cursor-pointer rounded-md border-2 border-transparent p-1 w-full aspect-square max-w-[300px] max-h-[300px] overflow-hidden',
                selectedFiles?.some((f) => f.id === file.id) ? 'border-black' : '',
              )}
              onClick={() => onSelect?.(file)}
            >
              <FilePreview
                file={file}
                interactive={false}
                className="w-full h-full"
                maxWidth={300}
                maxHeight={300}
              />
            </div>
          ))}
        </div>
        {totalCount > 0 && (
          <div className="mt-2 flex justify-center items-center">
            <div>
              <PaginationContainer
                showPageNumbers={false}
                showTotalCount={false}
                page={pagination.page}
                pageSize={pagination.pageSize}
                currentCount={files.length}
                totalCount={totalCount}
                onPagingChange={(page: number, pageSize: number) => {
                  setPagination({ page, pageSize });
                  handlePageChange(page);
                }}
              />
            </div>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
};

export default FilesPopover;
