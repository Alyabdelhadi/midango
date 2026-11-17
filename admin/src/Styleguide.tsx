import { useEffect, useState } from 'react';
import { Languages } from 'lucide-react';
import { Button } from './components/Button';
import { StatusTag, type BookingState } from './components/StatusTag';
import './styleguide.css';

const SWATCHES: Array<[name: string, cssVar: string]> = [
  ['Lime', '--mg-lime'],
  ['Navy', '--mg-navy'],
  ['Slate', '--mg-slate'],
  ['Muted', '--mg-muted'],
  ['Cyan', '--mg-cyan'],
  ['Cyan Ink', '--mg-cyan-ink'],
  ['Cloud', '--mg-cloud'],
  ['White', '--mg-white'],
  ['Error', '--mg-error'],
  ['Warning', '--mg-warning'],
];

const TYPE: Array<[name: string, cssVar: string]> = [
  ['display', '--mg-text-display'],
  ['title-l', '--mg-text-title-l'],
  ['title-m', '--mg-text-title-m'],
  ['numeric', '--mg-text-numeric'],
  ['body', '--mg-text-body'],
  ['label', '--mg-text-label'],
  ['caption', '--mg-text-caption'],
];

const STATES: BookingState[] = ['free', 'pay-at-venue', 'paid', 'checked-in', 'blocked'];

export function Styleguide() {
  const [dir, setDir] = useState<'ltr' | 'rtl'>('ltr');

  useEffect(() => {
    document.documentElement.dir = dir;
    document.documentElement.lang = dir === 'rtl' ? 'ar' : 'en';
  }, [dir]);

  return (
    <main className="sg">
      <header className="sg-header">
        <img src="/midango-icon.png" alt="Midango" height={32} />
        <h1>Styleguide</h1>
        <Button variant="secondary" onClick={() => setDir(dir === 'ltr' ? 'rtl' : 'ltr')}>
          <Languages size={16} aria-hidden="true" /> {dir === 'ltr' ? 'العربية' : 'English'}
        </Button>
      </header>

      <section>
        <h2>Colors</h2>
        <div className="sg-swatches">
          {SWATCHES.map(([name, cssVar]) => (
            <div key={name} className="sg-swatch">
              <div style={{ background: `var(${cssVar})` }} />
              <span>{name}</span>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h2>Type</h2>
        {TYPE.map(([name, cssVar]) => (
          <p key={name} style={{ font: `var(${cssVar})` }}>
            {name} · Book your next move · احجز ملعبك القادم · 20:00
          </p>
        ))}
      </section>

      <section>
        <h2>Buttons</h2>
        <div className="sg-row">
          <Button>Save booking</Button>
          <Button variant="secondary">Block time</Button>
          <Button variant="text">View policy</Button>
          <Button variant="destructive">Cancel booking</Button>
          <Button disabledReason="Court 3 is closed for maintenance">Save booking</Button>
        </div>
      </section>

      <section>
        <h2>Booking states</h2>
        <div className="sg-row">
          {STATES.map((s) => (
            <StatusTag key={s} state={s} />
          ))}
        </div>
      </section>
    </main>
  );
}
