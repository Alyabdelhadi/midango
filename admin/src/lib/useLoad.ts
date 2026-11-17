import { useCallback, useEffect, useState } from 'react';
import { errorText } from './api';

/** Runs `load` on mount and on reload(). Keeps the last good data while reloading. */
export function useLoad<T>(load: () => Promise<T>) {
  const [data, setData] = useState<T>();
  const [error, setError] = useState<string>();
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let live = true;
    load().then(
      (result) => {
        if (live) {
          setData(result);
          setError(undefined);
        }
      },
      (err: unknown) => {
        if (live) setError(errorText(err));
      },
    );
    return () => {
      live = false;
    };
    // `load` is a new closure every render; `version` is what triggers a reload.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [version]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);
  return { data, error, reload, setData };
}
