import { useId, type ButtonHTMLAttributes } from 'react';
import './button.css';

type Variant = 'primary' | 'secondary' | 'text' | 'destructive';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  /** Shown under a disabled button. A button is never disabled without a reason. */
  disabledReason?: string;
};

export function Button({ variant = 'primary', disabledReason, className, ...rest }: Props) {
  const reasonId = useId();
  const disabled = rest.disabled || disabledReason !== undefined;

  return (
    <span className="mg-button-wrap">
      <button
        type="button"
        {...rest}
        disabled={disabled}
        aria-describedby={disabledReason ? reasonId : undefined}
        className={['mg-button', `mg-button--${variant}`, className].filter(Boolean).join(' ')}
      />
      {disabledReason && (
        <span id={reasonId} className="mg-button-reason">
          {disabledReason}
        </span>
      )}
    </span>
  );
}
