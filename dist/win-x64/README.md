# ビルドせずに使う

このフォルダーの `WinmailExtractor.exe` は、Windows x64用のランタイム同梱ポータブル版です。Git管理の対象に含めています。

**[EXEを直接ダウンロード](https://github.com/PrismEnsembleStudio/WinmailExtractor/raw/refs/heads/main/dist/win-x64/WinmailExtractor.exe)**

ダウンロードして任意のローカルフォルダーに置き、起動してください。インストーラー、.NET SDK、追加のDLLは不要です。初回プレビュー版で、未署名です。

ファイルを選択して添付を確認し、必要なものだけ保存します。右クリック連携は右上の設定から登録・解除してください。Windows 11では「その他のオプションを確認」側を想定しています。

サイズは62,100,304 bytes（約59.2 MiB）。単一EXE内部の圧縮を有効にしています。.NET/WPFは実行時にWindowsの一時フォルダーへ展開されます。

SHA-256は同梱の `SHA256SUMS.txt` を参照してください。

```powershell
Get-FileHash .\WinmailExtractor.exe -Algorithm SHA256
```

[使い方・制限](../../README.md)、[検証範囲](../../docs/VALIDATION.md)、[MITライセンス](../../LICENSE)も確認してください。本体のMIT通知と第三者ソフトウェアの通知はEXE内にも収録しています。

安定した配布用URLと過去版は [GitHub Releases](https://github.com/PrismEnsembleStudio/WinmailExtractor/releases) から取得できます。
