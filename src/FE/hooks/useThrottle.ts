import { useEffect, useRef, useState } from 'react';

export const useThrottle = <V>(value: V, wait: number) => {
  const [throttledValue, setThrottledValue] = useState<V>(value);
  const lastExecuted = useRef<number>(0);

  useEffect(() => {
    const now = Date.now();
    if (lastExecuted.current === 0) {
      lastExecuted.current = now - wait;
    }

    if (now >= lastExecuted.current + wait) {
      lastExecuted.current = now;
      setThrottledValue(value);
    } else {
      const timerId = setTimeout(() => {
        lastExecuted.current = Date.now();
        setThrottledValue(value);
      }, wait);

      return () => clearTimeout(timerId);
    }
  }, [value, wait]);

  return throttledValue;
};
