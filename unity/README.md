# Replay Partner — Unity版

最新のWindows配布候補は `Build/Windows-RC6/ReplayPartner.exe`。正面／背面の探索者画像と歩行切替を実装した10部屋の時間差協力パズルです。全10部屋の手動通しプレイと新版動画は本人の希望で保留。第三者・別PCテストは未実施です。未確認事項を含むため、正式完成とは記載しません。

## 起動と開発

プレイはexeを同梱フォルダのまま起動してください。操作・保存・素材は [DISTRIBUTION.md](DISTRIBUTION.md)、実施済み／未実施は [QA.md](QA.md)、設計は [TECHNICAL.md](TECHNICAL.md) にまとめています。

Unity Hubでこのフォルダを追加し、Editor `6000.0.60f1` で開き、Main.unityを再生します。シーンがない場合は Replay Partner > Setup Project。タイトルでEnter／はじめるから開始します。

## 追加した内容

自由・斜め移動、最大2分身の入力再生、記録取消、鍵、箱、門、予告付き罠。キャラは左右の手足を独立に動かすスタイライズ表現。スイッチの沈み、門の上昇、鍵・被弾の光と効果音、記録した足跡と停止地点、4つの景観テーマを実装。設定には音量・画面サイズ・7操作のキー変更・演出軽減。リザルトには挑戦時間・再挑戦回数・分身数、各部屋のBESTと記録合計を表示します。

## テストとビルド

Unityメニュー Replay Partner > Run Smoke Tests / Run Presentation Tests / Build Windows を使用します。コマンドラインでも実行できます。

```powershell
unity run . -- -executeMethod ReplayPartner.Editor.ReplaySmokeTests.Run
unity run . -- -executeMethod ReplayPartner.Editor.ReplayPresentationTests.Run
unity run . -- -executeMethod ReplayPartner.Editor.ReplayProjectSetup.BuildWindows
```

全10部屋のロジックテストと、2分身・足跡付き全10部屋／各メニューの画面生成テストは成功。実画面では部屋2・斜め移動・記録・確定・停止地点・設定・キー保存と復元を確認。自動テストを手動クリアの完了とは扱いません。

Windows版は操作・技術・検証・フォントライセンス文書を同梱します。旧ビルドは保持。WebGLはWeb Build Supportの管理者権限エラーで未導入のため、ビルド・公開は未検証。既存Phaser版は削除・置換していません。

## 素材

Noto CJKフォントはSIL OFL 1.1、全文は `Assets/Resources/NotoSans-LICENSE.txt`。Dungeon*.pngの8点は本作用にOpenAI画像生成で作成。最新の盤面キャラはUIパーツで描画し、旧主人公画像も素材として保持。効果音はコード生成です。
