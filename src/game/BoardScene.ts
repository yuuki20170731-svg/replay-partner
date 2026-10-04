import Phaser from 'phaser';
import { Simulation } from '../engine/simulation';
import { type Point } from '../engine/stages';

const COLORS = { floor: 0x102633, wall: 0x172e3a, line: 0x294656, a: 0x54dacb, b: 0xf3c67e, crate: 0xc08d5b, player: 0xf4d493, ghost: 0x65c8f7 };
const CELL = 55, X = 24, Y = 24;
export class BoardScene extends Phaser.Scene {
  private gfx?: Phaser.GameObjects.Graphics;
  private labels: Phaser.GameObjects.Text[] = [];
  constructor() { super('board'); }
  create(): void { this.gfx = this.add.graphics(); this.game.canvas.tabIndex = 0; this.cameras.main.setBackgroundColor('#081a24'); this.events.emit('ready'); }
  private label(text: string, x: number, y: number, color = '#dcecf0', size = 18): void {
    this.labels.push(this.add.text(x, y, text, { fontFamily: 'system-ui, sans-serif', fontSize: `${size}px`, fontStyle: 'bold', color }).setOrigin(0.5));
  }
  private xy(p: Point): { x: number; y: number } { return { x: X + p.x * CELL + CELL / 2, y: Y + p.y * CELL + CELL / 2 }; }
  renderBoard(sim: Simulation): void {
    if (!this.gfx) return;
    this.labels.forEach(t => t.destroy()); this.labels = [];
    const g = this.gfx; g.clear();
    const state = sim.state;
    for (let y = 0; y < sim.stage.map.length; y++) for (let x = 0; x < sim.stage.map[y]!.length; x++) {
      const tile = sim.stage.map[y]![x]!;
      const px = X + x * CELL, py = Y + y * CELL;
      g.fillStyle(tile === '#' ? COLORS.wall : COLORS.floor).fillRoundedRect(px + 2, py + 2, CELL - 4, CELL - 4, 5);
      if (tile !== '#') g.lineStyle(1, COLORS.line, 0.55).strokeRoundedRect(px + 2, py + 2, CELL - 4, CELL - 4, 5);
      if (tile === 'E') {
        g.lineStyle(3, 0xdbe9ee).strokeRoundedRect(px + 9, py + 8, CELL - 18, CELL - 16, 7);
        this.label('EXIT', px + CELL / 2, py + CELL / 2, '#f5e8c9', 13);
      }
      if (tile === 'A' || tile === 'B') {
        const color = tile === 'A' ? COLORS.a : COLORS.b;
        const pressed = [...state.clones.map(c => c.pos), state.player, ...state.crates].some(p => p.x === x && p.y === y);
        g.lineStyle(3, color).strokeCircle(px + CELL / 2, py + CELL / 2, 19);
        g.fillStyle(color, pressed ? 0.45 : 0.14).fillCircle(px + CELL / 2, py + CELL / 2, 16);
        this.label(tile, px + CELL / 2, py + CELL / 2, pressed ? '#06161d' : '#eafafa');
      }
      if (tile === 'a' || tile === 'b') {
        const color = tile === 'a' ? COLORS.a : COLORS.b;
        const opened = state.open.has(tile);
        g.fillStyle(color, opened ? 0.14 : 0.55).fillRoundedRect(px + 5, py + 3, CELL - 10, CELL - 6, 4);
        g.lineStyle(3, color).strokeRoundedRect(px + 5, py + 3, CELL - 10, CELL - 6, 4);
        this.label(tile.toUpperCase(), px + CELL / 2, py + CELL / 2, '#f7fcff');
      }
    }
    for (const p of state.crates) { const { x, y } = this.xy(p); g.fillStyle(COLORS.crate).fillRoundedRect(x - 20, y - 20, 40, 40, 5); g.lineStyle(2, 0xf4d5a4).strokeRoundedRect(x - 20, y - 20, 40, 40, 5); this.label('□', x, y, '#14232a', 28); }
    state.clones.forEach((c, i) => { const { x, y } = this.xy(c.pos); g.lineStyle(3, COLORS.ghost).strokeCircle(x, y, 19); g.fillStyle(COLORS.ghost, 0.2).fillCircle(x, y, 16); this.label(`${i + 1}`, x, y, '#ddf6ff', 20); if (c.blocked) this.label('!', x + 20, y - 23, '#ff927f', 18); });
    const { x, y } = this.xy(state.player); g.fillStyle(COLORS.player).fillCircle(x, y, 19); g.lineStyle(3, 0xfff4d0).strokeCircle(x, y, 20); this.label('●', x, y - 1, '#293a40', 16);
  }
}
