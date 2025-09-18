import { useEffect, useState } from 'react';

import { FileDef, getFileUrl } from '@/types/chat';
import { GetUserFilesResult } from '@/types/clientApis';

import { IconFolder } from '@/components/Icons';
import PaginationContainer from '@/components/Pagiation/Pagiation';
import { Button } from '@/components/ui/button';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';

import { getUserFiles } from '@/apis/clientApis';
import { cn } from '@/lib/utils';
import { ImageLoader } from '@/components/ImageLoader/imageLoader';

interface FilesPopoverProps {
  selectedFiles?: FileDef[];
  onSelect?: (file: GetUserFilesResult) => void;
}

const FilesPopover = ({ onSelect, selectedFiles }: FilesPopoverProps) => {
  const [files, setFiles] = useState<GetUserFilesResult[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 9,
  });
  useEffect(() => {
    getUserFiles({
      page: pagination.page,
      pageSize: pagination.pageSize,
    }).then((res) => {
      setFiles(res.rows);
      setTotalCount(res.count);
    });
  }, [pagination.page, pagination.pageSize]);

  const handlePageChange = (page: number) => {
    setPagination({ ...pagination, page });
  };

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button className="rounded-sm p-1 m-1 h-auto w-auto bg-transparent hover:bg-muted">
          <IconFolder size={22} />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="min-w-80 max-w-lg">
        <div className="grid grid-cols-3 gap-2">
          {files.map((file) => (
            <div
              key={file.id}
              className="aspect-square cursor-pointer"
              onClick={() => onSelect?.(file)}
            >
              <ImageLoader
                src={getFileUrl(file.id)}
                alt={file.fileName}
                className={cn(
                  'w-full h-full object-cover rounded-md border-2 border-transparent',
                  selectedFiles?.some((f) => f.id === file.id)
                    ? 'border-black'
                    : '',
                )}
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
