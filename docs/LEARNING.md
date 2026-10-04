# 開発を追うための短いメモ

- Git: 変更履歴を残す道具。GitHubは、その履歴をオンラインで共有する場所。
- ブランチ: 主な完成版と別に、変更を試す作業線。PRは変更を確認して統合するための提案。
- ビルド: TypeScript等をブラウザ配信用ファイルへまとめる処理。`dist/` が結果。
- CI: GitHubに変更を送ったときに検証を自動実行する仕組み。設定は作成済みだが、GitHub上の実行は未確認。
- デプロイ: ビルド結果を公開先へ置く作業。Cloudflareは未接続。

読む順番: `src/engine/stages.ts` で盤面→`simulation.ts` の `step`、`begin`、`commit`→`tests/simulation.test.ts` の解法→`src/game/BoardScene.ts` の表示→`src/game/main.ts` の入力・画面。

試す小さな改修: ステージ2のヒント文章を変更→ `pnpm test` と `pnpm build` を実行→ローカルで画面を確認。本人が理解した、習得したという評価はまだ行っていない。
