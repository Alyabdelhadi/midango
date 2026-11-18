import { useCallback, useEffect, useState } from 'react';
import { Notice } from '../components/Notice';
import { errorText } from './api';
import { useI18n } from './i18n';

type Tone = 'error' | 'success';
type Toast = { tone: Tone; text: string; undo?: () => Promise<unknown> };

/** Success messages fade (5 s, or 10 s when they offer Undo); errors stay until the next action.
 *  Render `view` once per page. */
export function useToast() {
  const { t } = useI18n();
  const [toast, setToast] = useState<Toast>();

  useEffect(() => {
    if (toast?.tone !== 'success') return;
    const timer = setTimeout(() => setToast(undefined), toast.undo ? 10_000 : 5000);
    return () => clearTimeout(timer);
  }, [toast]);

  const show = useCallback(
    (tone: Tone, text: string, undo?: () => Promise<unknown>) => setToast({ tone, text, undo }),
    [],
  );

  async function runUndo(undo: () => Promise<unknown>) {
    setToast(undefined);
    try {
      await undo();
      setToast({ tone: 'success', text: t('undone') });
    } catch (err) {
      setToast({ tone: 'error', text: errorText(err) });
    }
  }

  const view = (
    <div className="toast-region" aria-live="polite">
      {toast && (
        <Notice tone={toast.tone}>
          {toast.text}
          {toast.undo && (
            <button type="button" className="toast-undo" onClick={() => void runUndo(toast.undo!)}>
              {t('undo')}
            </button>
          )}
        </Notice>
      )}
    </div>
  );
  return { show, view };
}
