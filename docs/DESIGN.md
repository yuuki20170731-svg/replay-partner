# 画面・体験設計

画面: タイトル、遊び方、ステージ選択、ゲーム、ポーズ、クリア、全クリア、設定、クレジット。紹介ページはゲームとは別入口で、ゲームの重い描画コードを初回読み込みしない。

初回はステージ1で移動を学び、2で記録/再生、3で待機、4で箱、5で分身と箱、6で分身2体。7〜10で位置・順序・時間差の応用。各ステージの名前、目的、3段階のヒント、盤面、テスト用解法は `src/engine/stages.ts` と `tests/simulation.test.ts` にある。

色: 背景 `#07151e`、盤面 `#102633`、文字 `#e8f3f4`、アクセント `#60ded0`、現在の自分 `#f1d18c`、分身 `#65c8f7`。色と番号・A/B文字を併用。パネルとボタンの境界線、focus-visible、disabled状態を統一。本文は日本語可読性優先。小さい画面では紹介ページの情報を縦方向に整理し、ゲームはPC推奨と表示する。

動きはボタンの短い変化と盤面の状態更新に絞る。`prefers-reduced-motion` と手動設定でCSSのアニメーションを抑える。音はクリック後のWeb Audioで生成し、使用不可なら無音のまま進める。

参考調査（2026-10-04確認、公式資料のみ）:

- [Phaser Scale Manager](https://docs.phaser.io/phaser/concepts/scale-manager): Canvasを親領域に合わせる設計。
- [Phaser Getting Started](https://docs.phaser.io/phaser/getting-started/making-your-first-phaser-game): Phaser 3シーンの基本構成。
- [Cloudflare Static Assets](https://developers.cloudflare.com/workers/static-assets/get-started/): 静的サイトとして配信する構成。

他作品の盤面や文章を模倣せず、仕掛けとレイアウトは本プロジェクト用に作成した。
