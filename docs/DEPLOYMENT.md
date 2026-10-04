# 公開準備

GitHubリポジトリは [yuuki20170731-svg/replay-partner](https://github.com/yuuki20170731-svg/replay-partner) として2026-10-04にPrivateで作成し、本人の一般公開許可に従ってPublic化した。`main` へpush済み。GitHub ActionsのVerifyは成功。Cloudflareは未認証・未公開で公開URLはまだない。通常OAuthとデバイス認証の両方が承認前にタイムアウトした。再開時は本人がブラウザを操作できる状態で `pnpm exec wrangler login --device` を実行し、表示されるコードを5分以内にCloudflareの公式画面へ入力・承認する。`wrangler deploy --dry-run` は成功した。

`main` を本番用、機能追加は別ブランチでレビューする方針。`.github/workflows/ci.yml` はpush/PR時のlint、型検査、テスト、ビルドを定義し、[初回実行](https://github.com/yuuki20170731-svg/replay-partner/actions/runs/37187283168)は成功。Public化は本人が2026-10-04に承認済み。

CloudflareはWorkers Static Assetsを利用。`wrangler.jsonc` が `./dist` を配信し、`previews` ブロックも定義する。バックエンド、KV、D1は不要。ログイン後、まず `pnpm exec wrangler preview --name review` でPreviewを作り、動作確認後に `pnpm exec wrangler deploy` で本番公開する。Cloudflare側でGitHub接続する場合のビルドコマンドは `pnpm install --frozen-lockfile && pnpm build`。そのGit連携はダッシュボードで未設定であり、自動デプロイ済みとは扱わない。

確認順: Previewで紹介ページ、ゲーム、記録/再生、保存、スマートフォン幅の案内を確認→mainへ反映→本番URLを再確認。戻すときはCloudflareの以前の動作版を選び、Gitの変更も元に戻す。実際の手順は公開後に記録する。

2026-10-04に確認した公式資料: [Static Assets設定](https://developers.cloudflare.com/workers/static-assets/binding/)、[Wrangler設定](https://developers.cloudflare.com/workers/wrangler/configuration/)、[Workers Builds](https://developers.cloudflare.com/workers/ci-cd/builds/)、[GitHub連携](https://developers.cloudflare.com/workers/ci-cd/builds/git-integration/github-integration/)、[Worker Previews](https://developers.cloudflare.com/workers/previews/get-started/)。無料枠・条件は公開時にアカウント画面と公式の現行案内で再確認する。
