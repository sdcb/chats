import { FC, useContext, useRef, useState } from 'react';

import useTranslation from '@/hooks/useTranslation';

import { IconX } from '@/components/Icons/index';

import { cn } from '@/lib/utils';

interface Props {
  placeholder: string;
  searchTerm: string;
  className?: string;
  containerClassName?: string;
  onSearch: (searchTerm: string) => void;
}
const Search: FC<Props> = ({
  placeholder,
  searchTerm,
  className,
  containerClassName,
  onSearch,
}) => {
  const { t } = useTranslation();
  const [query, setQuery] = useState<string>('');
  const timeoutRef = useRef<number | undefined>(undefined);

  const handleChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const value = event.target.value;
    setQuery(value);

    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
    }

    timeoutRef.current = window.setTimeout(() => {
      onSearch(value);
    }, 500);
  };

  const clearSearch = () => {
    onSearch('');
    setQuery('');
  };

  return (
    <div className={cn('relative flex items-center h-11 w-full', containerClassName)}>
      <input
        className={cn(
          'w-full flex-1 rounded-md px-3 py-3 pr-10 text-[14px] bg-muted leading-3 border-none outline-none',
          className,
        )}
        type="text"
        placeholder={t(placeholder) || ''}
        value={query}
        onChange={handleChange}
      />

      {searchTerm && (
        <IconX
          className="absolute right-[15px] cursor-pointer text-neutral-300 hover:text-neutral-400"
          size={18}
          onClick={clearSearch}
        />
      )}
    </div>
  );
};

export default Search;
