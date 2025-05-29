import { useState, useEffect } from 'react';
import { Skeleton } from '../ui/skeleton';

interface ImageLoaderProps {
  src: string;
  alt?: string;
  className?: string;
}

export function ImageLoader({ src, alt = '', className }: ImageLoaderProps) {
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    const img = new Image();
    img.src = src;
    
    img.onload = () => {
      setIsLoading(false);
    };
    
    img.onerror = () => {
      setIsLoading(false);
      setError(true);
    };

    return () => {
      img.onload = null;
      img.onerror = null;
    };
  }, [src]);

  if (isLoading) {
    return <Skeleton className={className} />;
  }

  if (error) {
    return <div className={className}>加载失败</div>;
  }

  return (
    <img
      src={src}
      alt={alt}
      className={className}
    />
  );
}
