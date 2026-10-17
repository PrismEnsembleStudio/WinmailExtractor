# 開発への参加

不具合報告、改善提案、Pull Requestを歓迎します。実際の業務メールや個人情報はリポジトリ・Issue・PRへ投稿しないでください。

## ローカルでの確認

Windows x64と、`global.json` に指定した.NET SDKを使用します。

```powershell
dotnet restore WinmailExtractor.sln --locked-mode --disable-parallel -m:1 -nr:false
dotnet build WinmailExtractor.sln -c Release --no-restore -m:1 -nr:false
dotnet test tests\WinmailExtractor.Tests -c Release --no-build --no-restore
```

解析・保存の変更には、合成TNEFによるバイト一致や、不正な入力・ファイル名を扱うテストを含めてください。通常のテストは、本番の右クリック登録先を変更せず、一時専用レジストリ領域だけを使用します。

右クリック連携やDPI、ダイアログなどは、`docs/MANUAL-TESTS.md` の手順で確認し、実施したものと未実施のものをPRに記載してください。

初回プレビュー版はWindows 11実機での受入確認が残っています。確認できたOS、倍率、操作、期待結果を明記した報告が役立ちます。

## 配布版の発行

`publish.ps1` に新しい空の出力先を指定します。通常用と発行用で依存ロックを分けています。SDK・依存の変更時は両方を更新し、ビルド、テスト、単一EXEの確認を行ってください。

ソースのライセンスは `LICENSE`、依存ソフトウェアの通知は `docs/THIRD-PARTY-NOTICES.txt` を参照してください。
