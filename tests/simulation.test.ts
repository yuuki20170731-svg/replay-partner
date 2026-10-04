import { describe, expect, it } from 'vitest';
import { Simulation, type Command } from '../src/engine/simulation';
import { stages } from '../src/engine/stages';
import { freshProgress, readProgress, writeProgress } from '../src/engine/storage';

const moves = (sim: Simulation, pattern: string): void => {
  const commands: Record<string, Command> = { R: 'right', L: 'left', U: 'up', D: 'down', W: 'wait' };
  for (const c of pattern) sim.step(commands[c]!);
};
const record = (sim: Simulation, pattern: string): void => {
  expect(sim.begin()).toBe(true);
  moves(sim, pattern);
  expect(sim.commit()).toBe(true);
};

const solutions: Array<{ records: string[]; play: string }> = [
  { records: [], play: 'RRRRR' },
  { records: ['RDD'], play: 'RRRRRRRRRRR' },
  { records: ['RRRUU'], play: 'RRRRRRRRRRR' },
  { records: [], play: 'DRRURRRRRRRRR' },
  { records: ['RDD'], play: 'RRRRRRDRURRRR' },
  { records: ['RDD', 'RRRRRRRDD'], play: 'RRRRRRRRRRR' },
  { records: ['RRRUU', 'RRRRRRRUU'], play: 'RRRRRRRRRRR' },
  { records: ['RRRDD', 'RRRRRRRRUU'], play: 'RRRRRRRRWRRR' },
  { records: ['RRUU'], play: 'RRRRRRDRURRRR' },
  { records: ['RRDD', 'RRRRRRRUU'], play: 'RRRRRRRRRRR' }
];

describe('all playable stages', () => {
  it('has ten distinct stages with valid board data', () => {
    expect(stages).toHaveLength(10);
    for (const stage of stages) {
      expect(stage.map).toHaveLength(7);
      expect(stage.map.every(row => row.length === 16)).toBe(true);
      expect(stage.map.join('').match(/P/g)).toHaveLength(1);
      expect(stage.map.join('').match(/E/g)).toHaveLength(1);
    }
  });
  it.each(stages.map((stage, i) => [stage.name, i] as const))('%s can be solved through real record and replay actions', (_name, index) => {
    const sim = new Simulation(stages[index]!);
    const solution = solutions[index]!;
    solution.records.forEach(path => record(sim, path));
    moves(sim, solution.play);
    expect(sim.state.mode).toBe('cleared');
  });
});

describe('simulation rules', () => {
  it('does not clear a stage while recording at the exit', () => {
    const sim = new Simulation(stages[0]!);
    sim.begin(); moves(sim, 'RRRRR');
    expect(sim.state.mode).toBe('recording');
    sim.commit();
    expect(sim.state.mode).toBe('replay');
  });
  it('keeps a closing door open while occupied', () => {
    const custom = { ...stages[0]!, map: ['#######', '#PAaE.#', '#######'] };
    const sim = new Simulation(custom);
    sim.step('right'); sim.step('right');
    expect(sim.state.player).toEqual({ x: 3, y: 1 });
    expect(sim.state.open.has('a')).toBe(true);
  });
  it('resolves a shared crate in old-clone-first order', () => {
    const custom = { ...stages[0]!, map: ['#######', '#PC.AE#', '#######'] };
    const sim = new Simulation(custom);
    record(sim, 'R');
    sim.step('right');
    expect(sim.state.clones[0]?.pos).toEqual({ x: 2, y: 1 });
    expect(sim.state.player).toEqual({ x: 2, y: 1 });
    expect(sim.state.crates).toEqual([{ x: 3, y: 1 }]);
  });
  it('does not clear while recording and allows retry with clones', () => {
    const sim = new Simulation(stages[1]!);
    record(sim, 'RDD');
    expect(sim.recordingsCount).toBe(1);
    moves(sim, 'RRRR');
    sim.retry();
    expect(sim.recordingsCount).toBe(1);
    expect(sim.state.player).toEqual(sim.start);
    moves(sim, 'RRRRRRRRRRR');
    expect(sim.state.mode).toBe('cleared');
  });
  it('cancels, undoes and fully restarts without stale state', () => {
    const sim = new Simulation(stages[1]!);
    sim.begin(); moves(sim, 'RDD'); sim.cancel();
    expect(sim.recordingsCount).toBe(0);
    record(sim, 'RDD'); sim.undo();
    expect(sim.recordingsCount).toBe(0);
    record(sim, 'RDD'); sim.restart();
    expect(sim.recordingsCount).toBe(0);
    expect(sim.state.open.size).toBe(0);
  });
  it('stops a blocked clone and keeps it in the same location after playback', () => {
    const stage = { ...stages[0]!, map: ['######', '#PC#E#', '######'] };
    const sim = new Simulation(stage);
    record(sim, 'RR');
    moves(sim, 'WWWW');
    expect(sim.state.clones[0]?.done).toBe(true);
    expect(sim.state.clones[0]?.pos).toEqual({ x: 1, y: 1 });
  });
  it('automatically commits at the 30 second limit', () => {
    const sim = new Simulation(stages[1]!);
    sim.begin();
    for (let i = 0; i < 150; i++) sim.step('wait');
    expect(sim.state.mode).toBe('replay');
    expect(sim.getRecordings()[0]).toHaveLength(150);
  });
});

describe('progress storage', () => {
  it('recovers from corrupt and old data without losing boot', () => {
    expect(readProgress({ getItem: () => '{bad' })).toEqual(freshProgress());
    expect(readProgress({ getItem: () => '{"version":0}' })).toEqual(freshProgress());
    expect(readProgress({ getItem: () => { throw new Error('denied'); } })).toEqual(freshProgress());
    expect(readProgress({ getItem: () => '{"version":1,"unlocked":2,"cleared":[1],"prefs":{"volume":1e999}}' }).prefs.volume).toBe(0.35);
    expect(writeProgress(freshProgress(), { setItem: () => { throw new Error('denied'); } })).toBe(false);
  });
});
