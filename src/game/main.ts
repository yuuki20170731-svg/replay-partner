import Phaser from 'phaser';
import { BoardScene } from './BoardScene';
import { Simulation, type Command } from '../engine/simulation';
import { stages } from '../engine/stages';
import { readProgress, writeProgress } from '../engine/storage';
import '../style.css';

type Screen = 'title' | 'help' | 'select' | 'playing' | 'pause' | 'clear' | 'complete' | 'settings' | 'credits';
const app = document.querySelector<HTMLDivElement>('#game-app')!;
app.innerHTML = `<header class="game-header"><a class="brand" href="/" aria-label="紹介ページへ"><span class="brand-symbol">⟲</span> REPLAY <span>PARTNER</span></a><span class="game-header-right">FACILITY / ARCHIVE 001 <span class="signal-dot"></span> SYSTEM ONLINE</span></header>
<main class="game-layout"><section class="board-area" aria-label="パズル盤面"><div class="board-top"><span id="stage-heading">STAGE 01 / 起動室</span><span id="status-heading">待機中</span></div><div class="board-wrap"><div id="board" tabindex="0" role="application" aria-label="パズル盤面。操作はWASDまたは矢印キー、Eで記録、Spaceで待機"></div><div id="overlay" class="game-overlay"></div></div><div id="game-feedback" class="feedback" role="status" aria-live="polite">過去の自分と、扉を開こう。</div></section>
<aside class="side-panel"><div class="side-top"><span class="eyebrow">MISSION CONTROL</span><h1 id="mission-name">REPLAY<br>PARTNER</h1><p id="mission-goal">過去の自分と協力するパズル</p></div><div class="status-card"><span class="card-label">CURRENT STATE</span><strong id="mode-value">待機</strong><div class="meter"><span id="time-meter"></span></div><small id="remaining-value">記録可能時間 30.0 秒</small></div><div class="status-card"><span class="card-label">PARTNERS</span><strong id="partners-value">0 / 2</strong><small id="partner-detail">まだ分身はいません</small></div><div class="side-controls"><button type="button" id="record-button" data-action="record" class="action-button accent">E　記録を始める</button><button type="button" data-action="retry" class="action-button">↶　リトライ</button><button type="button" data-action="undo" class="action-button">⌫　最後の分身を削除</button><button type="button" data-action="cancel" class="action-button">記録をキャンセル</button><button type="button" data-action="restart" class="action-button">部屋を最初から</button><button type="button" data-action="hint" class="action-button subtle">?　ヒントを見る</button></div><div class="control-reference"><span class="card-label">KEYBOARD</span><p><kbd>W A S D</kbd> 移動　<kbd>E</kbd> 記録 / 確定</p><p><kbd>Space</kbd> 待機　<kbd>Esc</kbd> ポーズ</p></div></aside></main><footer class="game-footer"><span>RECORD THE PAST. OPEN THE FUTURE.</span><span>PC KEYBOARD RECOMMENDED · <a href="/">紹介ページへ ↗</a></span></footer>`;

const progress = readProgress();
let screen: Screen = 'title';
let previous: Screen = 'title';
let stageIndex = 0;
let sim = new Simulation(stages[0]!);
let hintLevel = 0;
let held: Command | null = null;
let audio: AudioContext | undefined;
const board = document.querySelector<HTMLDivElement>('#board')!;
const overlay = document.querySelector<HTMLDivElement>('#overlay')!;
const feedback = document.querySelector<HTMLDivElement>('#game-feedback')!;
const scene = new BoardScene();
new Phaser.Game({ type: Phaser.AUTO, parent: 'board', width: 928, height: 434, backgroundColor: '#081a24', scene: [scene], scale: { mode: Phaser.Scale.FIT, autoCenter: Phaser.Scale.CENTER_BOTH }, render: { antialias: true } });

function beep(freq: number, duration = 0.07): void {
  if (progress.prefs.mute || progress.prefs.volume <= 0) return;
  try {
    audio ??= new AudioContext();
    const osc = audio.createOscillator(), gain = audio.createGain();
    osc.type = 'sine'; osc.frequency.value = freq;
    gain.gain.setValueAtTime(progress.prefs.volume * 0.08, audio.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + duration);
    osc.connect(gain).connect(audio.destination); osc.start(); osc.stop(audio.currentTime + duration);
  } catch { /* Audio is optional. */ }
}

function save(): void { writeProgress(progress); }
function focusBoard(): void { board.querySelector('canvas')?.focus(); }
function button(label: string, action: string, primary = false): string { return `<button type="button" data-action="${action}" class="button ${primary ? 'primary' : 'ghost'}">${label}</button>`; }
function panel(title: string, subtitle: string, body: string): string { return `<div class="overlay-card"><p class="eyebrow">REPLAY PARTNER / FACILITY 001</p><h2>${title}</h2><p>${subtitle}</p>${body}</div>`; }

function show(next: Screen): void {
  screen = next;
  held = null;
  board.classList.toggle('board-inactive', next !== 'playing');
  overlay.hidden = next === 'playing';
  if (next === 'title') overlay.innerHTML = panel('昨日の自分と、<br>扉を開こう。', '行動を記録して分身を再生する、10の小さな部屋。', `<div class="overlay-actions">${button('はじめから', 'new', true)}${button('続きから', 'continue')}${button('遊び方', 'help')}${button('ステージ選択', 'select')}${button('設定', 'settings')}${button('クレジット', 'credits')}</div>`);
  if (next === 'help') overlay.innerHTML = panel('遊び方', 'ひとりでは通れない扉を、過去の自分と開きます。', `<ol class="help-list"><li><b>移動</b>　WASD / 矢印キー。Space はその場で待機。</li><li><b>記録</b>　E で開始。もう一度 E で確定。最大30秒。</li><li><b>再生</b>　部屋が戻り、分身が同じ行動を繰り返します。</li><li><b>協力</b>　分身にスイッチを任せ、現在の自分で出口へ。</li></ol><div class="overlay-actions">${button('ステージ1へ', 'new', true)}${button('戻る', 'back')}</div>`);
  if (next === 'select') overlay.innerHTML = panel('ステージ選択', 'クリアした部屋には何度でも戻れます。', `<div class="stage-grid">${stages.map((s, i) => `<button type="button" data-stage="${i}" ${s.id > progress.unlocked ? 'disabled' : ''} class="stage-choice"><span>${String(s.id).padStart(2, '0')}</span><b>${s.name}</b><small>${progress.cleared.includes(s.id) ? 'CLEAR' : s.id > progress.unlocked ? 'LOCKED' : s.lesson}</small></button>`).join('')}</div><div class="overlay-actions">${button('戻る', 'back')}</div>`);
  if (next === 'pause') overlay.innerHTML = panel('一時停止', '思考は止めても、記録は失われません。', `<div class="overlay-actions">${button('再開', 'resume', true)}${button('リトライ', 'retry')}${button('遊び方', 'help')}${button('設定', 'settings')}${button('タイトルへ', 'title')}</div>`);
  if (next === 'clear') overlay.innerHTML = panel(`STAGE ${String(stageIndex + 1).padStart(2, '0')} CLEAR`, `「${stages[stageIndex]!.name}」を突破しました。`, `<div class="result-line"><span>使った分身</span><strong>${sim.recordingsCount} / 2</strong></div><div class="overlay-actions">${button(stageIndex === 9 ? 'エピローグへ' : '次のステージへ', 'next', true)}${button('もう一度', 'retry')}${button('ステージ選択', 'select')}</div>`);
  if (next === 'complete') overlay.innerHTML = panel('脱出成功。', '過去のあなたと今のあなたが、同じ出口を開きました。', `<p class="epilogue">「もうひとりの自分がいれば」と思った日があった。<br>でも、ここまで連れてきたのは、ずっとあなた自身だった。</p><div class="overlay-actions">${button('ステージ選択', 'select', true)}${button('タイトルへ', 'title')}</div>`);
  if (next === 'settings') overlay.innerHTML = panel('設定', '自分のペースで遊べるように。', `<div class="settings-list"><label>音量 <input id="volume" type="range" min="0" max="100" value="${Math.round(progress.prefs.volume * 100)}"><output id="volume-output">${Math.round(progress.prefs.volume * 100)}%</output></label><label><input id="mute" type="checkbox" ${progress.prefs.mute ? 'checked' : ''}> 音を消す</label><label><input id="motion" type="checkbox" ${progress.prefs.reducedMotion ? 'checked' : ''}> 演出を控えめにする</label></div><div class="overlay-actions">${button('戻る', 'settings-back', true)}</div>`);
  if (next === 'credits') overlay.innerHTML = panel('クレジット', 'Replay Partner / リプレイ・パートナー', `<p>企画・制作協力: 田中 優輝 / Yuki Tanaka<br>開発支援: OpenAI Codex<br>画像・音: ゲーム内で描画、Web Audioで生成。第三者素材は使用していません。</p><p class="micro">制作過程と本人の担当範囲は付属資料をご覧ください。</p><div class="overlay-actions">${button('戻る', 'back', true)}</div>`);
  if (next === 'playing') { overlay.innerHTML = ''; focusBoard(); }
  renderStatus();
}

function loadStage(index: number): void {
  stageIndex = index;
  sim = new Simulation(stages[index]!);
  hintLevel = 0;
  document.querySelector('#stage-heading')!.textContent = `STAGE ${String(index + 1).padStart(2, '0')} / ${stages[index]!.name}`;
  document.querySelector('#mission-name')!.textContent = stages[index]!.name;
  document.querySelector('#mission-goal')!.textContent = stages[index]!.goal;
  feedback.textContent = stages[index]!.intro;
  scene.renderBoard(sim); show('playing');
}

function renderStatus(): void {
  const state = sim.state;
  document.querySelector('#status-heading')!.textContent = screen === 'playing' ? (state.mode === 'recording' ? '● 記録中' : '▶ 再生中') : '待機中';
  document.querySelector('#mode-value')!.textContent = state.mode === 'recording' ? '● 記録中' : state.mode === 'cleared' ? '✓ クリア' : '▶ 再生中';
  document.querySelector('#remaining-value')!.textContent = `記録可能時間 ${(state.remaining / 5).toFixed(1)} 秒`;
  (document.querySelector('#time-meter') as HTMLElement).style.width = `${state.remaining / 150 * 100}%`;
  document.querySelector('#partners-value')!.textContent = `${sim.recordingsCount} / 2`;
  document.querySelector('#partner-detail')!.textContent = state.clones.length ? state.clones.map((c, i) => `分身${i + 1}: ${c.blocked ? '停止' : c.done ? '待機' : '再生中'}`).join(' / ') : 'まだ分身はいません';
  const recordButton = document.querySelector<HTMLButtonElement>('#record-button')!;
  recordButton.textContent = state.mode === 'recording' ? 'E　記録を確定' : 'E　記録を始める';
  recordButton.disabled = screen !== 'playing' || state.mode === 'cleared' || (state.mode === 'replay' && sim.recordingsCount >= 2);
  document.querySelector<HTMLButtonElement>('[data-action="undo"]')!.disabled = screen !== 'playing' || sim.recordingsCount === 0 || state.mode === 'recording';
  document.querySelector<HTMLButtonElement>('[data-action="cancel"]')!.disabled = screen !== 'playing' || state.mode !== 'recording';
  document.querySelector<HTMLButtonElement>('[data-action="retry"]')!.disabled = screen !== 'playing';
  document.querySelector<HTMLButtonElement>('[data-action="restart"]')!.disabled = screen !== 'playing';
  document.querySelector<HTMLButtonElement>('[data-action="hint"]')!.disabled = screen !== 'playing';
}

function step(): void {
  if (screen !== 'playing' || document.hidden || document.activeElement !== board.querySelector('canvas')) return;
  const state = sim.step(held ?? 'wait');
  scene.renderBoard(sim); renderStatus();
  if (state.feedback) feedback.textContent = state.feedback;
  if (state.mode === 'cleared') {
    const id = stages[stageIndex]!.id;
    if (!progress.cleared.includes(id)) progress.cleared.push(id);
    progress.unlocked = Math.min(10, Math.max(progress.unlocked, id + 1)); save(); beep(660, 0.25); show('clear');
  }
}
setInterval(step, 200);

function act(action: string): void {
  if (action === 'new') loadStage(0);
  else if (action === 'continue') loadStage(Math.min(9, progress.unlocked - 1));
  else if (action === 'stage') show('select');
  else if (action === 'select') show('select');
  else if (action === 'help') { previous = screen; show('help'); }
  else if (action === 'settings') { previous = screen; show('settings'); }
  else if (action === 'settings-back' || action === 'back') show(previous === 'settings' ? 'title' : previous);
  else if (action === 'credits') { previous = screen; show('credits'); }
  else if (action === 'title') show('title');
  else if (action === 'resume') show('playing');
  else if (action === 'next') { if (stageIndex === 9) show('complete'); else loadStage(stageIndex + 1); }
  else if (action === 'record' && screen === 'playing') {
    const changed = sim.state.mode === 'recording' ? sim.commit() : sim.begin();
    if (changed) { beep(480); scene.renderBoard(sim); renderStatus(); feedback.textContent = sim.feedback; focusBoard(); }
  } else if (action === 'retry' && (screen === 'playing' || screen === 'pause' || screen === 'clear')) { sim.retry(); scene.renderBoard(sim); show('playing'); feedback.textContent = '分身を保持して、最初から再生します'; }
  else if (action === 'undo' && screen === 'playing') { sim.undo(); scene.renderBoard(sim); renderStatus(); feedback.textContent = '最後の分身を削除しました'; focusBoard(); }
  else if (action === 'cancel' && screen === 'playing') { sim.cancel(); scene.renderBoard(sim); renderStatus(); feedback.textContent = '記録をキャンセルしました'; focusBoard(); }
  else if (action === 'restart' && screen === 'playing') { sim.restart(); scene.renderBoard(sim); renderStatus(); feedback.textContent = '部屋と分身を初期化しました'; focusBoard(); }
  else if (action === 'hint' && screen === 'playing') { hintLevel = Math.min(3, hintLevel + 1); feedback.textContent = `${hintLevel}/3　${stages[stageIndex]!.hints[hintLevel - 1]}`; focusBoard(); }
}

app.addEventListener('click', e => {
  const target = e.target as HTMLElement;
  const stageButton = target.closest<HTMLButtonElement>('[data-stage]');
  if (stageButton && !stageButton.disabled) { loadStage(Number(stageButton.dataset.stage)); return; }
  const actionButton = target.closest<HTMLButtonElement>('[data-action]');
  if (actionButton && !actionButton.disabled) act(actionButton.dataset.action!);
});
app.addEventListener('input', e => { const t = e.target as HTMLInputElement;
  if (t.id === 'volume') { progress.prefs.volume = Number(t.value) / 100; document.querySelector('#volume-output')!.textContent = `${t.value}%`; save(); }
  if (t.id === 'mute') { progress.prefs.mute = t.checked; save(); }
  if (t.id === 'motion') { progress.prefs.reducedMotion = t.checked; document.body.classList.toggle('reduce-motion', t.checked); save(); }
});

const keyCommand: Record<string, Command> = { w: 'up', ArrowUp: 'up', s: 'down', ArrowDown: 'down', a: 'left', ArrowLeft: 'left', d: 'right', ArrowRight: 'right', ' ': 'wait' };
window.addEventListener('keydown', e => {
  if (screen !== 'playing' || document.activeElement !== board.querySelector('canvas')) return;
  const command = keyCommand[e.key];
  if (command) { e.preventDefault(); held = command; return; }
  if (e.repeat) return;
  if (['e', 'r', 'Backspace', 'Escape'].includes(e.key)) e.preventDefault();
  if (e.key === 'e') act('record');
  if (e.key === 'r') act('retry');
  if (e.key === 'Backspace') act('undo');
  if (e.key === 'Escape') show('pause');
});
window.addEventListener('keyup', e => { if (keyCommand[e.key] === held) held = null; });
window.addEventListener('blur', () => { held = null; if (screen === 'playing') show('pause'); });
document.addEventListener('visibilitychange', () => { held = null; if (document.hidden && screen === 'playing') show('pause'); });
document.body.classList.toggle('reduce-motion', progress.prefs.reducedMotion || matchMedia('(prefers-reduced-motion: reduce)').matches);
show('title');
