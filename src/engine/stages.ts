export type Point = { x: number; y: number };
export type Stage = {
  id: number; name: string; goal: string; lesson: string; hints: [string, string, string];
  map: string[]; intro: string;
};

const blank = (): string[][] => Array.from({ length: 7 }, (_, y) => Array.from({ length: 16 }, (_, x) =>
  x === 0 || x === 15 || y === 0 || y === 6 ? '#' : '.'));

function stage(id: number, name: string, goal: string, lesson: string, hints: [string, string, string], intro: string,
  switches: { a?: Point; b?: Point } = {}, barriers: number[] = [], crate?: Point, exit: Point = { x: 13, y: 3 }): Stage {
  const grid = blank();
  grid[3]![2] = 'P';
  grid[exit.y]![exit.x] = 'E';
  for (const [i, x] of barriers.entries()) {
    for (let y = 1; y <= 5; y++) grid[y]![x] = '#';
    grid[3]![x] = i === 0 ? 'a' : 'b';
  }
  if (switches.a) grid[switches.a.y]![switches.a.x] = 'A';
  if (switches.b) grid[switches.b.y]![switches.b.x] = 'B';
  if (crate) grid[crate.y]![crate.x] = 'C';
  return { id, name, goal, lesson, hints, intro, map: grid.map(row => row.join('')) };
}

export const stages: Stage[] = [
  stage(1, '起動室', '右側の出口へ進む', '移動', ['WASD / 矢印キーで移動できます。', '光る出口を目指してください。', '記録はまだ必要ありません。'], 'ここには、昨日のあなたが残した足跡がある。', {}, [], undefined, { x: 7, y: 3 }),
  stage(2, '片方の手', '分身に A を踏ませ、扉を通る', '記録と再生', ['E で記録開始。A の上まで移動します。', 'もう一度 E で確定すると、部屋が巻き戻ります。', '分身が A に残る間に、現在の自分で扉を通ります。'], 'ひとりでは届かない場所も、過去の自分となら。', { a: { x: 3, y: 5 } }, [7]),
  stage(3, '少し先の未来', '遠いスイッチを踏む行動を記録する', '待機', ['記録中に上側の A を目指します。', '確定後、分身が A に着くまで Space で待てます。', '扉が開いたら進みます。'], '急がなくていい。過去のあなたも、いま向かっている。', { a: { x: 5, y: 1 } }, [7]),
  stage(4, '重さの記憶', '箱を A に押して扉を開ける', '箱', ['箱の隣から押せます。', '箱を右へ押して A に載せます。', '開いた扉を通って出口へ。'], '残せるのは足跡だけじゃない。', { a: { x: 5, y: 4 } }, [7], { x: 4, y: 4 }),
  stage(5, '受け渡し', '分身と箱で二つの扉を開く', '分身と箱', ['最初の分身を左側の A に立たせます。', '記録確定後、中央の箱を右へ押し B に載せます。', '二つの扉が開いたら出口へ。'], '受け取った時間を、次へ渡そう。', { a: { x: 3, y: 5 }, b: { x: 10, y: 4 } }, [7, 11], { x: 9, y: 4 }),
  stage(6, 'ふたりの記録', '二体の分身で A と B を同時に踏む', '分身2体', ['一体目を A に記録します。', '二回目の記録では、一体目が再生されます。B へ向かいます。', '二体目を確定したら、現在の自分で出口へ。'], '二度目の自分は、一度目の自分を信じられる。', { a: { x: 3, y: 5 }, b: { x: 9, y: 5 } }, [7, 11]),
  stage(7, '異なる道', '離れた A と B を分担する', '経路の計画', ['A は左上、B は中央上です。', '一体目を A、二体目を B に記録します。', '扉の開く順番を見て進みます。'], '同じ場所から始めても、選ぶ道は変えられる。', { a: { x: 5, y: 1 }, b: { x: 9, y: 1 } }, [7, 11]),
  stage(8, '静かな秒針', '待機を使って扉の開く時刻を合わせる', 'タイミング', ['A は右下、B は中央上です。', '二体目が B に着くまで、扉の手前で待ちます。', 'Space を使って再生の時刻を進めましょう。'], '待つことも、行動のひとつ。', { a: { x: 5, y: 5 }, b: { x: 10, y: 1 } }, [7, 11]),
  stage(9, '箱の向こう', '分身が支える間に箱を使う', '複合', ['A の分身を作り、最初の扉を開けます。', '中央の箱を B に載せます。', '押す向きを確かめてから進みましょう。'], '手を放したあとも、役目は残る。', { a: { x: 4, y: 1 }, b: { x: 10, y: 4 } }, [7, 11], { x: 9, y: 4 }),
  stage(10, 'リプレイ・パートナー', '二体の分身とともに施設を出る', '総合', ['一体目を A に、二体目を B に記録します。', '二体目の記録では一体目の再生も進みます。', '二つの扉の先に出口があります。'], '振り返れば、いつもあなたが隣にいた。', { a: { x: 4, y: 5 }, b: { x: 9, y: 1 } }, [7, 11])
];

export const at = (a: Point, b: Point): boolean => a.x === b.x && a.y === b.y;
export const keyOf = (p: Point): string => `${p.x},${p.y}`;
