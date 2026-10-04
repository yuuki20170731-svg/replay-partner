# 公開準備

ローカルGitリポジトリは初期化済み。GitHubへのpushとCloudflareへの公開は未実施。GitHub CLIの保存済み認証は無効、Wranglerは未認証。公開URLはない。`wrangler deploy --dry-run` は成功した。

`main` を本番用、機能追加は別ブランチでレビューする方針。`.github/workflows/ci.yml` はpush/PR時のlint、型検査、テスト、ビルドを定義しているが、GitHub上では未稼働。新規リポジトリはPrivateで確認後、Public化する。本人は2026-10-04に氏名・所属を含む一般公開を承認済み。

CloudflareはWorkers Static Assetsを利用。`wrangler.jsonc` が `./dist` を配信する。バックエンド、KV、D1は不要。Cloudflare側でGitHub接続後、ビルドコマンド `pnpm install --frozen-lockfile && pnpm build`、本番デプロイコマンド `pnpm exec wrangler deploy`、PreviewはCloudflareのブランチPreviewを設定する。実際に接続する際はダッシュボードの表示と公式手順を再確認する。

確認順: Previewで紹介ページ、ゲーム、記録/再生、保存、スマートフォン幅の案内を確認→mainへ反映→本番URLを再確認。戻すときはCloudflareの以前の動作版を選び、Gitの変更も元に戻す。実際の手順は公開後に記録する。

2026-10-04に確認した公式資料: [Static Assets設定](https://developers.cloudflare.com/workers/static-assets/binding/)、[Wrangler設定](https://developers.cloudflare.com/workers/wrangler/configuration/)、[Workers Builds](https://developers.cloudflare.com/workers/ci-cd/builds/)、[GitHub連携](https://developers.cloudflare.com/workers/ci-cd/builds/git-integration/github-integration/)、[Previewブランチ](https://developers.cloudflare.com/workers/ci-cd/builds/build-branches/)。無料枠・条件は公開時にアカウント画面と公式の現行案内で再確認する。
