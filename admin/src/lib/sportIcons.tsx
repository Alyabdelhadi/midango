import {
  Activity, Award, Bike, CircleDot, Dumbbell, Flag, Footprints, Goal,
  Medal, Mountain, Target, Timer, Trophy, Users, Volleyball, Waves,
  type LucideIcon,
} from 'lucide-react';
import { mediaUrl } from './api';

/** The icons an operator can give a sport. Keys are Lucide names; the Flutter app maps the same keys (M8). */
export const SPORT_ICONS: Record<string, LucideIcon> = {
  volleyball: Volleyball,
  goal: Goal,
  trophy: Trophy,
  dumbbell: Dumbbell,
  target: Target,
  'circle-dot': CircleDot,
  bike: Bike,
  waves: Waves,
  mountain: Mountain,
  footprints: Footprints,
  medal: Medal,
  award: Award,
  flag: Flag,
  timer: Timer,
  activity: Activity,
  users: Users,
};

export const SPORT_ICON_KEYS = Object.keys(SPORT_ICONS);

/** An uploaded icon (iconUrl) wins over the built-in one. Decorative: the name always sits beside it. */
export function SportIcon({ iconKey, iconUrl, size }: { iconKey: string; iconUrl?: string | null; size: number }) {
  if (iconUrl) return <img src={mediaUrl(iconUrl)} alt="" width={size} height={size} className="uploaded-icon" />;
  const Icon = SPORT_ICONS[iconKey] ?? CircleDot;
  return <Icon size={size} aria-hidden="true" />;
}
