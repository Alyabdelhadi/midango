import type { ReactNode } from 'react';

/** A status label. Always carries words, never colour alone. */
export function Pill({ tone = 'neutral', children }: { tone?: 'neutral' | 'success' | 'warning'; children: ReactNode }) {
  return <span className={`mg-pill mg-pill--${tone}`}>{children}</span>;
}
