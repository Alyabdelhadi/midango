import { useRef, type ReactNode } from 'react';
import { Button } from './Button';

/** A Button that opens the file picker for JPEG, PNG or WebP images. The input is reset after each pick
 *  so choosing the same file again still fires. */
export function UploadButton({
  children,
  onFiles,
  multiple = false,
  variant = 'secondary',
  disabled,
  disabledReason,
  'aria-label': ariaLabel,
}: {
  children: ReactNode;
  onFiles: (files: File[]) => void;
  multiple?: boolean;
  variant?: 'secondary' | 'text';
  disabled?: boolean;
  disabledReason?: string;
  'aria-label'?: string;
}) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <>
      <Button
        variant={variant}
        disabled={disabled}
        disabledReason={disabledReason}
        aria-label={ariaLabel}
        onClick={() => input.current?.click()}
      >
        {children}
      </Button>
      <input
        ref={input}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        multiple={multiple}
        className="visually-hidden"
        tabIndex={-1}
        aria-hidden="true"
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          e.target.value = '';
          if (files.length > 0) onFiles(files);
        }}
      />
    </>
  );
}
