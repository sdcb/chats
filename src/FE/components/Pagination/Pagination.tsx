import useTranslation from '@/hooks/useTranslation';

import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
  PaginationLink,
  PaginationEllipsis,
} from '@/components/ui/pagination';

const PaginationContainer = ({
  page,
  pageSize,
  currentCount,
  totalCount,
  onPagingChange,
  showPageNumbers = true,
  showTotalCount = true,
}: {
  page: number;
  pageSize: number;
  currentCount: number;
  totalCount: number;
  onPagingChange: (page: number, pageSize: number) => void;
  showPageNumbers?: boolean;
  showTotalCount?: boolean;
}) => {
  const { t } = useTranslation();
  
  function previous() {
    onPagingChange(page - 1, pageSize);
  }

  function next() {
    onPagingChange(page + 1, pageSize);
  }

  function goToPage(pageNumber: number) {
    onPagingChange(pageNumber, pageSize);
  }

  const totalPages = Math.ceil(totalCount / pageSize);
  
  const generatePagination = () => {
    if (totalPages <= 5) {
      return Array.from({ length: totalPages }, (_, i) => i + 1);
    }
    
    let pages = [1];
    
    let startPage = Math.max(2, page - 1);
    let endPage = Math.min(totalPages - 1, page + 1);
    
    if (page <= 3) {
      endPage = Math.min(4, totalPages - 1);
    } else if (page >= totalPages - 2) {
      startPage = Math.max(2, totalPages - 3);
    }
    
    if (startPage > 2) {
      pages.push(-1);
    }
    
    for (let i = startPage; i <= endPage; i++) {
      pages.push(i);
    }
    
    if (endPage < totalPages - 1) {
      pages.push(-2);
    }
    
    if (totalPages > 1) {
      pages.push(totalPages);
    }
    
    return pages;
  };

  const paginationItems = generatePagination();
  
  const isFirstPage = page === 1;
  const isLastPage = totalCount <= page * pageSize;

  return (
    <div className="flex w-full p-4 items-center justify-between text-gray-500 text-sm flex-wrap">
      {showTotalCount && (
        <div>
          {t(
            'Showing {{currentCount}} - {{currentTotalCount}} total {{totalCount}}',
            {
            currentCount: page === 1 ? 1 : (page - 1) * pageSize,
            currentTotalCount:
              currentCount < pageSize ? totalCount : page * pageSize,
            totalCount: totalCount,
          },
        )}
        </div>
      )}
      <div>
        <Pagination className="justify-normal">
          <PaginationContent className="flex-wrap">
            {!isFirstPage && (
              <PaginationItem onClick={previous}>
                <PaginationPrevious>{t('Previous')}</PaginationPrevious>
              </PaginationItem>
            )}
            
            {showPageNumbers && paginationItems.map((pageNumber, index) => (
              pageNumber === -1 || pageNumber === -2 ? (
                <PaginationItem key={`ellipsis-${index}`}>
                  <PaginationEllipsis />
                </PaginationItem>
              ) : (
                <PaginationItem key={pageNumber}>
                  <PaginationLink 
                    isActive={page === pageNumber}
                    onClick={() => goToPage(pageNumber)}
                  >
                    {pageNumber}
                  </PaginationLink>
                </PaginationItem>
              )
            ))}
            
            {!isLastPage && (
              <PaginationItem onClick={next}>
                <PaginationNext>{t('Next')}</PaginationNext>
              </PaginationItem>
            )}
          </PaginationContent>
        </Pagination>
      </div>
    </div>
  );
};

export default PaginationContainer;
