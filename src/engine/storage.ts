export type Preferences = { volume: number; mute: boolean; reducedMotion: boolean };
export type Progress = { version: 1; unlocked: number; cleared: number[]; prefs: Preferences };
const KEY = 'replay-partner-v1';
export const freshProgress = (): Progress => ({ version: 1, unlocked: 1, cleared: [], prefs: { volume: 0.35, mute: false, reducedMotion: false } });

export function readProgress(storage?: Pick<Storage, 'getItem'>): Progress {
  try {
    const raw = (storage ?? globalThis.localStorage)?.getItem(KEY);
    if (!raw) return freshProgress();
    const value: unknown = JSON.parse(raw);
    if (!value || typeof value !== 'object') return freshProgress();
    const v = value as Record<string, unknown>;
    if (v.version !== 1 || !Number.isInteger(v.unlocked) || !Array.isArray(v.cleared)) return freshProgress();
    const p = v.prefs as Record<string, unknown> | undefined;
    return { version: 1, unlocked: Math.max(1, Math.min(10, v.unlocked as number)),
      cleared: v.cleared.filter((n): n is number => Number.isInteger(n) && n >= 1 && n <= 10),
      prefs: { volume: typeof p?.volume === 'number' && Number.isFinite(p.volume) ? Math.max(0, Math.min(1, p.volume)) : 0.35,
        mute: typeof p?.mute === 'boolean' ? p.mute : false,
        reducedMotion: typeof p?.reducedMotion === 'boolean' ? p.reducedMotion : false } };
  } catch { return freshProgress(); }
}

export function writeProgress(value: Progress, storage?: Pick<Storage, 'setItem'>): boolean {
  try {
    const target = storage ?? globalThis.localStorage;
    if (!target) return false;
    target.setItem(KEY, JSON.stringify(value));
    return true;
  } catch { return false; }
}
