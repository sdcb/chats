'use client';

import * as SliderPrimitive from '@radix-ui/react-slider';
import * as React from 'react';

import { cn } from '@/lib/utils';

const Slider = React.forwardRef<
  React.ElementRef<typeof SliderPrimitive.Root>,
  React.ComponentPropsWithoutRef<typeof SliderPrimitive.Root>
>(({ className, ...props }, ref) => (
  <SliderPrimitive.Root
    ref={ref}
    className={cn(
      // Root layout: horizontal by default, vertical switches to column
      'relative flex touch-none select-none items-center',
      'data-[orientation=horizontal]:w-full',
      'data-[orientation=vertical]:h-full data-[orientation=vertical]:w-auto data-[orientation=vertical]:flex-col',
      className,
    )}
    {...props}
  >
    <SliderPrimitive.Track
      className={cn(
        'relative grow overflow-hidden rounded-full bg-secondary',
        // Horizontal track: full width, small height
        'data-[orientation=horizontal]:h-2 data-[orientation=horizontal]:w-full',
        // Vertical track: full height, fixed narrow width
        'data-[orientation=vertical]:h-full data-[orientation=vertical]:w-2',
      )}
    >
      <SliderPrimitive.Range
        className={cn(
          'absolute bg-primary',
          // Horizontal range fills height from left to thumb
          'data-[orientation=horizontal]:h-full data-[orientation=horizontal]:left-0',
          // Vertical range fills width from bottom to thumb
          'data-[orientation=vertical]:w-full data-[orientation=vertical]:bottom-0',
        )}
      />
    </SliderPrimitive.Track>
    <SliderPrimitive.Thumb
      className={cn(
        'block rounded-full border-2 border-primary bg-background ring-offset-background transition-colors',
        'h-4 w-4',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        'disabled:pointer-events-none disabled:opacity-50',
      )}
    />
  </SliderPrimitive.Root>
));
Slider.displayName = SliderPrimitive.Root.displayName;

export { Slider };
