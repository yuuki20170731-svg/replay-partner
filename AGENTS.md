# Replay Partner 作業方針

- 目的: ブラウザで遊べる10ステージの時間差協力パズルと、作品紹介ページ・就活資料を完成する。別作品は触らない。
- 技術: TypeScript、Phaser 3、Vite。盤面判定は `src/engine/`、表示は `src/game/`。保存はlocalStorage。バックエンドなし。
- 起動: `pnpm install` → `pnpm dev`。検証: `pnpm lint && pnpm typecheck && pnpm test && pnpm build`。
- ルール: 200ms刻み。古い分身→新しい分身→現在の自分の順に行動し、各行動後に扉を更新する。記録を確定すると盤面を初期化し、全分身を時刻0から再生する。記録中の出口到達はクリアしない。
- GitHub: 2026-10-04に本人が氏名・所属を含む一般公開を承認。`https://github.com/yuuki20170731-svg/replay-partner` はPublic。CIは `.github/workflows/ci.yml` で実際に成功。
- Cloudflare: `wrangler.jsonc` のStatic Assetsを使う。公開範囲は本人確認済み。Git接続と本番公開は本人のアカウント認証後。代替ホストへ無断変更しない。
- 説明: 初心者に用語を短く説明し、実施済み・未実施を分ける。本人の経験やテスト結果を捏造しない。
- 秘密情報: トークン等をコード・チャット・Gitに入れない。環境変数が不要なので現状 `.env` は使わない。
- 資料: `README.md` と `docs/`。進行状況は `docs/STATUS.md` を更新する。
