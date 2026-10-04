import { at, keyOf, type Point, type Stage } from './stages';

export type Command = 'up' | 'down' | 'left' | 'right' | 'wait';
export type Mode = 'replay' | 'recording' | 'cleared';
type Actor = { pos: Point; blocked: boolean };
export type Snapshot = {
  mode: Mode; tick: number; player: Point; clones: Array<Actor & { done: boolean }>;
  crates: Point[]; open: Set<'a' | 'b'>; remaining: number; feedback: string;
};
const DELTA: Record<Command, Point> = { up: { x: 0, y: -1 }, down: { x: 0, y: 1 }, left: { x: -1, y: 0 }, right: { x: 1, y: 0 }, wait: { x: 0, y: 0 } };
const LIMIT = 150; // 150 × 200 ms = 30 seconds.

export class Simulation {
  readonly stage: Stage;
  readonly start: Point;
  private recordings: Command[][] = [];
  private current: Command[] = [];
  private player: Actor;
  private clones: Actor[] = [];
  private crates: Point[] = [];
  private tick = 0;
  private mode: Mode = 'replay';
  private open = new Set<'a' | 'b'>();
  feedback = '';

  constructor(stage: Stage) {
    this.stage = stage;
    this.start = this.find('P')[0] ?? { x: 1, y: 1 };
    this.player = { pos: { ...this.start }, blocked: false };
    this.resetRoom();
  }

  get recordingsCount(): number { return this.recordings.length; }
  get state(): Snapshot {
    return { mode: this.mode, tick: this.tick, player: { ...this.player.pos },
      clones: this.clones.map((c, i) => ({ pos: { ...c.pos }, blocked: c.blocked, done: this.tick >= (this.recordings[i]?.length ?? 0) })),
      crates: this.crates.map(c => ({ ...c })), open: new Set(this.open), remaining: Math.max(0, LIMIT - this.current.length), feedback: this.feedback };
  }

  private find(symbol: string): Point[] {
    const found: Point[] = [];
    this.stage.map.forEach((row, y) => [...row].forEach((c, x) => { if (c === symbol) found.push({ x, y }); }));
    return found;
  }

  private tile(pos: Point): string { return this.stage.map[pos.y]?.[pos.x] ?? '#'; }

  private updateDoors(): void {
    const occupied = [...this.clones.map(c => c.pos), this.player.pos, ...this.crates];
    for (const tag of ['a', 'b'] as const) {
      const switchTiles = this.find(tag.toUpperCase());
      const doorTiles = this.find(tag);
      const pressed = switchTiles.some(s => occupied.some(p => at(s, p)));
      // An occupied door stays open until all occupants have left.
      const held = doorTiles.some(d => occupied.some(p => at(d, p)));
      if (pressed || held) this.open.add(tag); else this.open.delete(tag);
    }
  }

  private resetRoom(): void {
    this.tick = 0;
    this.player = { pos: { ...this.start }, blocked: false };
    this.clones = this.recordings.map(() => ({ pos: { ...this.start }, blocked: false }));
    this.crates = this.find('C');
    this.open.clear();
    this.updateDoors();
    this.mode = 'replay';
    this.feedback = '部屋を巻き戻しました';
  }

  private blocked(pos: Point, crateMoving = false): boolean {
    const tile = this.tile(pos);
    return tile === '#' || ((tile === 'a' || tile === 'b') && !this.open.has(tile)) ||
      (crateMoving && this.crates.some(c => at(c, pos)));
  }

  private move(actor: Actor, action: Command): void {
    actor.blocked = false;
    if (action === 'wait') return;
    const d = DELTA[action];
    const to = { x: actor.pos.x + d.x, y: actor.pos.y + d.y };
    if (this.blocked(to)) { actor.blocked = true; return; }
    const crate = this.crates.find(c => at(c, to));
    if (crate) {
      const beyond = { x: to.x + d.x, y: to.y + d.y };
      if (this.blocked(beyond, true)) { actor.blocked = true; return; }
      crate.x = beyond.x; crate.y = beyond.y;
    }
    actor.pos = to;
  }

  step(action: Command): Snapshot {
    if (this.mode === 'cleared') return this.state;
    if (this.mode === 'recording' && this.current.length >= LIMIT) { this.commit(); return this.state; }
    this.updateDoors();
    // Stable order: older recordings first, current player last. Identical crate pushes resolve in that order.
    this.clones.forEach((clone, i) => {
      this.move(clone, this.recordings[i]?.[this.tick] ?? 'wait');
      this.updateDoors();
    });
    this.updateDoors();
    this.move(this.player, action);
    if (this.mode === 'recording') this.current.push(action);
    this.tick++;
    this.updateDoors();
    if (this.clones.some(c => c.blocked)) this.feedback = '分身の移動が障害物で止まりました';
    else if (this.player.blocked) this.feedback = 'ここは通れません';
    else this.feedback = '';
    if (this.mode === 'replay' && this.tile(this.player.pos) === 'E') {
      this.mode = 'cleared'; this.feedback = 'ステージクリア';
    }
    if (this.mode === 'recording' && this.current.length >= LIMIT) this.commit();
    return this.state;
  }

  begin(): boolean {
    if (this.mode !== 'replay' || this.recordings.length >= 2) return false;
    this.current = [];
    this.resetRoom();
    this.mode = 'recording';
    this.feedback = '記録中 — もう一度 E で確定';
    return true;
  }

  commit(): boolean {
    if (this.mode !== 'recording') return false;
    this.recordings.push([...this.current]);
    this.current = [];
    this.resetRoom();
    return true;
  }

  cancel(): void { if (this.mode === 'recording') { this.current = []; this.resetRoom(); } }
  retry(): void { this.current = []; this.resetRoom(); }
  undo(): void { if (this.mode !== 'recording') { this.recordings.pop(); this.resetRoom(); } }
  restart(): void { this.recordings = []; this.current = []; this.resetRoom(); }
  getRecordings(): Command[][] { return this.recordings.map(r => [...r]); }
  getCell(p: Point): string { return this.tile(p); }
  getOccupiedCrateKeys(): Set<string> { return new Set(this.crates.map(keyOf)); }
}
