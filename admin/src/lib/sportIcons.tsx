import {
  Activity, Award, Bike, CircleDot, Dumbbell, Flag, Footprints, Goal,
  Medal, Mountain, Target, Timer, Trophy, Users, Volleyball, Waves,
  type LucideIcon,
} from 'lucide-react';

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

export function SportIcon({ iconKey, size }: { iconKey: string; size: number }) {
  const Icon = SPORT_ICONS[iconKey] ?? CircleDot;
  return <Icon size={size} aria-hidden="true" />;
}
