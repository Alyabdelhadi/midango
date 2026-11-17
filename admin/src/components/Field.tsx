import type { ReactNode } from 'react';

export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="mg-field">
      <span className="mg-field-label">{label}</span>
      {children}
      {hint && <span className="mg-field-hint">{hint}</span>}
    </label>
  );
}
