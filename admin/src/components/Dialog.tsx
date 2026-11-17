import { useEffect, useId, useRef, type ReactNode } from 'react';

/** Native <dialog>: the browser provides the focus trap, Esc and the backdrop.
 *  Children mount only while open, so every opening starts with a fresh form. */
export function Dialog({
  open,
  title,
  onClose,
  children,
}: {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  return (
    <dialog ref={ref} className="mg-dialog" aria-labelledby={titleId} onClose={onClose}>
      <h2 id={titleId}>{title}</h2>
      {open && children}
    </dialog>
  );
}
