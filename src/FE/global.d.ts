/// <reference types="react" />
import type React from 'react';

declare global {
  namespace JSX {
    interface IntrinsicElements extends React.JSX.IntrinsicElements {}
  }

  interface Date {
    addYear(years: number, date?: Date | string): Date;
  }
}

export {};
