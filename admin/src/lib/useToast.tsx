import { useCallback, useEffect, useState } from 'react';
import { Notice } from '../components/Notice';

type Toast = { tone: 'error' | 'success'; text: string };

/** Success messages fade after 5 s; errors stay until the next action. Render `view` once per page. */
export function useToast() {
  const [toast, setToast] = useState<Toast>();

  useEffect(() => {
    if (toast?.tone !== 'success') return;
    const timer = setTimeout(() => setToast(undefined), 5000);
    return () => clearTimeout(timer);
  }, [toast]);

  const show = useCallback((tone: Toast['tone'], text: string) => setToast({ tone, text }), []);
  const view = (
    <div className="toast-region" aria-live="polite">
      {toast && <Notice tone={toast.tone}>{toast.text}</Notice>}
    </div>
  );
  return { show, view };
}
