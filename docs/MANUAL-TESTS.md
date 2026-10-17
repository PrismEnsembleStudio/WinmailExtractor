# Windows実機の受入確認・復旧

対象は配布用の `dist/win-x64/WinmailExtractor.exe`。Windows 11 x64で実施してください。次の項目は自動描画テストだけでは確認できません。実施結果は `VALIDATION.md` に分けて記録します。

## 通常操作

1. EXEを置き場所の決まったフォルダーに配置して起動。ランタイムが入っていないWindowsでも起動できることを確認。
2. `artifacts/verification/sample-winmail.dat` をドロップし、4件の一覧を確認。これはテストツールが合成した検証用TNEFで、実際の業務メールではありません。
3. Tab / Shift+Tab、Spaceでチェック変更。Alt+Aで全選択、Alt+Nで解除、Ctrl+Oまたは選択ボタンで別ファイル。ダイアログのキャンセルで元の一覧を維持することを確認。
4. 2件だけ選択して保存。フォルダー選択のキャンセル、保存完了、保存先を開く、既存同名ファイルを残して採番保存を確認。
5. 不正DAT、途中で切ったコピー、存在しないパス、複数引数、50 MiB超のファイルを試す。元ファイルが変わらず、エラーを表示して再操作できることを確認。
6. 権限のない入力・保存先、空き容量不足で落ちずに説明し、途中保存の場合は件数が表示されることを確認。
7. 実メールのTNEFを端末内でのみ試す場合は、元の添付のハッシュと保存後のSHA-256を比較。業務メールをリポジトリや外部サービスへ送信しない。

## 右クリック連携（ユーザーが設定画面から実行）

事前に現在の登録を読み取り、既存キーがある場合はバックアップする。他のアプリのキーや `.dat` の既定関連付けを変更しない。

```powershell
reg query "HKCU\Software\Classes\SystemFileAssociations\.dat\shell\WinmailExtractor" /s
# キーが元から存在する場合のみ実行。既存ファイルへ上書きしない保存名を選ぶ。
reg export "HKCU\Software\Classes\SystemFileAssociations\.dat\shell\WinmailExtractor" winmail-menu-backup.reg
```

1. 設定で「未登録」を確認し、「登録 / 再登録」を押す。
2. `.dat` の右クリック→「その他のオプションを確認」→「Winmail Extractorで開く」で、指定ファイルが読み込まれることを確認。
3. 関係のない拡張子にメニューが追加されず、`.dat` のダブルクリックで既定アプリが変わっていないことを確認。
4. 空白・日本語・`&`・括弧を含むフォルダーでも登録と起動を確認。EXEパスに `%` を含む場合は説明して拒否することを確認。
5. EXEを移動して新しいEXEを起動。「再登録が必要」を確認し、再登録後の右クリック起動を確認。
6. 設定の「解除」を押す。メニューがなくなり、既定関連付けや他のメニューが残っていることを確認。

自動テストは `HKCU\Software\WinmailExtractor.Tests-<GUID>` の一時キーで登録・検出・再登録状態・解除・他キー保護を試験します。実際のShell登録先や通知には触れず、終了時にその一時キーだけを削除します。

### EXEを削除してしまった場合の復旧

同じEXEを再配置して設定から解除する方法を優先する。手動削除する場合は次の専用キーの `Owner` が `WinmailExtractor.Portable.v1` であることを確認してから、このキーだけを削除する。

```powershell
reg query "HKCU\Software\Classes\SystemFileAssociations\.dat\shell\WinmailExtractor" /v Owner
# 上記の値を確認した場合のみ実行する。
reg delete "HKCU\Software\Classes\SystemFileAssociations\.dat\shell\WinmailExtractor" /f
```

上位の `.dat`、`shell`、`SystemFileAssociations` や他のキーを削除しない。手動削除後に表示が残るときはエクスプローラーを閉じて開き直すか、Windowsへサインインし直す。元から存在した登録を戻す場合は、内容を確認した自分のバックアップのみをインポートする。

## DPI・見た目

Windowsの表示倍率を125%・150%・200%へ変更し、必要ならサインインし直して確認する。異なるDPIのモニター間も移動する。

- 初期サイズと最小720×500でヘッダー、保存ボタン、設定の登録・解除が欠けない。
- 長い日本語名は一覧に収まり、ツールチップで確認できる。
- 添付が多数でもスクロールでき、フォーカスが見え、キーボードだけで主要操作ができる。
- ファイル選択・フォルダー選択・Explorerの動作は実際のOSダイアログで確認する。

描画検証ツールの125〜200% PNGは描画解像度の比較であり、OSのDPI変更やモニター間移動の試験を代替しない。
