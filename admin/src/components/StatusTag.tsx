import './status-tag.css';

export type BookingState = 'free' | 'pay-at-venue' | 'paid' | 'checked-in' | 'blocked';

// Every state carries a word, never colour alone.
const LABEL: Record<BookingState, string> = {
  free: 'Free',
  'pay-at-venue': 'Pay at venue',
  paid: 'Paid',
  'checked-in': '✓ Checked in',
  blocked: 'Blocked',
};

export function StatusTag({ state }: { state: BookingState }) {
  return <span className={`mg-tag mg-tag--${state}`}>{LABEL[state]}</span>;
}
