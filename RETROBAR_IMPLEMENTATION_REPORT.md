# 指定モニター単独表示の実装・検証報告

作業日: 2026-10-24（日本時間）

ベースコミット: `69f36278150cb39bc37d3237e3178926734c6804`

開始時の変更は未追跡の `RETROBAR_CODEX_TASK.md` のみ。指示書は変更していない。

## 変更したファイル

| ファイル | 内容 |
|---|---|
| `RetroBar/Utilities/Settings.cs` | 空文字既定の `TargetMonitorDeviceName` と `StartMenuMonitorDeviceName` を追加。null も空文字に正規化。旧 `ShowMultiMon` と既存設定は維持。 |
| `RetroBar/Utilities/MonitorSelection.cs` | All / Primary / Specific の選択を UI と画面列挙から分離。保存するのはデバイス名で、配列インデックスではない。対象不在時はプライマリを選び、設定は変更しない。 |
| `RetroBar/Utilities/WindowManager.cs` | 選択された画面だけにバーを生成。設定変更時も再生成。表示通知と再生成要求を Dispatcher でまとめ、処理中の再入と重複生成を防止。終了後の予約処理は無効化。 |
| `RetroBar/PropertiesWindow.xaml` / `.xaml.cs` | 既存の複数画面チェックを維持して「単独表示先」と「スタートメニューの表示先」を追加。未接続の保存先も保持。各タブをスクロール対応とし、ウィンドウサイズ・位置を表示元の作業領域内に制限。閉じる際には購読を解除。 |
| `RetroBar/Taskbar.xaml.cs` | `IsMainTaskbar` により単独バーの役割と物理プライマリを区別。トレイ位置・テーマ・言語・AutoHide・DPI の更新を単独サブモニターにも対応。ManagedShell が非プライマリから上位に渡さない DISPLAYCHANGE / DEVICECHANGE を補完。 |
| `RetroBar/Controls/TaskList.xaml.cs` | Win + 数字のタスク切替を唯一のバーでも処理。既存の `!ShowMultiMon` による全画面のタスク集約はそのまま利用。 |
| `RetroBar/Controls/TaskButton.xaml.cs` | 単独サブモニター上でもウィンドウアニメーション用のボタン矩形を返す。All の優先ルールは維持。 |
| `RetroBar/Controls/NotifyIcon.xaml.cs` | 単独サブモニター上でも通知バルーンを表示する。クリック処理は既存の ManagedShell 経路を利用。 |
| `RetroBar/Controls/Toolbar.xaml.cs` | クイック起動用の Win + 数字も唯一のバーで処理。 |
| `RetroBar/Controls/StartButton.xaml.cs` | 既存のモニター指定メニュー表示処理を使用。別モニターに開いた場合もクリック元のバーで表示状態を受け取る。 |
| `RetroBar/Utilities/StartMenuMonitor.cs` | タスクバーとは独立したメニュー表示先を解決し、既存の IImmersiveMonitorManager / ConnectToMonitor に渡す。既存のクラシックメニュー配置も指定先の作業領域へ対応。 |
| `RetroBar/Languages/English.xaml` / `日本語.xaml` | 新規 UI の英語・日本語リソース。その他の言語は既存の英語フォールバックを利用。 |
| `RetroBar/Themes/Windows Vista Basic.xaml` | ユーザーの指定により、縦バーの背景と左側の明るい枠線を暗色へ統一。横向けの配色は維持。 |
| `RetroBar.Tests/RetroBar.Tests.csproj` / `Program.cs` / `README.md` | 実際の選択・設定コードをリンクする自動回帰テスト。追加テストフレームワークなし。ユーザー設定には触れず、専用一時ディレクトリで保存と読込を検証。 |
| `RETROBAR_IMPLEMENTATION_REPORT.md` | 本報告書。 |

ManagedShell のバージョン、トレイサービス、ウィンドウ列挙、Explorer の停止・注入などは変更していない。Explorer 再起動時は既存の `ExplorerMonitor.ReopenTaskbars()` から同じ選択処理へ戻る。

## 設定画面での操作

1. RetroBar を右クリックして「プロパティ」を開く。
2. 「詳細設定」→「複数のディスプレイ」を開く。
3. 「タスクバーを複数のディスプレイに表示する」のチェックを外す。
4. 「単独表示先」で `\\.\DISPLAY2` など目的のデバイス名・解像度の画面を選ぶ。
5. 「タスクバー」タブの「位置」で「左」または「右」を選ぶと縦配置。「上」「下」も既存経路を使用する。

Primary に戻す場合は「単独表示先」で「プライマリ」を選ぶ。All は既存の複数画面チェックを入れる。All の間は単独表示先を保持し、チェックを外すとその選択へ戻る。

時計・クイック起動・入力言語・デスクトップ表示ボタンは既存設定で非表示にできる。単独表示では、非プライマリでもスタートボタンを表示する。`ShowStartButtonMultiMon` は All モードの追加バーにのみ適用する。

### スタートボタン非表示の修正（2026-10-24）

ユーザーから単独サブモニター上でスタートボタンが出ないとの報告があった。`UpdateStartButton` に物理プライマリ限定の条件が残っており、既定値 false の `ShowStartButtonMultiMon` によって単独バーでも非表示になっていた。条件を `IsMainTaskbar` に変更し、単独表示なら必ず表示するよう修正した。その後、ユーザーからスタートボタンが表示されたことを確認できた。メニュー位置は実機未検証。

### スタートメニューの表示先の選択（2026-10-24）

「詳細設定」→「複数のディスプレイ」に「スタートメニューの表示先」を追加した。「タスクバーと同じ」（既定）またはデバイス名・解像度付きの検出済み画面から選択する。タスクバーのない画面も選択できる。選択は保存され、次に RetroBar のスタートボタンから開く際に反映される。All / Primary / Specific のタスクバー配置とは独立している。

`StartMenuMonitorDeviceName` が空文字ならクリック元タスクバーの HMONITOR、値があれば該当画面の HMONITOR を既存のメニュー表示 API に渡す。対象不在ならプライマリを使い、保存値は消去しない。次回クリック時に再列挙するため、再接続後は指定先を再解決する。既定へ戻した際も直前の別画面指定に引きずられないよう、プライマリ上のボタンも既存のモニター指定経路から開く。

適用対象は RetroBar のスタートボタン（既存のドラッグで開く経路を含む）。Windows キーや Ctrl + Esc などから開く操作のモニターは変更しない。既に開いているメニューを設定変更だけで移動する機能は含まない。Windows の既存 COM API が失敗した場合は従来の表示処理へフォールバックするため、選択した画面に表示されるかは実機確認が必要。標準メニュー・Open-Shell・StartIsBack などの表示位置試験は今回未実施。

### 縦バーの明るい帯の修正（2026-10-24）

添付画像の左側の明るい帯は、Vista Basic テーマの縦向け背景（幅の約17%を使う光沢グラデーション）と明るい内枠に一致した。背景と枠線を WPF で 150% DPI・432×133 ピクセルに描画して同じ帯を再現できた。ユーザーの「縦バーの背景を暗色で統一する」という選択に従い、背景を `#1C1E1F` の単色へ変更し、明るい縦枠も暗色に揃えた。対象は画像に一致した Vista Basic テーマ。横向けと他テーマは変更していない。

修正後の背景・枠線も WPF で描画し、明るい帯が消えることを目視確認した。横向けの背景・枠線リソースが変更されていないことも確認。両フレームワークのビルド成功（この差分の最終ビルドでは既存の NETSDK1138 警告1件、エラー0件）、出力先のテーマとソースの SHA-256 一致、`git diff --check` 成功。実際のデスクトップ上の表示は未確認。

### 設定画面の下部が切れる問題の修正（2026-10-24）

添付画像では「詳細設定」の最下部が欠けていた。通知領域・詳細設定の内容の高さを最初のタブの `ActualHeight` に固定していたため、追加した設定がその領域に収まらなかった。高さの固定を削除し、3つのタブをそれぞれ ScrollViewer で囲んだ。OK ボタンはスクロール対象の外に維持。ウィンドウの最大サイズを表示元モニターの作業領域と DPI に基づいて制限し、サイズ変更時も位置を作業領域内へ補正する。

本番 XAML の詳細設定レイアウトから、設定へのバインディングとイベント処理を外した独立プレビューを作成し、150% 相当で描画した。高さ600 / 400 / 300 DIP の表示領域で確認し、スクロール開始・最下部への到達・OK ボタンが領域内に残ることの5ケースが成功。最下部の「タスクを表示する」とリンク類が見えることを画像でも確認した。実際の RetroBar ウィンドウや OS の配置処理を動かした試験ではない。

両フレームワークのビルド成功（既存の NETSDK1138 警告のみ）、`git diff --check` 成功。

### タブ切替で設定画面の位置が戻る問題の修正（2026-10-24）

前回追加した SizeChanged と従来の ContentRendered の両方から、タスクバー基準の初期配置を再実行していた。初期配置を Loaded の初回だけに限定し、その後の両イベントでは現在位置を維持して、実際にはみ出した部分だけを補正するよう分離した。補正先も、開いたときのモニターではなく現在のウィンドウがあるモニターと現在の DPI を使う。

通常の出力先は実行中の RetroBar.exe によるファイルロックで上書きできなかったため、次のコマンドで別フォルダーに両フレームワークをビルドした。ビルド成功、既存の NETSDK1138 警告のみ。`git diff --check` も成功。実際のドラッグ移動・タブ切替操作は今回未検証。

```powershell
dotnet build RetroBar.sln -c Release --no-restore -m:1 -nr:false -v:minimal -p:BaseOutputPath=D:\work\RetroBar\artifacts\position-fix\
```

今回の更新版は `artifacts/position-fix/Release/net10.0-windows/RetroBar.exe`（.NET 6 は隣の `net6.0-windows/RetroBar.exe`）。通常出力先の起動中アプリは終了させていない。

## ビルドと自動テスト

SDK: .NET SDK `10.0.401`。アプリの対象フレームワークは既存どおり `net6.0-windows` と `net10.0-windows`。

この環境では次の環境変数を設定して実行した。

```powershell
$env:DOTNET_CLI_HOME = 'D:\work\RetroBar\artifacts\dotnet-home'
$env:MSBuildEnableWorkloadResolver = 'false'
```

依存復元:

```powershell
dotnet restore RetroBar.sln -p:NuGetAudit=false -p:RestorePackagesPath=D:\work\RetroBar\artifacts\packages
dotnet restore RetroBar.Tests\RetroBar.Tests.csproj --source D:\work\RetroBar\artifacts\packages -p:RestorePackagesPath=D:\work\RetroBar\artifacts\packages -p:NuGetAudit=false
```

最初の通常実行は NuGet の DNS / ネットワーク制限で失敗した。権限付き復元は成功。テスト側は取得済みパッケージをローカルソースとして復元した。依存バージョン更新なし。NuGet の監査はこの復元では無効で、脆弱性監査を実施したという意味ではない。

最終ビルド:

```powershell
dotnet build RetroBar.sln -c Release --no-restore -m:1 -nr:false -v:minimal
```

結果: **PASS**。両フレームワークのビルド成功、エラー 0 件。既存の .NET 6 サポート終了に関する `NETSDK1138` 警告のみ（WPF 一時プロジェクト分を含め 2 件）。この環境で並列ビルドはエラー詳細なしの失敗になったため、成功した上記の直列コマンドを記載した。

自動テスト:

```powershell
dotnet run --project RetroBar.Tests -c Release -f net10.0-windows --no-restore -p:BuildInParallel=false
dotnet run --project RetroBar.Tests -c Release -f net6.0-windows --no-restore -p:BuildInParallel=false
```

結果: **各 21 PASS / 0 FAIL**。`dotnet test` ではなく、失敗時に終了コード 1 を返すコンソール回帰テストとして実行。

確認内容: 旧 JSON の Primary / All、既定値、既存の表示範囲と配置の保持、Specific の単独選択、プライマリ明示選択、デバイス名の大文字小文字、All の優先、切断時の保存値保持、再接続と列挙順変更、プライマリ変更、デバイス名変化、null、SettingsManager の読込と保存、変更通知と自動保存。追加した6件はメニュー表示先の既定値、タスクバー設定との独立性、対象不在・再接続、保存と再読込、null、変更通知と自動保存を検証する。実際の Windows メニュー表示 API を実行する試験ではない。

`git diff --check`: **PASS**。

ビルド成果物:

- `RetroBar/bin/Release/net10.0-windows/RetroBar.exe`
- `RetroBar/bin/Release/net6.0-windows/RetroBar.exe`

## A01～A14 の確認状況

Windows GUI の操作ツールでビルドしたアプリを起動しようとしたが、ツール側のアプリ承認がタイムアウトした（`Computer Use app approval timed out`）。UI を操作した実機試験は実施できていない。以下の「未検証」は不合格が確認されたという意味ではないが、実機 PASS として扱わない。

| ID | 実機判定 | 実施済みの検証・残る確認 |
|---|---|---|
| A01 | 未検証 | 旧 JSON 読込と選択判定は自動テスト PASS。旧設定を使った実アプリ起動は未確認。 |
| A02 | 未検証 | Specific が指定画面を1件だけ返す自動テスト PASS。UI 選択と実バー数は未確認。 |
| A03 | 未検証 | 既存 Left / Right 配置処理を維持。実画面の左右端で確認が必要。 |
| A04 | 未検証 | 単独表示時に全画面を通す既存タスクフィルタを静的確認。実際の一覧は未確認。 |
| A05 | 未検証 | 切替・最小化の既存 ManagedShell 処理を維持。クリック確認が必要。 |
| A06 | 未検証 | 単独バーからトレイホスト位置を更新する経路を静的確認。左右クリック・ダブルクリック・ポップアップ位置は未確認。 |
| A07 | 未検証 | `HideExplorerTaskbar` の有効化と Dispose 時の解除を維持。実際の非表示と復元は未確認。 |
| A08 | 未検証 | 設定の自動保存・再読込は自動テスト PASS。実アプリ再起動後の UI 復元は未確認。 |
| A09 | 未検証 | 対象不在時にプライマリ1件へ退避する自動テスト PASS。物理切断時のイベント配送は未確認。 |
| A10 | 未検証 | 保存値維持・再接続・列挙順変更の自動テスト PASS。物理再接続後の再配置は未確認。 |
| A11 | 未検証 | 選択判定のプライマリ変更は自動テスト PASS。負の座標、解像度、DPI の実機試験が必要。 |
| A12 | 未検証 | 既存の Explorer 再起動監視から再生成へ至る経路を静的確認。Explorer の再起動試験は未実施。 |
| A13 | 未検証 | Primary / All の選択と All 優先は自動テスト PASS。切替時のイベント重複・バー数は未確認。 |
| A14 | 未検証 | 単独バーのホットキー受信条件とボタン矩形応答を修正。実際のキー操作とアニメーションは未確認。 |

## 制約・既知の未確認事項

- 識別には `AppBarScreen.DeviceName` を使用。再接続で OS が別のデバイス名を割り当てた場合は自動で同じ物理モニターと同定できない。プライマリへのフォールバックを優先するため、設定画面で再選択が必要。
- トレイ表示と左・右・ダブルクリック、メニュー・ポップアップ位置、異なる DPI と負の座標、物理切断・再接続、Explorer 再起動は実機未確認。
- All では受動的なトレイ位置通知はプライマリのバーに限定。アイコン／スタートを実際に操作したバーによる明示的なホスト位置通知は、従来どおり維持。
- 追加翻訳は英語・日本語のみ。他言語の既存リソースは保持し、新規文言は英語フォールバックとなる。

## 標準タスクバーを戻す方法

RetroBar の右クリックメニューから終了する。既存の終了処理が `HideExplorerTaskbar = false` を設定し、ManagedShell の破棄処理へ進む。

異常終了などで標準タスクバーが戻らない場合は、タスクマネージャー（Ctrl + Shift + Esc）で「Windows エクスプローラー」を再起動する。RetroBar が動作中だと既存の監視機構が再び標準タスクバーを隠すため、先に RetroBar を終了する。この復旧操作自体も今回の実機試験では未実施。
