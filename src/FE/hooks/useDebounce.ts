import { useCallback, useRef } from 'react';

export default function useDebounce<A extends any[], R>(
  fn: (...args: A) => R,
  delay: number,
): (...args: A) => void {
  const timer = useRef<NodeJS.Timeout>();
  return useCallback(
    (...args: A) => {
      if (timer.current) clearTimeout(timer.current);
      timer.current = setTimeout(() => {
        fn(...args);
      }, delay);
    },
    [fn, delay],
  );
}
