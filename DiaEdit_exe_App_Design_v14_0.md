# DiaEdit.exe 設計書（v14.0）

作成日：2026-07-17
更新日：2026-09-07
技術スタック：Visual Studio / C# / .NET / Avalonia（Skia描画）

---

## 目次

1. [開発目的](#1-開発目的)
2. [課題点](#2-課題点)
3. [搭載する機能](#3-搭載する機能)
4. [実装状況](#4-実装状況)

---

## 1. 開発目的

列車時刻表を快適に作成できるデスクトップアプリケーションを開発する。既存ツールにおける課題（2章）を解消することを目的とする。

**開発体制**：個人開発（単独）。C++/Python経験はあるが、Windows API/UI知識・MVVM経験はゼロの前提で技術選定・解説を行う。

**残したい既存機能**：駅作業（連結・解結・入替など）／運用管理／ダイヤグラム／駅時刻表

---

## 2. 課題点

既存ツールが抱える課題と、対応する設計上の解決策を対にして示す。

| # | 課題 | 内容 | 対応する設計（詳細は4章） |
|---|---|---|---|
| 1 | 上下方向をまたぐ列車の編集 | 方向別（上り／下り）管理のため、直通・折り返しを伴う1本の列車を単一の存在として表現できない（列車番号が重複） | 列車を`Line`でなく`ServiceRoute`に紐付ける |
| 2 | 列車追加の手間 | 基準列車のコピー＆ペースト作成が非効率 | `baseTimeTableSet`内の基準列車を選んで複製する方式 |
| 3 | 交差支障の視認性 | 構内配線図が無く、支障発生箇所が視覚的に分かりにくい | 汎用`ConflictChecker`（駅構内・駅間・番線を統一的に扱う）＋構内配線図の視覚化 |
| 4 | 所要時分の正確性 | 番線ごとの分岐制限速度が異なり、正確な所要時分の把握が難しい | `StationConnectionSegment`・`Rail`（Role=Track/Normal）に距離・速度制限を保持し動的計算 |
| 5 | 直通系統の表現 | 路線ごとの列車管理では直通系統（本線＋支線）を表現しづらい | `ServiceRoute`単位での編集画面切り替え |
| 6 | 乗り換え接続の取りやすさ | 複数路線の乗り換え接続を検討しづらい | 構内配線図上への列車アイコン配置・ホバー表示。`TrainConnectionResolver`による前後列車導出 |
| 7 | 編成長と番線制限 | 編成長ごとに使用できない番線を検知できない | `EffectiveLengthChecker`によるCarConsistの実車両列長とPlatform/Railの有効長との自動照合（超過時は保存不可） |

**設計上の基本原則**

| 原則 | 内容 |
|---|---|
| 単一の正データ over 二重の永続参照 | 導出可能なキャッシュは都度導出し、冗長な相互参照を永続化しない。整合性を走査の利便性より優先する |
| 構造的予防 over 事後警告 | 不整合を構造として作れない設計を、事後検知＋警告よりも優先する |
| 複雑なオブジェクトの単純プリミティブへの分解 | 例：シザースクロッシングは複数の単純な`Switcher`に分解する |
| 破棄・再生成（作り直し） over 自動マイグレーション | トポロジー変更で派生データが無効化される場合は、自動移行せず破棄し再入力を求める |

---

## 3. 搭載する機能

### 3.1 幹（主要機能・コア）

| 機能 | 関連データモデル |
|---|---|
| 路線網モデルのCRUD | `MainRoute`／`StationConnection`／`StationConnectionSegment` |
| 駅構内オブジェクトのCRUD | `FloorUnit`とその配下（`Rail`／`BoundaryPoint`／`EntryPoint`／`BufferStop`／`Platform`／`Switcher`） |
| 運転系統のCRUD | `ServiceRoute` |
| 列車の追加・編集・削除、時刻入力 | `Train`／`TrainRunSegment`／`StopTime` |
| 駅時刻表・ダイヤグラムの生成 | 上記全データからの投影（`DisplayContext`） |

### 3.2 枝（補助機能）

| 機能 | 関連データモデル／アルゴリズム |
|---|---|
| 基準列車（`baseTimeTableSet`）からの列車追加 | `Train.sourceTrainId` |
| 番線ベースの所要時分自動計算（動的調整） | `RunTimeCalculator` |
| 編成長バリデーション（番線ごとの使用可否判定） | `EffectiveLengthChecker` |
| 交差支障検知 | `ConflictChecker`、`StationPath`の展開・グルーピングを含む |

### 3.3 葉（構成要素・UI/表示）

| 機能 |
|---|
| 構内配線図の描画コンポーネント |
| 配線図上での列車アイコン表示・ホバー情報表示 |
| ダイヤグラム描画（幹のデータを描画するだけの薄いレイヤー） |
| 駅作業（連結・解結・入替）UI |
| 運用管理画面 |

### 3.4 出力機能

既存フォーマットからの移行（インポート）は設計思想の乖離が大きいため見送り、エクスポート機能として以下を追加する。

| 出力 | 形式 |
|---|---|
| 駅時刻表・ダイヤグラム・列車時刻表 | PDF（印刷レイアウト） |
| ダイヤグラム等 | PNG／SVG（ダイヤグラムはSVGの方がベクタとして扱いやすい） |
| 列車時刻表データ | CSV（外部集計・共有用） |

### 3.5 実装順序

**推奨する実装順序（大分類）**

1. 路線網モデル（`MainRoute`/`StationConnection`/`ServiceRoute`）を最小構成で実装し、既存の方向別管理からの移行が成立するか検証
2. 駅時刻表・ダイヤグラムの自動生成
3. 基準列車からの列車追加、番線所要時分計算（枝）
4. 構内配線図・ホバー情報（葉）

データモデルの組み替えが最もリスクが高いため、最初に着手し早期に検証する。

---

## 4. 実装状況

### 4.1 マイルストーン一覧

| # | マイルストーン | 状態 |
|---|---|---|
| M0 | 要件・アーキテクチャ確定 | ✅完了 |
| M1 | コアドメイン層実装（DiaEditCore） | 🟡ほぼ完了（コマンド横展開のみ残） |
| M2 | Walking Skeleton（駅編集の垂直スライス） | ✅完了・実機確認済み（v13.3、2026-09-02）：M2-1〜M2-6全項目完了 |
| M3 | MVP：4モードタブの基本編集画面 | ❌未着手（M2完了後） |
| M4 | 機能拡充（駅時刻表・ダイヤグラム・運用時刻表・構内配線図ポップアップ等） | ❌未着手 |
| M5 | 非機能対応（ダイヤグラム描画CPU対策、Undo確認ダイアログ等） | ❌未着手 |
| M6 | テスト・品質保証 | 🟡Core側は継続中。ViewModels/App側は`ChangeNotificationBridgeTests`のみ |
| M7 | リリース準備（パッケージング、CSV/HTML出力） | ❌未着手 |


**実装手順**

M2以降は「**UI実装→問題抽出→設計書修正→実装修正**」のサイクルを基本の開発フローとする。

また、未着手のマイルストーンに初めて着手する場合は、M2を参考にして§4.1.xとして、タスク、実施内容、他タスクへの依存、状態を明確にした表を作成し、セッションごとに確認を怠らないこと。

UI層に実際に触れることで、Core側の設計・実装の抜け漏れ（未実装ケース、逆引きの欠落等）が具体的な形で表面化するため、これを都度にフィードバックし、設計書を先に直してから実装を追従させる進め方に統一する。

このサイクルの中で、DependencyResolverの依存関係グラフ完成作業をUI実装と並行して進める。具体的な優先順位を以下の通り確定する：

1. **Commandは新規登録（Create/Add）系を優先実装する**：汎用コマンドパターン横展開のうち、属性変更・削除より先にCreate系を対象モデルごとに揃える。UI側の「一覧→追加→詳細編集」という導線（マスター・ディテール構成）が、まず新規オブジェクトを作れることを前提とするため。
2. **逆引きキャッシュ（Index）の要否は都度判断する**：あるオブジェクトが別のオブジェクトを参照する関係が実装過程で見つかるたびに、インデックス棚卸し表（Builder化候補）へ機械的に追加しない。実装するCommand（特にDelete）が実際に参照元チェックを必要とするかどうかを見て、必要になった時点でIndex新設を判断する。ResolveDirectDependents未実装ケース網羅および、未実装Index棚卸しは、この判断基準の下でM2〜M4を通じて順次消化していく運用とする。

（運用メモ・v13.1追記）フェーズ再分類表上のフェーズ表記と、次アクションでの実施タイミングが食い違う場合（早期クローズ可能と判断し前倒しするケース等）は、表側の該当行に「（M2着手前に前倒し可）」のような注記を残し、後から読んだ際に矛盾に見えないようにする。

（運用メモ・v13.6追記）各セッションの終了時、確認済みの実装内容（ビルド成功・テスト成功・実機確認のいずれかが取れたもの）を変更履歴へ追記するのに合わせて、**次アクション**も必ず同一セッション内で更新する。更新を怠ると次回セッション開始時に「どのバージョンの次アクションが実態と合っているか」を変更履歴全体を遡って突き合わせる必要が生じる（v13.4時点の次セッションが前バージョンの未定義事項のクローズを反映しないまま次回セッションに持ち越された事例が実際に発生した）。**次アクション**の更新は**変更履歴追記と対で行う一連の作業と**し、**片方だけを行った状態でセッションを終えないこと**。

（運用メモ・v14.0追記）過去の修正ログや、相互参照をチェックする過程で、章節参照番号に表記ゆれが存在することが判明した。今後、参照を行う部分は`§x.x`のようにし、未定義事項など、章節内にテーブルが存在する場合は、`§x.x項目x`ではなく、疑似的に`§x.x-x`のように表記を行うこと（`§x.x.x`とすると、別の章節番号を参照してしまう恐れがあるため）。

---

#### 4.1.1 M2

対象を「駅」とした理由：コマンド3パターン（属性変更・新規登録・削除）が既に実装済みで、UI層に着手すれば即座に配線できる最短経路であったため。

| # | 項目 | 内容 | 依存 | 状態 |
|---|---|---|---|---|
| M2-1 | ナビゲーションツリー最小実装 | ツリー構造のうち「駅／駅一覧」ノードのみ先行実装。他カテゴリは空ノードでよい | — | ✅完了・動作確認済み（v13.2） |
| M2-2 | 駅一覧画面（マスター） | 列構成でテーブル表示。「+駅追加」で`StationCreationWorkflow.CreateStationWithDefaultFloorUnit`呼び出し | M2-1 | ✅完了・動作確認済み（v13.2） |
| M2-3 | 駅詳細編集画面（ディテール） | `ChangeStationAttributesCommand`／`DeleteStationCommand`を実配線 | M2-2 | ✅完了・動作確認済み（v13.2、並べ替えは見送り） |
| M2-4 | ChangeNotificationBridgeの実配線 | `AttachedToVisualTree`等のAvaloniaライフサイクルでSubscribe/Unsubscribe。ディスパッチャ本体は`ChangeNotificationBridgeTests`で検証済みのため、UI側の購読タイミングのみが残作業 | M2-3 | ✅完了・動作確認済み（v13.3、実装ではコンストラクタ／`Dispose()`紐付けへ簡略化） |
| M2-5 | Undo/Redo最小UI | ツールバーボタン＋`CommandInvoker`接続のみ。確認ダイアログ（Undo確認ダイアログ）はM4に後回し | M2-3 | ✅完了・動作確認済み（v13.2） |
| M2-6 | 保存（Ctrl+S）の最小実装 | dirtyキャッシュ一括フラッシュ→`JsonProjectFileSerializer`呼び出し。保存先パス選択等の周辺UIは最小限でよい | M2-3 | ✅完了・動作確認済み（v13.3、dirtyキャッシュ一括フラッシュは不要と判明）** |

**M2完了の定義**：アプリを起動し、駅を1件追加→名称等を編集→保存→再起動して読み込めることを確認できる状態。

**v13.3進捗**：M2-4（ChangeNotificationBridge本配線）・M2-6（保存機能）を実装・動作確認完了。あわせてCreate系コマンドのRedo参照同一性バグをクローズ。M2完了の定義（起動→駅追加→編集→保存→再起動→読込確認）を実機確認済み。

---

### 4.2 次アクション

**v13.14更新（構内配線図キャンバスUI：収束変換Algorithm/Command層の実装完了、テストは未実施）**：構内配線図キャンバスUI実装・端点収束・確定変換ワークフロー未実装のうちAlgorithm層4件・Command層3件（`RailEndpointConvergenceResolver`／`RailEndPointRefChanger`／`DeleteFloorUnitObjectCommand<TId,T>`／`RailEndpointConvergenceWorkflow`／`ChangeSwitcherAttributesCommand`／`RailDeletionWorkflow`）を実装、`SwitcherExpand`ケースの`NotImplementedException`を解消、`ProjectSession.SwitcherIds`追加、既存`DeleteRailCommand`は無改修のまま`RailDeletionWorkflow`（`RailCreationWorkflow`と対称）でラップしてRail削除時の両端点再照合を配線した。ビルド成功確認済み（xUnitテストは未実施）。新規発見事項として、`RailEndpointConvergenceWorkflow.Reconcile`のVanish/Keepケースで消滅する端点のStationPathブロックチェック（`FindBlockingStationPaths`）が呼ばれていない設計上の穴を発見、§9.2項目39として新規記録。
次回セッションの優先順位を下記へ更新する。

1. **v13.14実装分のテスト実施・実機確認**（次回セッション最優先）：`RailEndpointConvergenceResolver`／`RailEndPointRefChanger`／`DeleteFloorUnitObjectCommand<TId,T>`／`RailEndpointConvergenceWorkflow`／`ChangeSwitcherAttributesCommand`／`RailDeletionWorkflow`のいずれもビルド成功のみでテスト未実施。xUnitテスト作成・実行、可能であれば実機確認を行う
2. **Vanish/KeepケースのStationPathブロックチェック欠落の対応要否判断**：実際に消滅する端点がStationPath.Waypointsから参照されるケースがあり得るかを検証した上で、対応するかを判断する
3. **構内配線図キャンバスUI本体：ViewModel/View層の実装**：Algorithm/Command層が揃ったため、キャンバス描画・ドラッグジェスチャ・モード切替・端点編集モーダルの実装に進む。「構内進路編集モード」はボタンのみ配置しStationPathSuggester統合は対象外
4. **M3（4モードタブの基本編集画面）に着手する**：§4.1のマイルストーン全体像に従う
5. **保存時Idコンパクションに着手する**：`JsonProjectFileSerializer.Save()`側の設計に入る
6. DependencyResolver.ResolveDirectDependents`のEntryPointObjectId／MainRouteObjectIdケースでのIndex参照組み込み確認は、M3着手時に別途確認する
7. EntryPoint.Type変更ポリシー見直しは判断保留のため優先度を下げる。着手前に`EntryPointValidator.cs`／`StationPathValidator.cs`の監査が必要
8. **FloorUnit並べ替えUI、新設・v13.11**：`ReorderFloorUnitsCommand`（v13.10新設）のViewModel/View配線が未着手。UI操作方式（ドラッグ&ドロップ／↑↓ボタン）は未決定。設計者判断により先延ばし確定、優先度は現時点で低
9. 汎用カスケード削除機構・`RailSplitter`：いずれも将来構想として記録済み。着手時期は未定

（旧バージョン時点の次アクションは旧バージョンファイルを参照）

---

### 4.3 実装における特記実装

各マイルストーンの実装において特記すべき事項についてマイルストーンごとに記載する。

#### 4.3.1 M2 実装確定事項

M2-1（ナビゲーションツリー）・M2-2（駅一覧マスター）・M2-3（駅詳細編集）・M2-5（Undo/Redo最小UI）を実装し、いずれも動作確認済み。実装済みファイルは以下の通り。

**DiaEditApp.ViewModels**：`Navigation/NavigationNodeViewModel.cs`、`MainViewModel.cs`、`Stations/StationListViewModel.cs`、`Stations/StationDetailViewModel.cs`、`Composition/ViewModelServiceCollectionExtensions.cs`
**DiaEditApp.Views**：`MainWindow.axaml`、`Stations/StationListView.axaml`／`.axaml.cs`、`Stations/StationDetailView.axaml`／`.axaml.cs`
**DiaEditApp（ルート）**：`Services/AppSettings.cs`、`App.axaml.cs`

**起動時プロジェクト初期化方針**：`ProjectSession.Load()`が従来一度も呼ばれておらず`ProjectSession.Current`が`null`のまま画面遷移するとクラッシュする欠落が本セッションで発覚し対応した。`AppSettings`（`%AppData%\DiaEdit\settings.json`、DiaEditApp.Services層）に`LastProjectFilePath`を保持し、起動時：パスがあれば`JsonProjectFileSerializer.Load(path)`を試行、パスが無い／読込失敗（理由を問わず）の場合は空の新規`ProjectFile`を生成してから`ProjectSession.Load(project)`を呼ぶ。空プロジェクトの`ProjectSettings`既定値は、`ValidationRules`の各閾値を全て`null`（未設定＝当該チェックスキップ、路線ごとの実情に応じてユーザーが後から設定する前提）、`EnableConflictDetection`／`EnableCarLengthCheck`は安全側として`true`、`DiagramBasedTimeSec`は`ProjectSettings`側のデフォルト値（14400＝4:00）をそのまま使用。`AppSettings.LastProjectFilePath`の保存時書き戻しはM2-6実装時に対応。

**DI登録方式の確定（駅系画面ViewModel）**：`StationListViewModel`・`StationDetailViewModel`のデータ取得元解決について3案（A：`IReadOnlyList<Station>`直接注入／B：汎用`IReadOnlyList<T>`ファクトリの型ごと個別登録／C：`ProjectSession`と`CommandInvoker`をそのままコンストラクタ注入）を比較し、**案Cを採用**した。A案はCreate/Delete実行に結局`ProjectSession`/`CommandInvoker`が別途必要になり二重管理になる、B案はopen genericの型解決が曖昧になりやすく構造的予防の原則に反する、という理由による。C案では両者が既にSingleton登録済みのため`services.AddTransient<T>();`のみでファクトリラムダ不要のプレーンなコンストラクタ注入が効く。今後追加する画面ViewModel（路線一覧等）も同じパターンを踏襲する想定。

**駅詳細編集画面の実装範囲確定**：上部（`DisplayName`／`Type`／`OperatingCode`／`TelegraphCode`／`ShowsInStationTimetableOverride`）は編集可能。保存は明示的な「保存」ボタン（`ChangeStationAttributesCommand`を1回だけExecute）で確定し、フィールド変更のたびの自動保存は採らなかった（Undoスタックが不必要に細分化されるため）。**FloorUnitの並べ替え（`DisplayOrder`変更）はM2スコープから明示的に見送り、追加・削除のみ実装した**（対応コマンドは現状未実装であることを本セッションで確認済み。）。`ShowsInStationTimetableOverride`（`bool?`）はUI上`CheckBox`の`IsThreeState`（null/true/false）で表現し、`Station.ResolveShowsInStationTimetable()`の派生値を隣に補助表示する。FloorUnit削除が`DeleteFloorUnitCommand`のコンストラクタ内検査（n≥1制約／直接参照元あり）で拒否された場合、専用ダイアログの仕様がまだ未設計のため暫定的に画面内テキストメッセージで表示している。

**駅一覧⇔駅詳細のナビゲーション設計（新規パターン、今後のマスター・ディテール遷移で踏襲）**：`StationListViewModel`は`event Action<Station>? OpenDetailRequested`を公開し、`OpenDetailCommand`（Viewの`DoubleTapped`から呼ばれる）実行時に発火する。`MainViewModel`は`ShowContentForSelectedNode()`（旧`OnSelectedNodeChanged`本体を分離・再利用可能にしたメソッド）でコンテンツ生成時、それが`StationListViewModel`であれば`OpenDetailRequested`を購読する。遷移先の`StationDetailViewModel`は`Station`というDIコンテナ管理外の実行時パラメータをコンストラクタで受け取るため、`Microsoft.Extensions.DependencyInjection`の`ActivatorUtilities.CreateInstance<T>(sp, station, goBackAction)`を使い、`session`/`invoker`はDIコンテナから解決しつつ`station`と`goBack`コールバックのみ呼び出し側から渡す。「一覧へ戻る」（`GoBackCommand`）は`MainViewModel.ShowContentForSelectedNode`をそのままコールバックとして渡すことで実現し、一覧側の状態を個別に保持する必要をなくしている。

**UI実装上の既知の制約**：`Avalonia.Controls.DataGrid`（別NuGetパッケージ、未導入）は使わず、`ListBox`＋`Grid`による簡易テーブル表示で代替した（駅一覧・FloorUnit一覧とも）。列ソート・列リサイズ等が必要になった時点でパッケージ追加を含めて改めて検討する。またXAMLコメント本文が`-`で終わると`XmlException`になる制約、および実在未確認の型（例：`Avalonia.Controls.Converters.StringConverters`）をXAML内で参照すると当該1ファイルの型解決失敗が原因でプロジェクト全体のXAMLリソース生成タスクが中断し、無関係な`App.axaml`側で「No precompiled XAML found」という誤解を招くエラーになることを確認した。以後、XAML内でのコンバータ等の外部クラス参照は、実在確認ができない場合はViewModel側の計算プロパティに逃がす方針とする。

**Undo/Redoにおけるオブジェクト参照の不整合**：M2-3動作確認中、「駅追加→詳細編集→駅追加前までUndo」という操作列で、詳細編集（`ChangeStationAttributesCommand`）が正しくRedoされないケースを発見した。原因はID採番方式ではなく、より根本的な**Create系コマンドの`Redo()`が毎回新しいオブジェクトインスタンスを生成し直す設計**にある。v13.3にて、CreateStationCommand／CreateRailCommand／CreateFloorUnitCommandのApply()を、生成結果保持プロパティが非nullならAllocateNextIdを呼び直さず既存インスタンスを再利用するパターンへ統一した。UndoableCommand基底にComputeAffectedIdsAfterApplyフックを追加し、Create系コマンドのAffectedIdsをApply完了後に確定・凍結する仕組みを導入することにより、期待されるUndo/Redoの挙動へ修正した。

**詳細画面表示中に対象オブジェクトが消滅した場合の復帰処理（新設・v13.3）**：M2-4実装過程で発見。`StationDetailViewModel`が表示中の`Station`が、Undo（Create系コマンドの取り消し）や他画面からの削除操作によりセッション上のコレクションから除去された場合、画面が編集不能な孤立オブジェクトを表示し続ける問題があった。

対応：`IAffectedByObjectId.OnAffected()`（`ChangeNotificationBridge`経由の通知ハンドラ）内で、対象がまだ`_session.Current.Stations`に含まれるかを確認し、含まれなければ`_goBack()`を呼んで一覧画面へ自動的に戻る。

```csharp
void IAffectedByObjectId.OnAffected()
{
    if (!_session.Current.Stations.Contains(_station))
    {
        _goBack();
        return;
    }
    LoadFromStation();
    ReloadFloorUnits();
}
```

`List<T>.Contains`は参照等価性で判定されるため、Redo時の同一インスタンス再利用が前提として効いている。今後Rail等の詳細画面を実装する際も同じパターンを踏襲すること。

**ChangeNotificationBridgeの購読方式（M2-4本体・新設・v13.3）**：

購読対象の使い分け確定：
- **一覧画面**（`StationListViewModel`）・**MainViewModel**（Undo/Redoボタン判定）：`CommandInvoker`への直接`Subscribe`を維持。理由：一覧は「将来追加される未知のObjectId」を含む全件を監視対象とする必要があり、`ObservedIds`という固定（または都度算出）集合ベースの`ChangeNotificationBridge`とは性質が合わない。discard-and-regenerateの精神を素直に適用する対象として維持する。
- **詳細画面**（`StationDetailViewModel`）：`ChangeNotificationBridge`＋`IAffectedByObjectId`へ移行。`ObservedIds`は固定集合ではなく、Station自身のObjectId＋現在の配下FloorUnit全件のObjectIdを都度算出する動的プロパティとした（FloorUnit追加直後の新規Idも自動的に監視対象へ含めるため）。

購読タイミングの簡略化（設計意図からの変更点）：M2-4原案では「`AttachedToVisualTree`等のAvaloniaライフサイクルでSubscribe/Unsubscribe」としていたが、実装では`StationDetailViewModel`のコンストラクタ／`Dispose()`に紐付ける形を採用した（`MainViewModel.ShowContentForSelectedNode`が明示的に`(CurrentContent as IDisposable)?.Dispose()`を呼ぶ既存設計と整合するため）。View（.axaml.cs）側のAvaloniaライフサイクルとは連動させていない。M2スコープでは実用上問題ないと判断し、この簡略化を正式な方針とする。

CommandInvoker.Notify()の列挙中変更対策（M2-4実装過程で発見・修正）：`ChangeNotificationBridge.OnChanged`は元々`_subscribers.ToArray()`で列挙中変更に対応済みだったが、`CommandInvoker.Notify()`側は`_observers`を直接foreachしており対策が漏れていた。`OnChanged`内から同期的に`Dispose()`→`Unsubscribe()`が呼ばれる経路（詳細画面の対象消滅→自動的に一覧へ戻る、上記⑧）で`InvalidOperationException`を誘発するため、`ChangeNotificationBridge`と同じ`.ToArray()`スナップショット方式に修正した。

**保存機能（M2-6・新設・v13.3）**：

構成：
- `IFileDialogService`（`DiaEditApp.ViewModels`、Avalonia非依存の抽象）／`FileDialogService`（`DiaEditApp.Services`、`IStorageProvider`経由の実装）
- `IAppSettingsService`（`DiaEditApp.ViewModels`、Avalonia非依存の抽象）／`AppSettingsService`（`DiaEditApp.Services`、既存`AppSettings`のラッパー）— 依存方向規約（DiaEditApp.ViewModelsはDiaEditApp.Services/DiaEditApp本体を参照しない）を守るため、具象`AppSettings`を`MainViewModel`へ直接注入せず、インターフェース越しに使う設計とした
- `MainViewModel.SaveCommand`（`[RelayCommand]`、非同期）：`AppSettings.LastProjectFilePath`があればそのパスへ、無ければ`IFileDialogService.PickSaveProjectFileAsync`でユーザーに選ばせてから`JsonProjectFileSerializer.Save`を呼ぶ。成功時は`LastProjectFilePath`を更新して`AppSettingsService.Save()`。

dirtyキャッシュ一括フラッシュについて：`SaveValidationRunner.ToValidationContext()`は`ProjectFile`のリストから直接`ValidationContext`を構築しており、`ProjectSession`の`TimeTableSetCache`には依存しない。したがって保存フローが`ProjectSession.GetCache()`を明示的に呼んでdirtyフラグを解消しておく必要はないと確認できた。

バリデーション失敗時のUIハンドリング（§4.4.2項目25と同一の論点）：`ProjectFileValidationException.Issues`を`Environment.NewLine`区切りで連結し、`MainViewModel.SaveErrorMessage`へ一括表示する暫定方針を採用（`StationDetailViewModel.DeleteFloorUnitError`と同じ、画面内インラインメッセージのパターンを踏襲）。専用ダイアログ化は本節の暫定方針解消時（§4.4.2項目25クローズ時）にあわせて見直す。

保存先ファイル拡張子：`.dedit`で確定。

**Core層record型のXAMLバインド運用確定（新規発見の重大な設計制約、v13.11）**：FloorUnitDetailView実装中、新設したCore層のrecord型`FloorUnitSummary`（`DiaEditCore.Algorithm.Resolvers`名前空間を想定）をXAML側で`x:DataType`として直接参照したところ、当該名前空間が実在しない誤り（既存の`DiaEditCore.Algorithm.CacheBuilder`等の命名パターンからの類推のみで進め、実在確認を怠ったことが原因。実際は`DiaEditCore.Algorithm`というフラットな名前空間だった）により型解決に失敗し、「1ファイルの型解決失敗がプロジェクト全体のXAML生成を巻き込み、無関係な箇所のバインディングエラーとして表面化する」事象が再発した。

対応：「XAML内でのコンバータ等の外部クラス参照は、実在確認ができない場合はViewModel側の計算プロパティに逃がす」の適用範囲を、**コンバータだけでなくCore層のrecord型（DTO）全般に拡張する**方針を確定。`FloorUnitSummary`は`FloorUnitDetailViewModel`側でフラットなプリミティブ型プロパティ（`SummaryTrackRailCount`等）として再公開し、XAMLからは`FloorUnitSummary`という型名自体を一切参照しない実装に修正した。

運用メモ：Core層に新規の名前空間を導入する際は、既存ファイルのnamespace宣言を実際に確認してから使用すること（類推のみで進めない）。今回の原因はCore層側の命名規則の類推のみで作業を進めたことによる。

### 4.4 未定義事項

実装難易度・依存関係を踏まえて分類する。進捗状況をアイコンで以下のように示す。

| アイコン | 状態 |
| --- | --- |
| 🔴 | 未着手 |
| 🟠 | 方針検討済み |
| 🟡 | 実装済み・テスト未着手 |
| ✅ | クローズ・解消済み |

#### 4.4.1 優先度：高 （DiaEditCore.Modelに影響する可能性大）

| # | 進捗 | 項目 | 内容 |
|---|---|---| --- |
| 1 | 🟠 | `ResolveDirectDependents`の未実装ケース網羅対応 | StationObjectId／BoundaryPointObjectId等／EntryPointObjectId／BufferStopObjectId／StationConnectionSegmentObjectId／StationPathObjectId／MainRouteObjectId／StationConnectionObjectId。RailObjectIdはv12.20で対応済み、StationObjectIdはv12.24で対応済み（いずれもDependencyResolverのグラフ経由ではなくコマンド内直接走査／専用Indexの組み合わせ、詳細は各バージョンの変更履歴参照）につき対象から除外。BoundaryPoint／EntryPoint／BufferStop／Platform横展開（§9.2項目10）と一体で実装。**v12.29進捗**：「孤立Segment問題」（Station経由はv12.24で対応済み）がEntryPoint・MainRouteの2つにも共通発生することを確認し、新規Builder`EntryPointUsedBySegmentIndexBuilder`／`MainRouteUsedBySegmentIndexBuilder`は設計・実装済み。**v13.1で`TimeTableSetCache.RebuildAll`への配線が完了済みであることを確認**（`EntryPointUsedBySegmentIndex`／`MainRouteUsedBySegmentIndex`ともに構築されている）。ただし`DependencyResolver.ResolveDirectDependents`本体（EntryPointObjectId／MainRouteObjectIdケースでのIndex参照組み込み）が実際に更新済みかは本セッションでは未確認のため、残課題として維持する。またMainRoute←ServiceRoute／StationConnection←ServiceRoute等、他の未実装Index群の棚卸しに着手（設計中）。**v12.31追記**：`StationConnectionObjectId → ServiceRoute`（`ServiceRouteSegment.SelectedStationConnectionId`／`PairedSelectedStationConnectionId`経由）を新規`StationConnectionUsedByServiceRouteIndexBuilder`で実装し、`TimeTableSetCache`／`ProjectSession`に配線済み。棚卸し表からは本項目を削除済み。残課題（BoundaryPoint／EntryPoint／BufferStop／StationPathのWaypoints経由分、MainRouteのServiceRouteSegment・DisplayContext経由分）は引き続き未着手。BoundaryPoint等は§9.2項目10（StationWork CRUD横展開）と一体実装の方針を維持 |
| 2 | 🔴~🟠 | `TrainOperation`実体のdiscard-and-regenerateロジック本体の実装 | `TrainOperation`をStartOp・DecouplingのCarComposition集合ごとに内部導出される派生データへ位置づけ変更したことに伴い、`OperationNumber`文字列群から`TrainOperationId`への安定的な割当方法（同一番号に常に同一IDを再利用するか、都度採番し直すか）の実装が必要。`RailMerger`と同種の設計判断を要する。discard-and-regenerateのトリガーは「StationWorkのChangeAttributesでStartOpが選択され、OperationNumberが設定されたこと」であり、StationWork系CRUDコマンド実装（§9.2項目10）と不可分のため、同一セッションで着手する方針が確定済み |
| 3 | 🔴~🟠 |「Train作成＝ID発行のみ、RunSegments編集は汎用コマンドへ」という設計転換の要否 | `ServiceRouteToRunSegmentsResolver`実装セッション中に浮上した論点：現行は「列車追加＝RunSegments/StopTimesの初期構築まで含む一体操作」という前提だが、経路②（基準列車複製）・経路③（新規構築）のいずれも実質的には「RunSegments編集」という共通の下位操作にTrain作成が付随しているだけと捉え直せる。この場合、Train作成コマンド自体は`TrainId`発行と基本属性設定のみの薄い操作とし、RunSegments／StopTimesの決定は新規作成時・既存編集時を問わず共通の「RunSegments編集コマンド」（未設計）が担う設計に転換できる可能性がある。「列車追加の3経路」全体の書き換えを伴うため、着手前に単独セッションで方針を固める必要がある。**v12.28追記**：`RunTimeCalculator.Calculate`の呼び出し元設計（Train編集コマンド側での「DiagramRevision特定→BaseTimeTableSetId取得→対象Train絞り込み→`BaseRunTimeIndexBuilder.Build`呼び出し→ホップごとの`RunTimeHopInput`組み立て」の一連の流れ）は、本項目のクローズ前に先行設計すると結論次第で手戻りが生じるリスクがあるため、本項目のクローズと合わせてTrain編集用`UndoableCommand`実装セッションで着手する方針とした |

#### 4.4.2 優先度：中 （DiaEditCore（Modelを除く）に影響する可能性大）

| # | 進捗 | 項目 | 内容 |
|---|---|---| --- |
| 1 | 🟠 | 保存前dirtyキャッシュ一括フラッシュの組み込み位置 | Ctrl+Sハンドラ内・ファイルシリアライズ直前という方針は確定済み。具体的なクラス構成・呼び出し経路は実装着手時に詰める |
| 2 | 🔴 | `ChangeNotificationBridge`へのViewModel登録・解除タイミング | Avaloniaライフサイクルイベント（AttachedToVisualTree等）のどれをSubscribe/Unsubscribe契機にするか、購読漏れ防止の実装方式、`TimeOfDaySec`等のコンバータ設計は実装着手時に詰める。ディスパッチャ本体の通知ロジックは`ChangeNotificationBridgeTests.cs`（7ケース）で検証済み |
| 3 | 🔴 | `ReversalResolver`のShunting対応 | 使用Track変更ケース（`RailRole.Shunting`側の判定）は現状スコープ外。必要になった時点で追加対応 |
| 4 | 🔴 | `BoundaryEntryPointResolver`の出発側取得への拡張 | 到着側のみ返す現行実装のため、`ReversalResolver`が出発側取得ロジックをローカル複製している。両端を返せる拡張の余地あり（優先度低） |
| 5 | 🟠 | `TrainOperationValidator`の2引数版の誤登録リスク | v11.39でValidatorを明示的登録・DIコンテナ非登録の方針としたため当面のリスクは解消。将来DI経由解決が必要になった際に再確認 |
| 6 | 🟠 | N=2 Rail自動統合ロジックのUI層残作業 | `RailMerger`純粋関数はv11.42実装完了。エディタ操作からの呼び出しフロー、Dirty化・再生成促しのUI層はUI着手時に詰める |
| 7 | 🔴 | `AffectedIds`受け取り〜キャッシュ無効化ディスパッチの汎用機構未整備 | `UndoableCommand.Execute()`が返す`AffectedIds`（`IReadOnlySet<ObjectId>`）を受け取り、`ObjectId`種別ごとに`TimeTableSetCache`側の該当インデックス（`StationConnectionIndex`等の軽量Index、`ConflictObjectGroupingCache`等の重量キャッシュ）を無効化・再構築へディスパッチする汎用ルーチンが未実装。現状`InvalidateConflictCache(ObjectId)`という個別メソッドのみ存在。CommandInvoker（呼び出し元）側の設計とあわせて着手する |
| 8 | 🟠 |  `SplitOriginRef`実在性検証のCross Validator拡張 | `CouplingWork`はRule 9で参照先StopTime実在性を検証済みだが、`SplitOriginRef.OriginStopKey`側には対応するルールが未設定。`StopKeyReferenceIndex`を用いた絞り込みとあわせて追加する |
| 9 | 🟠~🟡 | 汎用コマンドパターンの新規登録／削除への拡張 | `ChangeStationAttributesCommand`（v12.10）に続き、`CreateStationCommand`／`DeleteStationCommand`（ともにv12.11）でStationにおける新規登録・削除パターンの実装が完了。削除パターンの参照元残存時の扱い（execute時点で拒否）も確定。残作業は他モデルへの横展開（§4.4.2-10と統合） |
| 10 | 🟠~🟡 | 汎用コマンドパターンの他モデルへの横展開 | Station（v12.10〜v12.11、3パターン完了）、Rail（v12.12〜v12.13、属性変更・新規登録・削除の3パターン完了。ただし属性変更は`Name`／`LengthM`／`SpeedLimitKph`／`Role`の4フィールド限定）に続き、**v13.8でRail端点3種（BoundaryPoint／EntryPoint／BufferStop）の新規登録パターン＋`AttachRailEndpointsCommand`／`RailCreationWorkflow`を実装完了**（Rail作成＝両端点オブジェクト作成と等価という業務理解に基づく。Switcherのみ既存端点接続の別導線として対象外）。Rail残作業：`EndpointA`/`EndpointB`接続変更コマンド（Switcherコマンド実装時にあわせて設計）、`ControlPoints`形状編集コマンド。他モデル（MainRoute、ServiceRoute、Platform、Switcher等）は未着手 |
| 11 | 🔴 | プロジェクト読込時のID再採番（コンパクション） | セッション中のID採番方針（最大値+1、欠番を詰めない）に対応し、Undoスタックが存在しないプロジェクト読込時に限定して欠番を詰める再採番を行う設計・実装が必要。`JsonProjectFileSerializer`側の読込フロー、または専用コンポーネント（`ProjectIdCompactor`等）としての実装形態は未確定 |
| 12 | 🔴 | `TrainOperationIndex`の重複プロパティ削除 | `TimeTableSetCache.TrainOperationIndex`は消費者が存在しないため削除する。実消費者は`TrainCrossValidationData.TrainOperationIndex`（型・生成元とも別物）のみ |
| 13 | 🟠~🟡 | `ConflictObjectGroupingCache`のID単位dirty管理廃止 | `_conflictDirty`／`GetConflictGroup`を撤去し、他のCacheBuilderと同じフルダーティ化＋一括再構築方式に統一する方向。最終確認待ち |
| 14 | 🔴 | `DepartureByStationTrackIndex`の再計算経路の重複 | `TrackOccupancyProvider`・`TrainOperationCrossValidator`・`TrainOperationUniquenessValidator`が`ProjectSession`経由でなく`DepartureByStationTrackIndexBuilder.Build`を直接呼び都度再計算しており、`ProjectSession`のCacheと理論上不整合になりうる。現時点実害なしと判断し保留 |
| 15 | 🟠 | 時刻表UI上の`RunTimeCalculationResult`背景色表現 | OuDiaSecond互換（不足＝薄赤、超過＝薄青、未定義＝薄黄）を踏襲する想定。`HopRunTimeOk`／`HopRunTimeUndefined`の型が確定したため、ViewModel/UI層はこの判別共用体をswitchするだけで色分けを機械的に決定できる状態になった（方針「ViewModel/UI層はUndoableCommand設計後」に従い、UI実装自体は引き続き後回し）。なお型は当初想定していたShortfall／Exceeded区分を持たない2ケース構成に確定したため、「不足＝薄赤／超過＝薄青」の色分けはUI層側で`ProposedAdjustment`の差分符号から独自に導出する必要がある点に注意（Algorithm層の型そのものには含まれない） |
| 16 | 🔴 | `DiagramRevision.BaseRevisionId`（複製元追跡タグ）の削除時参照元チェック要否 | 自己参照フィールドだが、削除系コマンドの参照元チェック対象に含めるべきか未確定（複製元Revisionを削除しても複製先には実害がない可能性がある。`Train.SourceTrainId`の扱いと同種の論点） |
| 17 | 🟠 | 未実装Index棚卸しの方針確定 | MainRoute←ServiceRoute／StationConnection←ServiceRoute・Train／Train関連4種（TrainType・ServiceRoute・VehicleType・TimeTableSet起点）／CarConsist・CarComposition関連／FloorUnit←VirtualConflictObjectの計13種のBuilder化候補。**v12.31**：このうち`StationConnection←ServiceRoute`（`StationConnectionUsedByServiceRouteIndexBuilder`）を実装完了、残り12種。統合粒度（`TrainReferenceIndexBuilder`のような複合Builderとするか、1 Builder = 1 Indexの既存粒度を優先するか）・着手順（MainRoute関連が有力）が未確定。次回セッション冒頭で確認する6項目は列挙済み |
| 18 | 🔴 | ループ路線の継ぎ目駅自体（`StationOrder`の`index 0`・`index count-1`自体）での境界判定への未対応 | `BoundaryEntryPointResolver`のIndex範囲検証、および`ReversalResolver.ResolveDirectionReversalStations`のループ（`for i = 1; i < stationOrder.Count - 1`）は、ループ路線の継ぎ目駅そのものでの境界判定に対応していない。`EntryPointSequenceResolver`側にループ対応（`IsLoop`ゲート）を入れたことで、この既存スコープ外事項が相対的に露見した。対応要否・優先度は未決定 |
| 19 | 🔴 | コマンド実行時拒否・保存時バリデーション失敗のUIハンドリング方針未確定 | `DeleteFloorUnitCommand`等のコンストラクタ内検査による`InvalidOperationException`（n≥1制約・直接参照元残存）、および`JsonProjectFileSerializer.Save`の`ProjectFileValidationException`（保存時バリデーション失敗）について、専用ダイアログ等の共通UIコンポーネントが未定義。現状`StationDetailViewModel.DeleteFloorUnitError`のような画面固有の暫定テキスト表示のみで個別対応している。M2-6（保存機能）着手時に、エラー表示の共通化を含めて方針を確定する |
| 20 | 🔴~🟠 | 詳細画面の対象消滅ハンドリングの横展開 | 「表示中の対象が外部から消滅したら一覧へ戻る」パターンは、現状StationDetailViewModelのみに実装済み。Rail等、今後実装する他モデルの詳細画面にも同じパターンを横展開する必要がある。コマンド横展開と合わせて、UI層のこのパターンもテンプレート化しておくとよい |
| 21 | 🟠 | プロジェクトIdコンパクションのタイミング未実装 | §4.4.2-11（v13.4方針確定）と対の論点。当初「コンパクションはロード時のみ」としていたが、保存直後のファイルをメモ帳や他アプリで開く／分析する用途を考慮すると、保存されたファイル上でIdが飛び飛びのままなのは望ましくないと判明。**方針**：コンパクションは`JsonProjectFileSerializer.Save()`内でのみ実施し、書き出し直前に種別ごとの`旧Id→新連番Id`対応表を作ってシリアライズ結果にのみ適用する。ライブなセッション上のオブジェクトのId・Undo/Redoスタック・既存コマンドが保持するインスタンス参照は一切変更しない（discard-and-regenerateの精神通り、ファイル表現はライブモデルから都度導出される派生データという位置づけ）。次回`Load()`時はファイル記載のId（コンパクション済み）をそのまま使い、単調カウンタはロード時の最大Id+1から再開する。実装未着手 |
| 22 | 🔴 | `EntryPoint.Type`変更ポリシーの見直し | 「EntryPoint.Type変更は削除＋新規作成として扱う（同一IDでの書き換えは実装しない）」の妥当性再検討。DependencyResolverによるdirty通知の仕組みが整った現在では、Type変更をin-place属性変更（ChangeRailAttributesCommand型）に緩和できる可能性があると実装担当より指摘あり。ただし、DependencyResolverが解決するのは「変更時に誰に通知するか」のみであり、「Type変更後も既存のStationPath.Waypoints・StationConnectionSegment構成がType前提のまま意味的に無効化されていないか」という整合性検証は別問題（設計原則「破棄・再生成 over 自動マイグレーション」の趣旨）。判断には`EntryPointValidator.cs`／`StationPathValidator.cs`の実装内容の監査が必要（未実施）。判断保留のまま次アクション候補としては優先度を下げる。**v13.9追記**：`NoneEndpoint`から`BoundaryPoint`/`EntryPoint`/`BufferStop`への種別確定についても、`EntryPoint.Type`変更と同様「削除＋新規作成」パターンを踏襲する想定である旨、v13.9セッションで確認した（実装は未着手）。本項目の判断（in-place変更への緩和可否）とあわせて今後検討する |
| 23 | 🟠~🟡 | 構内配線図キャンバスUI実装 | FloorUnitDetailViewModelの暫定リストUI（v13.8で実装）をキャンバスベースのRail/Platform/SignalSet配置・選択・属性パネル編集に置き換える。Rail端点クリック→属性タブでの端点種別選択のインタラクション実装を含む。規模が大きいため独立セッションでの着手を推奨。**v13.9更新**：着手前提として、`NoneEndpoint`のデータモデル新設と`RailCreationWorkflow`の生成順序再設計（無効な中間状態を作らない方式への変更）を完了。旧`CreateRailCommand`が生成していた「両端未接続の仮Rail」パターンは撤廃され、Railは生成時点から常に確定済みの端点を持つ。これによりキャンバス上でのドラッグ新規作成（始点→終点を引く操作）を、途中で無効な状態を経由せず1つのUndo単位として自然に実装できる土台が整った。次回セッションではキャンバス本体（グリッドスナップ、ドラッグ操作のUI実装、既存端点クリックでの接続、属性タブ連動）に着手する。**v13.11追記**：ホスティング方式を確定：駅編集モード＝他の詳細編集画面と同一のContentControl置換方式で全画面表示（§7テンプレートの左右分割規約は適用しない例外）、列車時刻表編集モード＝ドッキング領域内の5番目のドキュメント種別として追加。**v13.13：詳細設計を確定（実装は次回セッション）**。3モード制（閲覧／線路・端点・ホーム編集／構内進路編集、モード切替はキーバインド非依存のツールバーボタンでも可能）を採用。「線路・端点・ホーム編集モード」の操作系：単純ドラッグ＝端点座標変更（RailControlPoints追加は将来スコープ）、Ctrl+ドラッグ＝新規Rail作成、Shift+ドラッグ＝範囲選択（単クリック＝個別選択、複数選択後の一括操作は将来対応）。**収束変換ルール（新規確定）**：ドラッグの結果、ある座標に収束したRail参照数のみで対象種別が一意に決まる（N=1：EntryPoint/BufferStopのままユーザーが明示的に種別選択、既存フロー据置／N=2：BoundaryPoint／N=3,4：Switcher（既存Switcherの拡張時は`Mechanism`/`ValidRoutes`をクリアし再設定要求）／N≧5：エラー「任意の点を参照するRailの数は4以下である必要があります」でロールバック）。BoundaryPoint/Switcherへの変換確認はモーダルで、キャンセル時は操作前へ完全ロールバック（1ドラッグ＝1Undo単位、モーダル確定までを含む）。**カスケード削除は導入延期**：収束変換によって既存EntryPoint/BoundaryPoint/BufferStopが暗黙に消滅する場合、関連StationPathが存在すればブロックしてエラー表示（既存Delete系と同じ「参照があれば拒否」方針を踏襲）。汎用カスケード削除機構は§4.4.2-26（将来構想）へ切り出し。端点ダブルクリック→編集モーダル（共通1コンポーネント、NoneEndpointは種別選択→§4.4.2-24ワークフロー、確定済み端点はName/Type編集＋削除の差分判定Save、Switcherのみ追加でPort関連設定＝`Mechanism`/`ValidRoutes`編集を有効化）。Rail/Platformは常設サイドパネルでの単クリック選択編集（Railは`EndpointA`/`EndpointB`除く4フィールドのみ）。今回セッションのスコープは「閲覧モード」＋「線路・端点・ホーム編集モード」（Switcher・SignalSetの独立配置は対象外、Switcherは収束変換経由でのみ生成）。「構内進路編集モード」はボタンのみ配置しプレースホルダーとし、本体（StationPathSuggester統合）は次々回以降。**v13.14：Algorithm層・Command層の実装完了（ViewModel/View層は次回）**。 |
| 24 | 🟠~🟡| 端点収束・確定変換ワークフロー未実装 | キャンバスUIで「点だけ置いた後に属性タブで種別を確定する」フロー（`NoneEndpoint`削除＋`BoundaryPoint`/`EntryPoint`/`BufferStop`いずれかの新規作成＋Rail側`EndpointA`/`EndpointB`参照の差し替え）に加え、（**v13.13でスコープ拡大**：）ドラッグ操作によりRail端点が同一座標に収束した際のBoundaryPoint/Switcher変換も本項目に統合。後者は既存`RailMerger`（N=2のRail統合、収束点の端点情報ごと破棄する別実装）とは異なる新規ワークフローで、収束点に新規BoundaryPoint/Switcherを作成し、収束した各Railの端点参照をそこへ張り替える。Switcher新規作成時のPortIndex機械採番（座標・角度に依存せず、収束したRailを`Rail.Id`昇順で0〜N-1に採番する案を暫定提示、実装担当最終確認待ち）、および既存Switcher拡張時のPort追加ロジックの実装も含む。**v13.14：Algorithm層・Command層を実装完了**。新規4ファイル：`RailEndpointConvergenceResolver`（収束検出`FindConvergingEndpoints`・分類`Classify`・Switcher Port機械採番`AssignSwitcherPorts`・StationPathブロックチェック`FindBlockingStationPaths`の純粋関数群）、`RailEndPointRefChanger`（複数Rail端点の参照一括張替えコマンド、`ChangeRailAttributesCommand`のスコープは拡張せず別クラスとして分離）、`DeleteFloorUnitObjectCommand<TId,T>`（`CreateFloorUnitObjectCommand<TId,T>`と対称な汎用無条件削除、参照チェックは呼び出し元が事前完了させる前提）、`RailEndpointConvergenceWorkflow`（現在の収束状態と分類結果を比較し、不一致の場合のみ変換ステップ列を組み立てる`Reconcile`。Converge（新規Rail作成・既存端点への接続）とDiverge（Rail削除）を単一ロジックに統合、N=0＝Vanish（削除対象Rail自身のみが参照していた孤立端点は削除）を新規ケースとして追加）。加えて`ChangeSwitcherAttributesCommand`（PortCount／Mechanism／ValidRoutesの3フィールド、`SwitcherExpand`ケースの前提として新設）・`RailDeletionWorkflow`（既存`DeleteRailCommand`を無改修のまま1ステップ目として呼び出し、削除後の両端点座標で`FindConvergingEndpoints`→`Reconcile`を実行する`RailCreationWorkflow`と対称なラッパー、`ProjectSession.SwitcherIds`を新規追加）を実装。ビルド成功確認済み（xUnitテストは次回）。**新規発見（§4.4.2-28）**：`Reconcile`のVanish/Keepケースでは`FindBlockingStationPaths`によるStationPathブロックチェックが呼ばれておらず（BoundaryPoint/SwitcherNew/SwitcherExpandケースのみ`AddDeleteStepsForVanishingEndpoints`経由で呼ばれる）、Rail削除により消滅する端点がStationPathから参照されているケースを検知できない可能性がある。次回セッションで対応要否を判断する。キャンバスUI本体（§4.4.2-23）のViewModel/View層は次回セッション |
| 25 | 🔴~🟠 | FloorUnit並べ替えUI未配線 | `ReorderFloorUnitsCommand`（v13.10新設）のViewModel/View配線が未着手。駅詳細画面のFloorUnit一覧に並べ替え操作（ドラッグ&ドロップ／↑↓ボタン等）が一切実装されていない。v13.2時点でM2スコープから見送られた状態が継続しているだけで新規の不具合ではないことをv13.11セッションで確認済み。UI操作方式は未決定のまま、実装担当の判断により優先度を下げて起票 |
| 26 | 🔴~🟠 | 汎用カスケード削除機構（将来構想） | 項目33の収束変換設計セッション中に浮上。現行の全Delete系コマンドは「参照元が残っていればブロックしてエラー表示」方針（構造的防止の一環）だが、実装担当より「影響するオブジェクトを道連れに削除する複合コマンド」の提案があった。**方針**：全データモデルに対応するUI・機能が揃ってから着手する将来構想として記録し、現時点では既存のブロック方式を維持する（収束変換ワークフローでも同様にブロック方式を採用）。着手時は影響範囲が大きい（「既存Delete commands再監査」と同様、型ごとに「安全に道連れ削除してよい関係」と「ブロックすべき関係」の判断基準の精査が必要）ため、単独セッションでの設計から始めること |
| 27 | 🔴~🟠 | `RailSplitter`（`RailMerger`と対をなす分離機構、将来構想） | 収束変換設計セッション中に実装担当より提案。動作機序：元の収束点1点の`Base`をコピーし、ユーザーが操作しない側の端点へ値を継承、非操作側の残存Rail端点数から新Typeを決定（N=2→BoundaryPoint等、収束ルールを逆から適用）。ユーザーが操作した側は単純な`NoneEndpoint`を新規発行する。これが無い間は、既にBoundaryPoint/Switcherとして収束済みの端点はドラッグ操作自体を無効化する（掴めない・移動不可）という制約で対応する。将来スコープ、設計のみ記録・実装は未着手 |
| 28 | 🔴 | `RailEndpointConvergenceWorkflow.Reconcile`のVanish/KeepケースでStationPathブロックチェック欠落 | `RailDeletionWorkflow`実装過程で新規発見。`FindBlockingStationPaths`（消滅する端点がStationPath.Waypointsから参照されていないかのチェック）は、BoundaryPoint／SwitcherNew／SwitcherExpandケースの`AddDeleteStepsForVanishingEndpoints`経由でのみ呼ばれており、Vanish（N=0、Rail削除により孤立化した端点の削除）・Keep（N=1への縮退、旧BoundaryPoint/SwitcherからNoneEndpointへの差し戻し時の旧端点削除）の両ケースでは呼ばれていない。Rail削除によって消滅する端点が実際にStationPathから参照されているケースがあり得るかを検証した上で、対応要否を判断する必要がある。実装は未着手 |

#### 4.4.3 優先度：低／保留（将来構想／スコープ外）

| # | 項目 | 内容 |
|---|---|---|
| 1 | `TrainOperation.operationNumber`の時系列的な再利用 | 自社線内の運用番号を他社直通で変更後、自社線復帰時に再利用する等の時系列的再割当てはスコープ外。一意性制約は「同一TimeTableSet内で常に一意」のみ |
| 2 | `TrainOperationChainResolver`のDecoupling/Coupling経路テストカバレッジ空白 | `TryFollowDecoupling`／`TryFollowCoupling`を経由する経路のテストケースが未実装。実装はコンパイル・既存テストの通過という意味では健全だが、新規分岐自体はテストの裏付けがない |
| 3 | `current.StopTimes`辞書列挙順序への暗黙依存 | `TryFollowDecoupling`／`TryFollowCoupling`は`Dictionary<StopKey, StopTime>`を`foreach`で列挙しており、複数のDecoupling/Coupling該当StopKeyが同一Train内に存在する場合の処理順序が言語仕様上保証されない。`StopKeySequenceBuilder.BuildVisitedStopKeys`同様、時系列順の明示的リストで走査すべき |
| 4 | `StationWorkValidator.ValidateCoupling`の`PartnerTrainId`実在チェックの参照範囲 | `ValidationContext.Trains`が「保存対象TimeTableSet単位」か「プロジェクト全体」かは`SaveValidationRunner`側の`ValidationContext`構築ロジック未確認のため要検証 |
| 5 | `IValidationIssue`のエラーコード一般化 | 現状`Message`（string）と`Severity`のみを持ち、検証失敗の種別を機械的に判別する手段がない（`StationConnectionSegmentOverlapCrossValidatorTests`等、テスト側で`Assert.Contains`による文字列マッチングに頼らざるを得ない）。将来的にエラーコード（enum等）や構造化された対象ID一覧をIssueに持たせる設計に拡張する余地がある。優先度低・スコープ外 |
| 6 | `StationConnectionSegment.BaseRunTimeSec`削除に伴う既存プロジェクトファイルの読込互換性 | 既存JSON保存ファイルに`BaseRunTimeSec`フィールドが残っている場合、無視して読み飛ばすか`SchemaVersion`更新で明示的に非対応とするかは別途検討する。優先度低・スコープ外 |

## 5. プロジェクトディレクトリ構造

| 項目 | 確定内容 |
|---|---|
| IDE | Visual Studio（.NET開発ワークロード）＋Avalonia for Visual Studio拡張 |
| ビルドシステム | `dotnet` CLI＋`.sln`/`.csproj`（MSBuild） |
| ライセンス | Avalonia・関連ライブラリはMIT。個人開発・非商用・フリーライセンス公開前提でライセンス費用なし |


```
DiaEdit/
├ DiaEdit.sln                          # トップレベル。全プロジェクトを束ねるのみ
|
├ DiaEditCore/
│   ├ DiaEditCore.csproj                # Avalonia非依存の純粋な.NETクラスライブラリ
│   └ (Model/ Algorithm/ Serialization/ ChangeNotification/ の各フォルダ。)
├ DiaEditCore.Tests/
│   ├ DiaEditCore.Tests.csproj          # DiaEditCoreのみを参照（Avalonia非依存でテスト可能）
│   └ (Algorithm群を中心にユニットテスト)
|
├ DiaEditApp.ViewModels/
│   ├ DiaEditApp.ViewModels.csproj      # Avalonia非依存。DiaEditCoreをプロジェクト参照
│   └ (ChangeNotificationBridge・画面ごとのViewModel・Composition/ の各フォルダ)
├ DiaEditApp.ViewModels.Tests/
│   ├ DiaEditApp.ViewModels.Tests.csproj # DiaEditApp.ViewModelsのみを参照
│   └ (ViewModelのコマンド実行・OnAffected()経由の変更通知を中心にユニットテスト)
|
└ DiaEditApp/
    ├ DiaEditApp.csproj                 # Avalonia参照。DiaEditCore・DiaEditApp.ViewModelsをプロジェクト参照
    ├ Views/                            # .axaml（Avalonia XAML）
    ├ Rendering/                        # 
    ├ Services/                         # Avalonia依存のサービス実装（ファイルダイアログ等）
    └ Composition/                      # App.axaml.cs起動時のDI登録
```

**テスト・静的解析方針**

| 項目 | 内容 |
|---|---|
| テストフレームワーク | xUnit（Apache-2.0）。`DiaEditCore`・`DiaEditApp.ViewModels`はAvalonia非依存でCI高速化。View層の見た目テストは対象外（必要時にAvalonia.Headlessを検討） |
| MVVM・DIパッケージ | `CommunityToolkit.Mvvm`（NuGet）／`Microsoft.Extensions.DependencyInjection`（NuGet） |
| 依存取得方法 | NuGet（`dotnet add package`）。個人開発規模のため追加のパッケージ管理ツールは導入しない |
| 静的解析設定 | `.editorconfig`で`dotnet_diagnostic.CS8509.severity = error`（switch式網羅性チェックのエラー化）。プロジェクト初期設定時に必ず組み込む |
| ID型・値型規約 | 全エンティティID型は`readonly record struct`。Undo/Redoスナップショット等の不変性が必要な箇所は`record`で統一 |

`ViewModel→Core Library`の変更通知は`DependencyResolver`の`affectedIds`をそのままトリガーとして流用する。

**依存の要点**

- `DiaEditApp.ViewModels` は `DiaEditCore` を参照しており、`ChangeNotificationBridge` と `ProjectFile` などのコア型を利用する。
- `Composition` は `AddDiaEditCore()` で `CommandInvoker`と`ProjectSession` を Singleton 登録し、コア機能の起点を提供する。
- `Session` の `ProjectSession` が、`CommandInvoker` と `TimeTableSetCache` を管理し、検証と再計算のライフサイクルを統制する。
- `Model` は、`ProjectFile`、駅・路線・列車・時刻表などの中核ドメインを定義し、他の全モジュールの入力・出力の基盤になる。
- `Algorithm` は `Model` を入力にして経路・競合・時刻計算などの解決を行う。
- `Serialization` は `Model` を JSON に保存／読み込みし、各種検証と組み合わせてプロジェクト整合性を担保する。
- `ChangeNotification` は、`CommandInvoker` からの更新通知を `ProjectSession` / ViewModel 側へ伝えるためのインターフェースとして機能する。

**依存関係の方向性**

- 方向は「上位レイヤーが下位レイヤーに依存する」形。
- 実際には `Algorithm`, `Commands`, `Serialization`, `Session` などが `Model` に依存しており、`Session` が全体の orchestration を担当している。
- `ViewModels` はコアの公開 API を利用するクライアントとして位置づけられる。

<!-- DOCGEN:CHAPTER DiaEditCore -->
## 6. DiaEditCore

### 6.1 Model

**ID型一覧**：`BoundaryPointId`, `BoundaryPointObjectId`, `BufferStopId`, `BufferStopObjectId`, `CarCompositionId`, `CarCompositionObjectId`, `CarConsistId`, `CarConsistObjectId`, `CarId`, `CarObjectId`, `DiagramRevisionId`, `DiagramRevisionObjectId`, `DisplayContextId`, `DisplayContextObjectId`, `EntryPointId`, `EntryPointObjectId`, `FloorUnitId`, `FloorUnitObjectId`, `MainRouteId`, `MainRouteObjectId`, `NoneEndpointId`, `NoneEndpointObjectId`, `PlatformId`, `PlatformObjectId`, `RailId`, `RailObjectId`, `ServiceRouteId`, `ServiceRouteObjectId`, `StationConnectionId`, `StationConnectionObjectId`, `StationConnectionSegmentId`, `StationConnectionSegmentObjectId`, `StationId`, `StationObjectId`, `StationPathId`, `StationPathObjectId`, `SwitcherId`, `SwitcherObjectId`, `TemporaryRestrictionId`, `TemporaryRestrictionObjectId`, `TimeTableSetId`, `TimeTableSetObjectId`, `TrainId`, `TrainObjectId`, `TrainOperationId`, `TrainTypeId`, `TrainTypeObjectId`, `VehicleTypeId`, `VehicleTypeObjectId`, `VirtualConflictObjectId`, `VirtualConflictObjectIdObject`

---

#### `DiaEditCore.Model.DisplayName` (class)

| Field | Type |
|---|---|
| Name | `string` |
| Abbreviation | `string?` |
| Translations | `Dictionary<string, string>` (既定値 `new()`) |

---

##### `public string Resolve(string localeCode)`

---

##### `public DisplayName Clone()`

Name/Abbreviation/Translationsをディープコピーした新しいDisplayNameを返す。
DisplayNameは参照型（class）かつTranslationsがミュータブルなDictionaryのため、
スナップショット保持（UndoableCommand等）で外部参照を残さないために使う。

---

##### `public bool Equals(DisplayName? other)`

値等価の実装（§9.2項目31：Save時差分判定のため新設）。DisplayNameは参照型だが、
スナップショット比較（変更なしなら保存操作自体を無効化する）用途では内容の一致を
見る必要があるため、Translationsも含め中身で比較する。
StationSnapshot（record）はメンバの既定比較にEqualityComparer&lt;DisplayName&gt;.Defaultを
使うため、これが未実装だと参照比較にフォールバックし、Clone()由来の別インスタンス同士は
常に不一致と判定されてしまう（§9.2項目31実装時に実機で確認された不具合の原因）。

---

##### `public override bool Equals(object? obj)`

---

##### `public override int GetHashCode()`

---

#### `DiaEditCore.Model.IIntId` (interface)

int一つだけを値として持つID型に実装させる共通インターフェース。
JSONシリアライズ時、ネストしたオブジェクトではなく素朴なintとして書き出すための
IntIdJsonConverterFactory（Serialization層）が、リフレクションを使わずこのインターフェース
経由でValueを読み書きするために使う。

| Field | Type |
|---|---|
| Value | `int` |

---

#### `DiaEditCore.Model.ObjectId` (record)

---

#### `DiaEditCore.Model.Point` (record struct)

| Field | Type |
|---|---|
| X | `int` |
| Y | `int` |

---

#### `DiaEditCore.Model.ProjectFile` (class)

1プロジェクト1JSON方針における保存ファイルのルート集約オブジェクト。
設計方針（v11.38確定）：
- SchemaVersion：将来の保存形式変更に備え、先頭にスキーマバージョンを持たせる。
読込時に対応できないバージョンなら明示的にエラーとする（JsonProjectFileSerializer側で実施）。
- 所有構造ではなくフラットなコレクション（論点H①、ValidationContextと同型）：
Model層のオブジェクト間関係の大半はグラフ構造（forward-reference・共有参照・多対多）であり、
きれいな木構造を持つのはStation→FloorUnit程度に限られる。ProjectFile用に別の集約構造
（マッピング変換コード）を新設すると、Model層（5章）と二重管理になり保守コストと
データ破損リスクが増える。「読みやすさ」はJSON整形出力＋プロパティ宣言順序で確保する。
- プロパティ順序は推奨実装順序（下流→上流の依存順）に揃える。
ValidationContextとの違い：
- ValidationContextは「検証に必要な参照の寄せ集め」であり、IReadOnlyList＋init専用。
- ProjectFileは「保存・読込の実体」であり、List＋setterを持つ（読込後にUIから編集されるため）。
- ProjectFile → ValidationContextへの変換は JsonProjectFileSerializer 側の
ToValidationContext() 拡張メソッドで行う（1箇所に集約し、フィールド追加時の対応漏れを防ぐ）。

| Field | Type | 説明 |
|---|---|---|
| SchemaVersion | `int` (既定値 `1`) | 保存形式のスキーマバージョン。現バージョンは1。
読込時にJsonProjectFileSerializerが未対応バージョンを検知した場合は例外を送出する。 |
| ProjectSettings | `ProjectSettings` |  |
| Stations | `List<Station>` (既定値 `new()`) |  |
| FloorUnits | `List<FloorUnit>` (既定値 `new()`) |  |
| Rails | `List<Rail>` (既定値 `new()`) |  |
| NoneEndpoints | `List<NoneEndpoint>` (既定値 `new()`) |  |
| EntryPoints | `List<EntryPoint>` (既定値 `new()`) |  |
| BoundaryPoints | `List<BoundaryPoint>` (既定値 `new()`) |  |
| Switchers | `List<Switcher>` (既定値 `new()`) |  |
| BufferStops | `List<BufferStop>` (既定値 `new()`) |  |
| Platforms | `List<Platform>` (既定値 `new()`) |  |
| StationPaths | `List<StationPath>` (既定値 `new()`) |  |
| StationConnectionSegments | `List<StationConnectionSegment>` (既定値 `new()`) |  |
| MainRoutes | `List<MainRoute>` (既定値 `new()`) |  |
| StationConnections | `List<StationConnection>` (既定値 `new()`) |  |
| ServiceRoutes | `List<ServiceRoute>` (既定値 `new()`) |  |
| Cars | `List<Car>` (既定値 `new()`) |  |
| CarConsists | `List<CarConsist>` (既定値 `new()`) |  |
| CarCompositions | `List<CarComposition>` (既定値 `new()`) |  |
| VehicleTypes | `List<VehicleType>` (既定値 `new()`) |  |
| TrainTypes | `List<TrainType>` (既定値 `new()`) |  |
| Trains | `List<Train>` (既定値 `new()`) |  |
| TimeTableSets | `List<TimeTableSet>` (既定値 `new()`) |  |
| DiagramRevisions | `List<DiagramRevision>` (既定値 `new()`) |  |
| TemporaryRestrictions | `List<TemporaryRestriction>` (既定値 `new()`) |  |
| DisplayContexts | `List<DisplayContext>` (既定値 `new()`) |  |
| TrainOperations | `List<TrainOperation>` (既定値 `new()`) |  |

---

#### `DiaEditCore.Model.ProjectSettings` (record)

| Field | Type |
|---|---|
| ValidationRules | `ValidationRules` |
| DiagramBasedTimeSec | `int` |

---

#### `DiaEditCore.Model.TimeOfDaySec` (record struct)

時刻の内部表現。当日0:00を基準とした経過秒数（int）として保持する。
0〜86399: 当日 0:00〜23:59:59
86400以上: 24時以降の深夜帯表記（25:30 → 91800）をそのまま扱える
負値: 前日からの継続列車・日跨ぎダイヤの基準ズレ（例: -300 = 前日23:55）を表現できる
比較・演算規約（絶対時刻基準への正規化で統一）：
値自体が既にTimeTableSetの基準日0時からの経過秒数として定義されているため、
追加の正規化ロジックなしに単純なint比較・減算で完結する。

| Field | Type |
|---|---|
| Seconds | `int` |

---

##### `public int CompareTo(TimeOfDaySec other)`

---

#### `DiaEditCore.Model.TimeTableSetCache` (class)

| Field | Type |
|---|---|
| TrainNumberIndex | `Dictionary<string, TrainId>` (既定値 `new()`) |
| EntryPointConnectionIndex | `Dictionary<EntryPointId, List<StationConnectionId>>` (既定値 `new()`) |
| StationConnectionIndex | `Dictionary<StationId, List<StationConnectionId>>` (既定値 `new()`) |
| MainRouteConnectionIndex | `Dictionary<MainRouteId, List<StationConnectionId>>` (既定値 `new()`) |
| ScsUsedByIndex | `Dictionary<StationConnectionSegmentId, List<StationConnectionId>>` (既定値 `new()`) |
| TemporaryRestrictionBySegmentIndex | `Dictionary<StationConnectionSegmentId, List<TemporaryRestrictionId>>` (既定値 `new()`) |
| DerivedTrainsBySourceId | `Dictionary<TrainId, List<TrainId>>` (既定値 `new()`) |
| StopKeyReferenceIndex | `Dictionary<(TrainId TrainId, StopKey StopKey), List<StopKeyReferrer>>` (既定値 `new()`) |
| DepartureByStationTrackIndex | `Dictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>>` (既定値 `new()`) |
| ServiceRoutesByMainRouteIndex | `Dictionary<MainRouteId, List<ServiceRouteId>>` (既定値 `new()`) |
| FloorUnitDependentIndex | `Dictionary<FloorUnitId, List<ObjectId>>` (既定値 `new()`) |
| StationUsedByMainRouteIndex | `Dictionary<StationId, List<MainRouteId>>` (既定値 `new()`) |
| StationUsedBySegmentIndex | `Dictionary<StationId, List<StationConnectionSegmentId>>` (既定値 `new()`) |
| EntryPointUsedBySegmentIndex | `Dictionary<EntryPointId, List<StationConnectionSegmentId>>` (既定値 `new()`) |
| MainRouteUsedBySegmentIndex | `Dictionary<MainRouteId, List<StationConnectionSegmentId>>` (既定値 `new()`) |
| StationConnectionUsedByServiceRouteIndex | `Dictionary<StationConnectionId, List<ServiceRouteId>>` (既定値 `new()`) |
| ConflictObjectGroupingCache | `Dictionary<ObjectId, List<StationPathId>>` (既定値 `new()`) |

---

##### `public static ObjectId ToObjectId(StationPathWaypoint wp)`

---

##### `public void InvalidateConflictCache(ObjectId id)`

---

##### `public IReadOnlyList<StationPathId> GetConflictGroup(ObjectId id, Func<ObjectId, List<StationPathId>> rebuildFunc)`

---

##### `public void RebuildAll(IEnumerable<Train> trains, IEnumerable<StationConnection> stationConnections, IEnumerable<StationConnectionSegment> segments, IEnumerable<TemporaryRestriction> restrictions, IEnumerable<MainRoute> mainRoutes, IEnumerable<ServiceRoute> serviceRoutes)`

---

#### `DiaEditCore.Model.ValidationRules` (record)

保存時バリデーションの有効/無効・閾値を保持する。
プロジェクト単位（路線の特性によって適正値が変わるため）で設定可能とする。
ConflictChecker・RunTimeCalculator等のAlgorithm層はこれを参照して警告要否を判断する。

| Field | Type |
|---|---|
| MinDwellTimeSec | `int?` |
| MinHeadwaySec | `int?` |
| MinTurnaroundSec | `int?` |
| TrackEntryMarginSec | `int?` |
| TrackPassMarginSec | `int?` |
| EnableConflictDetection | `bool` |
| EnableCarLengthCheck | `bool` |

#### 6.1.1 Cars

#### `DiaEditCore.Model.Cars.Car` (class)

| Field | Type |
|---|---|
| Id | `CarId` |
| CarType | `string` |
| Placeholder | `int` (既定値 `0`) |
| IsPower | `bool` |
| LengthM | `double` |

---

#### `DiaEditCore.Model.Cars.CarComposition` (class)

| Field | Type |
|---|---|
| Id | `CarCompositionId` |
| Name | `string` |
| Identifier | `int` |
| CarConsistId | `CarConsistId` |

---

#### `DiaEditCore.Model.Cars.CarConsist` (class)

| Field | Type |
|---|---|
| Id | `CarConsistId` |
| VehicleTypeId | `VehicleTypeId` |
| Type | `CarConsistType` |
| Cars | `List<CarRef>` |

---

#### `DiaEditCore.Model.Cars.CarConsistType` (enum)

| Value | 説明 |
|---|---|
| Basic |  |
| Attached |  |

---

#### `DiaEditCore.Model.Cars.CarRef` (class)

| Field | Type |
|---|---|
| CarId | `CarId` |
| Position | `int` |

---

#### `DiaEditCore.Model.Cars.VehicleType` (class)

| Field | Type |
|---|---|
| Id | `VehicleTypeId` |
| Name | `string` |
| MaxSpeedKph | `double` |

#### 6.1.2 Routes

#### `DiaEditCore.Model.Routes.MainRoute` (class)

| Field | Type |
|---|---|
| Id | `MainRouteId` |
| Name | `DisplayName` |
| StationOrder | `List<StationId>` |
| IsLoop | `bool` (既定値 `false`) |
| DirectionReversalStations | `List<StationId>` (既定値 `new()`) |
| StationDisplayNameOverrides | `Dictionary<StationId, DisplayName>` (既定値 `new()`) |

---

#### `DiaEditCore.Model.Routes.ServiceRoute` (class)

| Field | Type |
|---|---|
| Id | `ServiceRouteId` |
| Name | `DisplayName` |
| IsAutoGenerated | `bool` |
| Segments | `List<ServiceRouteSegment>` |

---

#### `DiaEditCore.Model.Routes.ServiceRouteSegment` (class)

| Field | Type |
|---|---|
| MainRouteId | `MainRouteId` |
| FromStationIndex | `int` |
| ToStationIndex | `int` |
| IsUnidirectional | `bool` (既定値 `false`) |
| PairedMainRouteId | `MainRouteId?` |
| PairedFromStationIndex | `int?` |
| PairedToStationIndex | `int?` |
| ReversesAtBoundary | `bool` (既定値 `false`) |
| SelectedStationConnectionId | `StationConnectionId?` |
| PairedSelectedStationConnectionId | `StationConnectionId?` |

---

#### `DiaEditCore.Model.Routes.ServiceRouteSegmentExtensions` (class)

---

##### `public static bool IsPaired(this ServiceRouteSegment segment)`

---

#### `DiaEditCore.Model.Routes.StationConnection` (class)

| Field | Type |
|---|---|
| Id | `StationConnectionId` |
| Name | `string` (既定値 `""`) |
| MainRouteId | `MainRouteId` |
| Direction | `StationConnectionDirection` |
| Segments | `List<StationConnectionSegmentId>` |

---

#### `DiaEditCore.Model.Routes.StationConnectionDirection` (enum)

| Value | 説明 |
|---|---|
| Up |  |
| Down |  |

---

#### `DiaEditCore.Model.Routes.StationConnectionSegment` (class)

| Field | Type |
|---|---|
| Id | `StationConnectionSegmentId` |
| StationIdA | `StationId` |
| StationIdB | `StationId` |
| EntryPointIdA | `EntryPointId` |
| EntryPointIdB | `EntryPointId` |
| MainRouteId | `MainRouteId` |
| LengthM | `double` |
| SpeedLimitKph | `double` |

#### 6.1.3 Stations

#### `DiaEditCore.Model.Stations.FloorUnit` (class)

駅階層を表現する。

| Field | Type | 説明 |
|---|---|---|
| Id | `FloorUnitId` | 駅階層識別子 |
| StationId | `StationId` | 駅階層の所属する駅の識別子 |
| Name | `string` (既定値 `""`) | 駅階層名称 |
| DisplayOrder | `int` | 駅詳細画面における表示順 |

---

#### `DiaEditCore.Model.Stations.Station` (class)

駅や信号場、車両基地を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `StationId` | 駅識別子 |
| DisplayName | `DisplayName` | 駅名称 |
| Type | `StationType` | 駅種別 |
| OperatingCode | `string` (既定値 `""`) | 事業者管理用コード |
| TelegraphCode | `string` (既定値 `""`) | 電報略号 |
| ShowsInStationTimetableOverride | `bool?` | 駅時刻表の対象判別用フラグ |

---

##### `public bool ResolveShowsInStationTimetable()`

駅時刻表の対象判別用フラグをデフォルトに切り替えるメソッド

**Returns**
Standard, HaltならTrue、SignalStation, DepotならFalse

---

#### `DiaEditCore.Model.Stations.StationType` (enum)

駅種別

| Value | 説明 |
|---|---|
| Standard | 停車場。在線検知の境界となる。 |
| Halt | 停留場。在線検知の境界とならない。 |
| SignalStation | 信号場。在線検知の境界となる。 |
| Depot | 車両基地。在線検知の境界となる。 |

#### 6.1.4 Stations/FloorUnitObjects

**ID型一覧**：`BoundaryPointEndpointRef`, `BoundaryPointWaypoint`, `BufferStopEndpointRef`, `BufferStopWaypoint`, `EntryPointEndpointRef`, `EntryPointWaypoint`, `NoneEndpointRef`, `SwitcherWaypoint`

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BoundaryPoint` (class)

| Field | Type |
|---|---|
| Id | `BoundaryPointId` |
| Base | `FloorUnitObjectBase` |
| Name | `string` (既定値 `""`) |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BufferStop` (class)

| Field | Type |
|---|---|
| Id | `BufferStopId` |
| Base | `FloorUnitObjectBase` |
| Name | `string` (既定値 `""`) |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.EntryPoint` (class)

| Field | Type |
|---|---|
| Id | `EntryPointId` |
| Base | `FloorUnitObjectBase` |
| Name | `string` (既定値 `""`) |
| Type | `EntryPointType` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.EntryPointType` (enum)

| Value | 説明 |
|---|---|
| Arrival |  |
| Departure |  |
| Both |  |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.FloorObjectRefExtensions` (class)

---

##### `public static ObjectId? ToObjectId(this RailEndpointRef r)`

---

##### `public static ObjectId ToObjectId(this StationPathWaypoint w)`

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.FloorUnitObjectBase` (class)

| Field | Type |
|---|---|
| FloorUnitId | `FloorUnitId` |
| Position | `Point` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.NoneEndpoint` (class)

| Field | Type |
|---|---|
| Id | `NoneEndpointId` |
| Base | `FloorUnitObjectBase` |
| Name | `string` (既定値 `""`) |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.Platform` (class)

| Field | Type |
|---|---|
| Id | `PlatformId` |
| Base | `FloorUnitObjectBase` |
| SecondaryPosition | `Point` |
| Name | `string` (既定値 `""`) |
| FacingRailIds | `List<RailId>` (既定値 `new()`) |
| EffectiveLength | `double?` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.PortPair` (record struct)

| Field | Type |
|---|---|
| PortA | `int` |
| PortB | `int` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.Rail` (class)

| Field | Type |
|---|---|
| Id | `RailId` |
| Name | `string` (既定値 `""`) |
| LengthM | `double` |
| SpeedLimitKph | `double` |
| Role | `RailRole` |
| EndpointA | `RailEndpointRef` |
| EndpointB | `RailEndpointRef` |
| ControlPoints | `List<RailControlPoint>` (既定値 `new()`) |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.RailControlPoint` (class)

| Field | Type |
|---|---|
| Point | `Point` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.RailEndpointRef` (record)

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.RailRole` (enum)

| Value | 説明 |
|---|---|
| Normal |  |
| Track |  |
| Shunting |  |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.StationPath` (class)

| Field | Type |
|---|---|
| Id | `StationPathId` |
| FloorUnitId | `FloorUnitId` |
| Name | `string` |
| Direction | `StationPathDirection` |
| Waypoints | `List<StationPathWaypoint>` |
| AdjustmentSec | `int` (既定値 `0`) |
| ManualConflictObjectIds | `List<VirtualConflictObjectId>` (既定値 `new()`) |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.StationPathDirection` (enum)

| Value | 説明 |
|---|---|
| Arrival |  |
| Departure |  |
| Shunting |  |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.StationPathWaypoint` (record)

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.SwitchMechanism` (class)

| Field | Type |
|---|---|
| RootPortIndex | `int` |
| NormalPortIndex | `int` |
| ReversePortIndex | `int` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.Switcher` (class)

| Field | Type |
|---|---|
| Id | `SwitcherId` |
| Base | `FloorUnitObjectBase` |
| PortCount | `int` |
| Mechanism | `SwitchMechanism?` |
| ValidRoutes | `List<PortPair>` (既定値 `new()`) |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.SwitcherEndpointRef` (record)

| Field | Type |
|---|---|
| Id | `SwitcherId` |
| PortIndex | `int` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.SwitcherRoutingExtensions` (class)

---

##### `public static IReadOnlySet<PortPair> GetTraversablePairs(this Switcher switcher)`

Switcherの構造（mechanism / validRoutes）から、通行可能なPortペアの集合を都度計算する。
永続化はしない派生値。N=3はroot-normal・root-reverseの2組、N=4はvalidRoutesそのもの。
PortIndexの割り当て順序に業務的意味を持たせないため、各ペアはPortA&lt;=PortBに正規化する。

---

##### `public static PortPair Normalize(int a, int b)`

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.VirtualConflictObject` (class)

| Field | Type |
|---|---|
| Id | `VirtualConflictObjectId` |
| FloorUnitId | `FloorUnitId` |
| Name | `string` (既定値 `""`) |

#### 6.1.5 TimeTable

#### `DiaEditCore.Model.TimeTable.DateRange` (record struct)

| Field | Type |
|---|---|
| Start | `DateTime` |
| End | `DateTime` |

---

#### `DiaEditCore.Model.TimeTable.DiagramRevision` (class)

| Field | Type |
|---|---|
| Id | `DiagramRevisionId` |
| BaseRevisionId | `DiagramRevisionId?` |
| TimeTableSetIds | `List<TimeTableSetId>` (既定値 `new()`) |

---

#### `DiaEditCore.Model.TimeTable.DisplayContext` (record)

ダイヤグラム・駅時刻表の表示対象を定義する（5.15節）。
「路線系統」を基準に表示範囲を定義し、そこにServiceRouteに属するTrainを投影する。
stationOrderはこのMainRouteRangesから導出される表示用キャッシュであり、
ここには持たせない（Algorithm層のresolveDisplayContextStationOrder＋
TimeTableSetCache.stationOrderByDisplayContextIdで扱う。6章参照）。

| Field | Type |
|---|---|
| Id | `DisplayContextId` |
| Name | `DisplayName` |
| MainRouteRanges | `IReadOnlyList<MainRouteRange>` |

---

#### `DiaEditCore.Model.TimeTable.MainRouteRange` (record)

DisplayContextが対象とする区間。MainRouteから生成するのが基本だが、
ユーザーが任意の区間列を組んで独自のDisplayContextを作ることも可能。

| Field | Type |
|---|---|
| MainRouteId | `MainRouteId` |
| FromIndex | `int` |
| ToIndex | `int` |

---

#### `DiaEditCore.Model.TimeTable.Rail` (record)

| Field | Type |
|---|---|
| RailId | `RailId` |

---

#### `DiaEditCore.Model.TimeTable.RestrictionTarget` (record)

---

#### `DiaEditCore.Model.TimeTable.Segment` (record)

| Field | Type |
|---|---|
| StationConnectionSegmentId | `StationConnectionSegmentId` |

---

#### `DiaEditCore.Model.TimeTable.TemporaryRestriction` (record)

| Field | Type |
|---|---|
| Id | `TemporaryRestrictionId` |
| Target | `RestrictionTarget` |
| ExtraRunTimeSec | `int?` |
| SpeedLimitKph | `int?` |
| DateRange | `DateRange` |
| Note | `string` |

---

#### `DiaEditCore.Model.TimeTable.TimeTableSet` (class)

| Field | Type |
|---|---|
| Id | `TimeTableSetId` |
| Name | `string` |
| TrainIds | `List<TrainId>` (既定値 `new()`) |

#### 6.1.6 TimeTable/Trains

#### `DiaEditCore.Model.TimeTable.Trains.CouplingWork` (class)

| Field | Type |
|---|---|
| PartnerTrainId | `TrainId` |
| PartnerStopKey | `StopKey` |
| AttachToFront | `bool` (既定値 `false`) |

---

#### `DiaEditCore.Model.TimeTable.Trains.CutGroupEntry` (class)

| Field | Type |
|---|---|
| CarCompositionId | `CarCompositionId` |
| OperationNumber | `string` |

---

#### `DiaEditCore.Model.TimeTable.Trains.DecouplingWork` (class)

| Field | Type |
|---|---|
| FrontGroup | `List<CutGroupEntry>` |
| RearGroup | `List<CutGroupEntry>` |
| IsRearBase | `bool` (既定値 `false`) |

---

#### `DiaEditCore.Model.TimeTable.Trains.LineStyle` (enum)

| Value | 説明 |
|---|---|
| Solid |  |
| Dashed |  |
| Dotted |  |

---

#### `DiaEditCore.Model.TimeTable.Trains.NextTrainType` (enum)

| Value | 説明 |
|---|---|
| Other |  |
| TypeChange |  |
| InfoChange |  |
| SameTrain |  |
| Coupling |  |

---

#### `DiaEditCore.Model.TimeTable.Trains.PrevTrainOperationOverride` (class)

| Field | Type |
|---|---|
| CarCompositionId | `CarCompositionId` |
| NewOpNumber | `string` |

---

#### `DiaEditCore.Model.TimeTable.Trains.SplitOriginRef` (class)

| Field | Type |
|---|---|
| OriginTrainId | `TrainId` |
| OriginStopKey | `StopKey` |

---

#### `DiaEditCore.Model.TimeTable.Trains.StartOpCarSlot` (class)

| Field | Type |
|---|---|
| Position | `int` |
| CarCompositionId | `CarCompositionId` |
| OperationNumber | `string` |

---

#### `DiaEditCore.Model.TimeTable.Trains.StationWork` (class)

| Field | Type |
|---|---|
| Type | `StationWorkType` |
| StartOpConsist | `List<StartOpCarSlot>` (既定値 `new()`) |
| PrevTrainOperationOverrides | `List<PrevTrainOperationOverride>` (既定値 `new()`) |
| DecouplingDetail | `DecouplingWork?` |
| CouplingDetail | `CouplingWork?` |
| SplitOrigin | `SplitOriginRef?` |
| NextTrainType | `NextTrainType?` |
| StationPathId | `StationPathId?` |
| StartOpSeconds | `int` (既定値 `-1`) |
| EndOpSeconds | `int` (既定値 `-1`) |

---

#### `DiaEditCore.Model.TimeTable.Trains.StationWorkType` (enum)

| Value | 説明 |
|---|---|
| None |  |
| PrevTrain |  |
| StartOp |  |
| EndOp |  |
| Shunting |  |
| NextTrain |  |
| Coupling |  |
| Decoupling |  |

---

#### `DiaEditCore.Model.TimeTable.Trains.StopKey` (record struct)

Trainの停車を一意に識別するキー。
VisitCount：Train自身のRunSegmentsが定める訪問順において、同一StationIdへの訪問が
何回目か（0-indexed）。列車全体の通し位置ではなく、駅ごとのローカルなカウンタである。
環状線・デルタ線折返しによる同一駅への複数回訪問を区別するために存在する。
生成は必ずStopKeySequenceBuilderを経由すること。VisitCountを手計算してnew StopKey(...)を
直接構築しないこと（RunSegments編集によりVisitCountは変わりうるため、複数箇所で
独自に算出すると規約の乖離が再発する）。

| Field | Type |
|---|---|
| StationId | `StationId` |
| VisitCount | `int` |

---

#### `DiaEditCore.Model.TimeTable.Trains.StopKeyReferenceKind` (enum)

| Value | 説明 |
|---|---|
| SplitOrigin |  |
| CouplingPartner |  |

---

#### `DiaEditCore.Model.TimeTable.Trains.StopKeyReferrer` (record struct)

あるStopKeyを外部から参照しているTrainと、その参照種別。

| Field | Type |
|---|---|
| ReferrerTrainId | `TrainId` |
| Kind | `StopKeyReferenceKind` |

---

#### `DiaEditCore.Model.TimeTable.Trains.StopKeySequenceBuilder` (class)

Train.RunSegmentsから、訪問順に対応するStopKey列を導出する唯一の生成点。
StopKey.VisitCountは「駅ごとのローカルな訪問回数」であり、この規約に従ってStopKeyを
生成できるのはこのクラスのみとする。
用途：
1. StopTimes書き込み側（RunSegments編集コマンド）が、新規追加・リキー時のキーを
本メソッドの戻り値から取得する
2. StopTimes読み出し側（CarConsistResolver等）が、訪問順にStopKeyを辿るために使う

---

##### `public static List<StopKey> BuildVisitedStopKeys(Train train)`

train.RunSegmentsが定める訪問順（先頭駅→各RunSegmentのToStationId）に対応する
StopKey列を、訪問順のまま返す。戻り値のインデックスiは「経路上でi番目の停車」を
意味するが、各StopKey自体のVisitCountは駅ごとのローカルカウンタである点に注意。

---

#### `DiaEditCore.Model.TimeTable.Trains.StopTime` (class)

| Field | Type |
|---|---|
| ArrivalSeconds | `int` (既定値 `-1`) |
| DepartureSeconds | `int` (既定値 `-1`) |
| IsStop | `bool` (既定値 `false`) |
| TrackRailId | `RailId?` |
| Works | `List<StationWork>` (既定値 `new()`) |

---

#### `DiaEditCore.Model.TimeTable.Trains.Train` (class)

| Field | Type | 説明 |
|---|---|---|
| Id | `TrainId` |  |
| TimeTableSetId | `TimeTableSetId` |  |
| TrainNumber | `string` |  |
| ServiceNumber | `int?` |  |
| ServiceRouteId | `ServiceRouteId` |  |
| TrainTypeId | `TrainTypeId` |  |
| TrainTypeName | `DisplayName` |  |
| Nickname | `DisplayName` |  |
| DefaultVehicleTypeId | `VehicleTypeId?` |  |
| SourceTrainId | `TrainId?` |  |
| Revision | `int` (既定値 `0`) |  |
| SourceRevisionAtCopy | `int?` |  |
| RunSegments | `List<TrainRunSegment>` (既定値 `new()`) |  |
| StopTimes | `IReadOnlyDictionary<StopKey, StopTime>` | 停車情報の読み取り専用ビュー。StopKeyの追加・削除・差し替えは外部から不可能
（StopKeySequenceBuilderを経由しない直接new StopKey(...)の挿入を型で防ぐ、§9.2項目9）。
StopTimeインスタンス自体のフィールド（ArrivalSeconds等）はこのスコープの対象外で、
依然として可変（将来の停車時刻編集コマンド設計時に別途検討）。 |
| StopTimesInternal | `Dictionary<StopKey, StopTime>` | StopTimes辞書への書き込み専用ルート。DiaEditCoreアセンブリ内
（SyncRunSegmentsToTrainCommand等の正規コマンド、およびテストのフィクスチャ構築）からのみ
使用すること。ViewModel/UI層（別アセンブリ）からは参照できない。 |
| IsProvisional | `bool` (既定値 `false`) |  |

---

#### `DiaEditCore.Model.TimeTable.Trains.TrainOperation` (class)

| Field | Type |
|---|---|
| Id | `TrainOperationId` |
| OperationNumber | `string` |

---

#### `DiaEditCore.Model.TimeTable.Trains.TrainRunSegment` (class)

| Field | Type |
|---|---|
| FromStationId | `StationId` |
| ToStationId | `StationId` |
| StationConnectionId | `StationConnectionId` |
| IsOverriddenFromTemplate | `bool` (既定値 `false`) |

---

#### `DiaEditCore.Model.TimeTable.Trains.TrainType` (class)

| Field | Type |
|---|---|
| Id | `TrainTypeId` |
| Name | `DisplayName` |
| DiagramColor | `string` |
| DiagramLineStyle | `LineStyle` |
| SortOrder | `int` |

### 6.2 Algorithm

#### 6.2.1 CacheBuilder

#### `DiaEditCore.Algorithm.CacheBuilder.BaseRunTimeIndexBuilder` (class)

DiagramRevision.BaseTimeTableSetIdが指すTimeTableSet内のTrain実績から、
区間ごとの基準所要時分インデックスを構築する。

---

##### `public static Dictionary<SelectionKey, int> Build(IReadOnlyList<Train> baseTimeTableSetTrains, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments)`

TimeTableSet 内の Train 実績から、区間ごとの基準所要時分インデックスを構築する。

**Parameters**

- `baseTimeTableSetTrains`: 実績を持つ Train の一覧。RunSegments と StopTimes を使用して区間の所要時分を計算する。
- `allStationConnections`: StationConnectionId から物理的な駅間接続を引くための一覧。
- `allSegments`: StationConnection が保持する SegmentId を解決するための全 Segment 一覧。

**Returns**
SelectionKey（区間＋停車条件＋車両種別）をキーとし、
その区間の実績所要時分（秒）を値とする辞書。

**Remarks**
処理内容：
1. Train の RunSegments を順に処理する。
2. StopKeySequenceBuilder により停車キー列を取得し、RunSegments と整合する列車のみ採用する。
3. StationConnectionId から StationConnection を取得する。
4. ResolveHopSegmentId により、今回の区間に対応する物理 Segment を1件だけ特定する。
5. from/to の StopTimes から出発・到着（または通過）時刻を取得する。
6. 経過秒数を計算し、SelectionKey を構築して辞書に登録する。

---

##### `public static StationConnectionSegmentId? ResolveHopSegmentId(StationConnection sc, StationId fromStationId, StationId toStationId, IReadOnlyList<StationConnectionSegment> allSegments)`

StationConnection.Segments の中から、from/to の StationId に無向一致する
StationConnectionSegment を1件だけ特定する。

**Parameters**

- `sc`: 対象の StationConnection。複数の Segment を持つ場合がある。
- `fromStationId`: 区間の始点駅 ID。
- `toStationId`: 区間の終点駅 ID。
- `allSegments`: SegmentId を解決するための全 Segment 一覧。

**Returns**
一致する Segment が 1 件の場合はその SegmentId。
一致が 0 件または複数件の場合は null（区間を特定できないため）。

**Remarks**
駅間は無向ペア（A-B と B-A は同じ物理区間）として扱うため、
StationIdA/StationIdB の順序は問わない。

---

#### `DiaEditCore.Algorithm.CacheBuilder.DepartureByStationTrackIndexBuilder` (class)

あるStationにおけるTrainのStopTimeから出発順リストを構築する。駅時刻表の表示に利用する予定。TrackOccupancyProvider.csに利用される。
始発駅（VisitSequence=0）のStopTimeが未設定／DepartureSecondsが未設定（-1）／
TrackRailId未設定のTrainは対象外とする。

---

##### `public static Dictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> Build(IReadOnlyList<Train> allTrains)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.DerivedTrainsBySourceIdIndexBuilder` (class)

TimeTableSetCache.DerivedTrainsBySourceIdIndex（TrainId→そのTrainをSourceTrainIdとして
複製されたTrainの一覧）の構築を担う。
Train.SourceTrainId（Train複製Paste時に設定される派生元参照）を1回走査するだけで
導出できる逆引きインデックス。
消費者はDependencyResolver.ResolveDirectDependents（TrainObjectIdケース）。
注：現行のDependencyResolver.ResolveDirectDependentsはTrainObjectId => [] と終端ノード扱いのため、
本インデックスを消費する新ケース追加はTrain削除コマンド実装時にあわせて行う（本Builder自体は
RebuildAllの空実装解消というスコープに留め、DependencyResolver側のswitch式変更は別タスクとする）。

---

##### `public static Dictionary<TrainId, List<TrainId>> Build(IEnumerable<Train> allTrains)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.EntryPointUsedBySegmentIndexBuilder` (class)

TimeTableSetCache.EntryPointUsedBySegmentIndex（EntryPointId→それをEntryPointIdA/Bに持つ
StationConnectionSegmentの一覧）の構築を担う。
StationConnectionSegment.EntryPointIdA/EntryPointIdBはSegment自身が直接保持する属性のため、
Segment列を1回走査するだけで導出できる（StationUsedBySegmentIndexBuilderと同型）。
A/Bは無向ペアだが、本Builderは両方を対称的にインデックスへ加えるだけなので、
v12.29のA/Bリネームによる意味論上の変更はない（機械的なフィールド名追従のみ）。
既存のEntryPointConnectionIndex（StationConnection経由の間接参照）では、どのSCにも
属さない孤立Segmentから直接参照されているEntryPointを捕捉できないため新設する
（Stationで既に対応済みの「孤立Segment問題」のEntryPoint版）。
消費者はDependencyResolver.ResolveDirectDependents（EntryPointObjectIdケース）のみ。

---

##### `public static Dictionary<EntryPointId, List<StationConnectionSegmentId>> Build(IEnumerable<StationConnectionSegment> allSegments)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.FloorUnitDependentIndexBuilder` (class)

TimeTableSetCache.FloorUnitDependentIndexの構築。FloorUnit配下のObjectの依存関係解決。
対象7種：NoneEndpoint／BoundaryPoint／EntryPoint／BufferStop／Switcher／Platform
（いずれもFloorUnitObjectBase.FloorUnitId経由）／StationPath（FloorUnitIdを直接保持）。

---

##### `public static Dictionary<FloorUnitId, List<ObjectId>> Build(IEnumerable<NoneEndpoint> noneEndpoints, IEnumerable<BoundaryPoint> boundaryPoints, IEnumerable<EntryPoint> entryPoints, IEnumerable<BufferStop> bufferStops, IEnumerable<Switcher> switchers, IEnumerable<Platform> platforms, IEnumerable<StationPath> stationPaths)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.MainRouteConnectionIndexBuilder` (class)

TimeTableSetCache.MainRouteConnectionIndex（MainRouteId→それを参照するStationConnectionの一覧）の構築を担う。
StationConnection.MainRouteIdはStationConnection自身が直接保持する属性のため、
EntryPointSequenceResolver等の展開処理を経由せず、StationConnection列を1回走査するだけで導出できる

---

##### `public static Dictionary<MainRouteId, List<StationConnectionId>> Build(IEnumerable<StationConnection> allStationConnections)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.MainRouteUsedBySegmentIndexBuilder` (class)

TimeTableSetCache.MainRouteUsedBySegmentIndex（MainRouteId→それをMainRouteIdに持つ
StationConnectionSegmentの一覧）の構築を担う。
StationConnectionSegment.MainRouteIdはSegment自身が直接保持する属性のため、
Segment列を1回走査するだけで導出できる（StationUsedBySegmentIndexBuilderと同型）。
既存のMainRouteConnectionIndex（StationConnection経由の間接参照）では、どのSCにも
属さない孤立Segmentから直接参照されているMainRouteを捕捉できないため新設する
（Stationで既に対応済みの「孤立Segment問題」のMainRoute版）。
消費者はDependencyResolver.ResolveDirectDependents（MainRouteObjectIdケース）のみ。

---

##### `public static Dictionary<MainRouteId, List<StationConnectionSegmentId>> Build(IEnumerable<StationConnectionSegment> allSegments)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.ScsUsedByIndexBuilder` (class)

TimeTableSetCache.ScsUsedByIndex（StationConnectionSegmentId→それを含むStationConnectionの一覧）の構築を担う。
StationConnection.Segments（SCSId配列）を1回走査するだけで導出できる逆引きインデックス
（値がListである理由：SCは「複々線・双単線区間における経路をユーザーが分かりやすいようにグルーピングするための用途」であり、
同一(MainRouteId, Direction)内で同一SCSが複数のStationConnectionから参照されることは許容しない（複線区間は物理的に1本の
経路しか持たないため）。SCSが複数のStationConnectionから共有されうるのは、あくまで双単線区間
（同一SCSを上り方向SCと下り方向SCの双方が参照する。すなわちDirectionが異なるケースのみ）。
同一方向内での重複はStationConnectionSegmentOverlapValidatorが検証対象とする。
消費者はDependencyResolver.ResolveDirectDependents（StationConnectionSegmentObjectIdケースの
うちStationConnection逆引き部分）のみ。

---

##### `public static Dictionary<StationConnectionSegmentId, List<StationConnectionId>> Build(IEnumerable<StationConnection> allStationConnections)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.SelectionKey` (record struct)

区間ごとの基準所要時分を選択するためのキー
StationConnectionSegment（物理的な駅間区間）と、
区間両端の停車条件、および車両種別を組み合わせて一意に特定する。

| Field | Type |
|---|---|
| SegmentId | `StationConnectionSegmentId` |
| FromIsStop | `bool` |
| ToIsStop | `bool` |
| VehicleTypeId | `VehicleTypeId?` |

> 同じ区間でも、停車・通過条件や車両種別によって所要時分が異なるため、
> これらをキーに含めて辞書化する。

---

#### `DiaEditCore.Algorithm.CacheBuilder.ServiceRoutesByMainRouteIndexBuilder` (class)

TimeTableSetCache.ServiceRoutesByMainRouteIndex（MainRouteId→経由するServiceRouteの一覧）の構築を担う。
用途はUI表示専用（MainRoute編集時の影響範囲表示）に限定し、DependencyResolverの
AffectedIds算出には使わない。

---

##### `public static Dictionary<MainRouteId, List<ServiceRouteId>> Build(IReadOnlyList<ServiceRoute> allServiceRoutes)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.StationAndEntryPointConnectionIndexBuilder` (class)

TimeTableSetCache.StationConnectionIndex（StationId→それを通るStationConnectionの一覧）と
TimeTableSetCache.EntryPointConnectionIndex（EntryPointId→それを通るStationConnectionの一覧）を
同時に構築する。
v12.29 SCS direction-agnostic renameセッションでEntryPointSequenceResolver.Resolveが
allMainRoutesを要求するシグネチャへ変更されたことに伴い、本Builderも同様にallMainRoutesを
受け取る（TimeTableSetCache.RebuildAllが既にmainRoutesを引数に持つため、呼び出し元の追従は軽微）。
用途はStation・EntryPointの「集合」を求めるだけで順序に依存しないため、向き解決の失敗
（MainRoute未検出等）でSegmentがスキップされても実害は小さいが、Resolve側の防御的スキップに
従い、結果的に該当StationConnectionの一部Stationが欠落しうる点は留意する
（EntryPointSequenceResolver.Resolveのコメント参照）。

---

##### `public static (

        Dictionary<StationId, List<StationConnectionId>> StationConnectionIndex,

        Dictionary<EntryPointId, List<StationConnectionId>> EntryPointConnectionIndex

    ) Build(IEnumerable<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<MainRoute> allMainRoutes)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.StationConnectionUsedByServiceRouteIndexBuilder` (class)

TimeTableSetCache.StationConnectionUsedByServiceRouteIndex（StationConnectionId→それを
SelectedStationConnectionId／PairedSelectedStationConnectionIdとして参照するServiceRouteの一覧）
の構築を担う。
参照元は§5.13.2「参照関係表」の
「ServiceRouteSegment | SelectedStationConnectionId／PairedSelectedStationConnectionId | StationConnection」。
両フィールドともnull許容（候補1件の区間は自動採用のためSelected系は未設定のまま）なので、
null値はインデックスへ加えない。
§5.14.4棚卸し表に記載の`StationConnectionUsedByServiceRouteIndexBuilder`候補を実装したもの。
消費者はDependencyResolver.ResolveDirectDependents（StationConnectionObjectIdケース）のみ
（§9.1項目3）。

---

##### `public static Dictionary<StationConnectionId, List<ServiceRouteId>> Build(IEnumerable<ServiceRoute> allServiceRoutes)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.StationUsedByMainRouteIndexBuilder` (class)

TimeTableSetCache.StationUsedByMainRouteIndex（StationId→それをStationOrderに含むMainRouteの一覧）の構築を担う。
MainRoute.StationOrderはMainRoute自身が直接保持する属性のため、MainRoute列を1回走査するだけで導出できる
消費者はDependencyResolver.ResolveDirectDependents（StationObjectIdケース）のみ）。

---

##### `public static Dictionary<StationId, List<MainRouteId>> Build(IEnumerable<MainRoute> allMainRoutes)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.StationUsedBySegmentIndexBuilder` (class)

TimeTableSetCache.StationUsedBySegmentIndex（StationId→それをFrom/ToStationIdに持つ
StationConnectionSegmentの一覧）の構築を担う。
StationConnectionSegment.FromStationId/ToStationIdはSegment自身が直接保持する属性のため、
Segment列を1回走査するだけで導出できる。
消費者はDependencyResolver.ResolveDirectDependents（StationObjectIdケース）のみ。

---

##### `public static Dictionary<StationId, List<StationConnectionSegmentId>> Build(IEnumerable<StationConnectionSegment> allSegments)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.StopKeyReferenceIndexBuilder` (class)

SplitOriginRef／CouplingWork.PartnerStopKeyによる、Train間のStopKey参照を逆引きできるIndexを構築する。
TimeTableSetCache.StopKeyReferenceIndexへの格納値として使う想定。
用途は2つに限定する：
1. RunSegments編集コマンドがAffectedIds算出時に「このStopKeyを参照しているTrain」を効率的に洗い出す
2. Cross Validator（SplitOriginRef／CouplingWork実在性検証）が検証対象を絞り込む
注意：StopKeyはRunSegments編集により値が変わりうる不安定なキーであり、ObjectIdグラフ
（DependencyResolver）とは意図的に別枠としている。このIndexをDependencyResolverの
ObjectId switch式に混在させないこと。

---

##### `public static Dictionary<(TrainId, StopKey), List<StopKeyReferrer>> Build(IReadOnlyList<Train> allTrains)`

---

#### `DiaEditCore.Algorithm.CacheBuilder.TemporaryRestrictionBySegmentIndexBuilder` (class)

TimeTableSetCache.TemporaryRestrictionBySegmentIndex（StationConnectionSegmentId→それを対象と
するTemporaryRestrictionの一覧）の構築を担う。
TemporaryRestriction.TargetはRestrictionTarget.Segment（StationConnectionSegmentId対象）／
RestrictionTarget.Rail（RailId対象）の判別共用体（TemporaryRestriction.cs）。本インデックスは
Segmentケースのみを対象とする（Railケースはこのインデックスの対象外。RailId起点の逆引きが
必要になった場合は別途RestrictionsByRailIndex等を新設して対応する。現時点でRail起点の
逆引き消費者はDeleteRailCommandのみであり、Rail削除時のチェックはTemporaryRestriction列を
直接1回線形走査すれば足りる規模のため、専用インデックス化は見送る）。
消費者はDependencyResolver.ResolveDirectDependents（StationConnectionSegmentObjectIdケースの
うちTemporaryRestriction逆引き部分）のみ。

---

##### `public static Dictionary<StationConnectionSegmentId, List<TemporaryRestrictionId>> Build(IEnumerable<TemporaryRestriction> allRestrictions)`

#### 6.2.2 Dependency

#### `DiaEditCore.Algorithm.Dependency.DependencyResolver` (class)

変更対象オブジェクト群から、依存関係グラフ（TimeTableSetCacheの逆引きインデックス）を辿って影響を受ける全オブジェクトIDを算出する。
停止性：プロジェクト内のオブジェクト総数は有限で、visited集合が既訪問ノードの再訪問を防ぐため、
依存グラフに循環（pairedMainRoute等の双方向参照）が存在しても必ず停止する。
一意性：ルールテーブル（ResolveDirectDependents）とキャッシュのインデックスが決定的な限り、
同一changedIds・同一プロジェクト状態から常に同一結果となる。
計算量：O(V+E)（V=影響を受けたオブジェクト数、E=辿った依存エッジ数）
呼び出しタイミング：削除系操作では、変更対象のインデックスがまだ整合している削除実行前に呼ぶこと。
execute()実行時点で一度だけ呼び、結果をコマンドのメンバ変数として保持し、undo()時には再算出しない。

---

##### `public static IReadOnlySet<ObjectId> ResolveAffected(IReadOnlySet<ObjectId> changedIds, TimeTableSetCache cache)`

---

##### `public static IEnumerable<ObjectId> ResolveDirectDependents(ObjectId current, TimeTableSetCache cache)`

単一オブジェクトの直接の依存先（1ホップ分）を返す。依存関係ルールテーブルの実体。
新しいObjectId派生型を追加した場合、CS8509(error)によりここでのケース追加漏れがビルドエラーとなる
（.editorconfigでdotnet_diagnostic.CS8509.severity=errorが設定済みのため）。
public化：ResolveAffected内部での波及探索用途に加え、削除系コマンドが
「直接の参照元が残っている場合はexecute時点で拒否する」判定に使う1ホップ専用の
問い合わせとしても利用する。
【v12.31 §9.1項目20対応】従来は末尾が`null => throw` ／ `not null => throw`の2ケースで
構成されており、`null ∪ not null`が参照型に対し数学的に完全網羅となるため、
CS8509によるコンパイル時網羅性チェックが実質無効化されていた。
`ObjectId.cs`の24種類の派生型全てを明示的にケース化することで是正する。
依存関係ルールが未設計の型は`=> []`（終端）と明示し、「未実装」と「意図的に終端」を
コード上で区別できない状態を解消する（コメントで区別を残す）。

#### 6.2.3 Routes

#### `DiaEditCore.Algorithm.Routes.BoundaryCheckResult` (record)

| Field | Type |
|---|---|
| BoundaryIndex | `int` |
| IsSatisfied | `bool` |

---

#### `DiaEditCore.Algorithm.Routes.BoundaryEntryPointResolver` (class)

境界駅のEPを、既存のEntryPointSequenceResolverの結果から取り出すためのラッパー。
ServiceRoutePathResolver・ReversalResolverが下請けとして利用する。
都度導出・非保存。複々線等で対応するStationConnectionが複数存在しうるため、
該当する全候補を返す（列車種別ごとに利用可能なEPが異なりうるため、この段階では絞り込まない）。

---

##### `public static IReadOnlyList<EntryPointSequenceElement> ResolveBoundaryEntryPoint(MainRouteId mainRouteId, int fromIndex, int toIndex, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments)`

---

##### `public static IReadOnlyList<StationConnectionId> ResolveBoundaryStationConnection(MainRouteId mainRouteId, int fromIndex, int toIndex, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments)`

---

#### `DiaEditCore.Algorithm.Routes.EntryPointSequenceElement` (record)

向き解決済みの1ホップ分の発着情報。StationConnectionSegment（生データ、無向のA/Bペア）とは
意図的に語彙を分ける：本レコードは常に「発側→着側」の向きが確定した後の出力である。
無向のA/B語彙をそのまま使い回すと、呼び出し側が向き解決済みかどうかを型から読み取れなくなるため
（§9.1 SCS direction-agnostic renameセッションでの指摘）、From/To語彙に戻す。

| Field | Type |
|---|---|
| FromStationId | `StationId` |
| ToStationId | `StationId` |
| FromEntryPointId | `EntryPointId` |
| ToEntryPointId | `EntryPointId` |

---

#### `DiaEditCore.Algorithm.Routes.EntryPointSequenceResolver` (class)

---

##### `public static EntryPointSequenceElement? ResolveOriented(StationConnectionSegment seg, StationId fromStationId, StationId toStationId)`

系統(i)：呼び出し側が既に走行方向の意図（fromStationId/toStationId）を持っている場合の
単一SCS向け無向マッチング。RailSequenceResolver.FindRailBetweenと同じ精神で、
StationIdA/StationIdBのどちらがfrom/toに一致するかを見て向きを確定する。
一致しない場合はnull（呼び出し側で「このSCSは該当ホップではない」として扱う）。

---

##### `public static IReadOnlyList<EntryPointSequenceElement> Resolve(StationConnection sc, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<MainRoute> allMainRoutes)`

系統(ii)：呼び出し側がStationConnection自体の情報（sc.Direction）しか持たない場合、
MainRoute.StationOrder上の隣接関係から各SegmentのA/Bどちらが発側かを機械的に解決する。
都度導出・非保存。
権威あるMainRouteの選定：各Segment自身が保持するMainRouteId（sc.MainRouteIdではなく
segment.MainRouteId）を使う。sc.MainRouteIdとの一致はStationConnectionValidatorが
保存時に保証する前提（§9.1セッション確定）のため、都度導出側はSegment自身の情報だけで
自己完結できる（呼び出し文脈への依存を減らす防御的設計）。
ループ対応：MainRoute.StationOrderが環状（先頭駅=末尾駅）の場合でも、Index比較をmod演算で
行うことで境界を正しくまたげる（非ループの場合はmodが実質無効化されるだけで同じ式で扱える）。
各Segmentは、StationConnectionValidatorの検証によりStationOrder上で隣接するペアである
ことが保存時に保証されている前提のため、直前ホップからの継承（チェーン）に頼らず、
全Segmentを毎回StationOrder上のIndexから独立に解決する（前ホップの解決結果に依存しないため
部分的なデータ不整合が後続ホップへ伝播しない、という利点もある）。
向きが解決できないSegment（MainRoute未検出／StationOrder上で隣接していない等の
データ不整合）は結果から除外する（discard-and-regenerateの都度導出処理として、
例外を送出せず可能な範囲で結果を返す既存の防御的実装方針を踏襲。保存時の検出は
StationConnectionValidator／StationConnectionSegmentValidator側の責務）。

---

#### `DiaEditCore.Algorithm.Routes.MainRouteChecker` (class)

「MainRoute整合性」・「境界駅の整合性条件」共通ロジック。
EntryPointId列（長さ2N、[From_0,To_0,From_1,To_1,...]）を受け取り、
隣接するSCS境界（EP[2i+1]⇔EP[2i+2]）で到着側Track集合と出発側Track集合が
1件以上重複することを検証する。isLoopの場合はEP[0]⇔EP[^1]境界も追加検証する。
都度導出・非保存。単一StationConnection（MainRoute整合性）・ServiceRoute結合列
（ServiceRoute整合性）の両方から共通して呼ばれる。

---

##### `public static IReadOnlyList<BoundaryCheckResult> CheckBoundaryConnectivity(IReadOnlyList<EntryPointId> entryPointSequence, bool isLoop, IReadOnlyDictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId> arrivalIndex, IReadOnlyDictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId> departureIndex)`

**Parameters**

- `entryPointSequence`: [From_0, To_0, From_1, To_1, ..., From_{N-1}, To_{N-1}]（長さ2N、N=SCS数）

---

##### `public static bool CanTransfer(EntryPointId arrivalEp, EntryPointId departureEp, IReadOnlyDictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId> arrivalIndex, IReadOnlyDictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId> departureIndex)`

到着側EntryPointが経由するTrack集合と、出発側EntryPointが経由するTrack集合が
1件以上重複するか（＝物理的に転線可能か）を判定する。CheckBoundaryConnectivityが
隣接Segment間の境界判定に使う内部ロジック（HasOverlap）をそのまま公開しただけの
ラッパーであり、判定基準は完全に同一。
ServiceRouteToRunSegmentsResolverが、ServiceRouteSegment内部の任意の乗換駅における
StationConnection切替の妥当性検証に使用する。

---

#### `DiaEditCore.Algorithm.Routes.ReversalResolver` (class)

編成前後反転の自動導出。単一MainRoute内のスイッチバック判定（ResolveDirectionReversalStations）と、
境界駅（MainRoute間）での折り返し判定（ResolveReversesAtBoundary）を、同一の判定基準で扱う。
（クラス冒頭のコメントは既存版から変更なし。以下メソッド本体のみv12.29対応）

---

##### `public static Dictionary<StationId, bool> ResolveDirectionReversalStations(MainRoute mainRoute, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<Rail> allRails, IReadOnlyDictionary<StationId, IReadOnlyList<StationPath>> stationPathsByStation)`

---

##### `public static bool? ResolveReversesAtBoundary(ServiceRouteSegment prevSegment, ServiceRouteSegment nextSegment, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<Rail> allRails, IReadOnlyList<StationPath> pathsAtBoundaryStation)`

---

#### `DiaEditCore.Algorithm.Routes.ServiceRoutePathResolver` (class)

ServiceRoute.segmentsをたどり、各境界駅のEntryPointSequenceElementを連結した
経路全体のEntryPointSequenceを導出する。出力型はEntryPointSequenceElementを
そのまま再利用する（境界駅だけを含む部分列として扱う。新しい型は起こさない）。
都度導出・非保存。

---

##### `public static IReadOnlyList<IReadOnlyList<EntryPointSequenceElement?>> ResolveServiceRoutePath(ServiceRoute sr, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments, CandidateSelector selector)`

ServiceRoute全体のEntryPointSequenceを導出する。
全segmentがpaired情報（IsUnidirectional=true かつ PairedMainRouteId等が設定済み）を
持つ場合のみ [primarySeq, pairedSeq] の2本を返す。1つでも非pairedのsegmentが
混ざる場合は、pairedSeqを一切生成せず [primarySeq] の1本のみを返す
（Pairedは「異なる路線を経由して同じ始終着駅を持つ運転系統」を表現するための仕組みであり、
一部segmentだけpaired側の値を持つ中途半端な状態は許容しない）。
各列の要素は、対応するBoundaryEntryPointResolverの候補が0件だった場合はnull
（「対応するStationConnectionが実在しない」の判定・エラー化は呼び出し側の責務とする。
BoundaryEntryPointResolverと同じ責務分離方針）。

---

#### `DiaEditCore.Algorithm.Routes.ServiceRouteStationOrderResolver` (class)

ServiceRouteが通る駅の順序付き全リスト（境界駅だけでなく中間駅も含む）を返す
非永続の導出処理。基準列車選択UIの停車パターン表示のために追加する。
都度導出・非保存。

---

##### `public static IReadOnlyList<StationId> ResolveServiceRouteStationOrder(ServiceRoute sr, IReadOnlyList<MainRoute> allMainRoutes)`

---

##### `public static IReadOnlyList<(StationId StationId, MainRouteId MainRouteId)> ResolveServiceRouteStationMainRoutes(ServiceRoute sr, IReadOnlyList<MainRoute> allMainRoutes)`

resolveServiceRouteStationOrderと同じ駅列に加え、各駅がどのSegment（MainRoute）由来かを返す。
StopPatternResolverが駅表示名の解決にMainRoute.StationDisplayNameOverridesを
適用する際に使用する。境界駅（前Segmentの終端と重複する駅）は、重複除去の結果として
前Segment側のMainRouteIdが採用される。

#### 6.2.4 Stations

#### `DiaEditCore.Algorithm.Stations.FloorUnitSummary` (record)

FloorUnit詳細編集画面（UI設計書§4.2.2改訂）の右ペイン概要表示用。
保存はしない都度算出の派生値（Pattern C：ProjectSessionのdirty管理下に置かない）。
「番線数」＝旅客案内上の番線ではなく、在線・占有チェック対象（RailRole.Track）の本数
（2026-09セッションでTao様と合意した定義。Platform件数とは意図的に一致させていない）。

| Field | Type |
|---|---|
| TrackRailCount | `int` |
| ShuntingRailCount | `int` |
| TotalRailCount | `int` |
| StationPathCount | `int` |
| HasUnreferencedTrackEndpoints | `bool` |

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitSummaryResolver` (class)

---

##### `public static FloorUnitSummary Build(FloorUnitId floorUnitId, IReadOnlyList<Rail> allRails, IReadOnlyList<StationPath> allStationPaths, IReadOnlyList<NoneEndpoint> noneEndpoints, IReadOnlyList<BoundaryPoint> boundaryPoints, IReadOnlyList<EntryPoint> entryPoints, IReadOnlyList<BufferStop> bufferStops, IReadOnlyList<Switcher> switchers)`

---

#### `DiaEditCore.Algorithm.Stations.RailFloorUnitLookup` (class)

RailはFloorUnitIdを直接持たない（§4.4.3、EndpointA/Bの接続先端点オブジェクト経由で導出される
派生関係）ため、「指定FloorUnitに属するRail」を都度算出する共有ロジック。
元はFloorUnitDetailViewModel.ResolveFloorUnitId／RailsBelongingToThisFloorUnitとして
ViewModel内に閉じていたが、FloorUnitSummaryResolverからも必要になったため
Core層へ切り出した（単一の情報源原則）。専用逆引きIndexは新設しない
（DeleteRailCommandと同じ判断基準：消費者が少数・件数規模も小さいため線形走査で足りる）。

---

##### `public static FloorUnitId? ResolveFloorUnitId(RailEndpointRef endpoint, IReadOnlyList<NoneEndpoint> noneEndpoints, IReadOnlyList<BoundaryPoint> boundaryPoints, IReadOnlyList<EntryPoint> entryPoints, IReadOnlyList<BufferStop> bufferStops, IReadOnlyList<Switcher> switchers)`

---

##### `public static IEnumerable<Rail> RailsBelongingTo(FloorUnitId floorUnitId, IReadOnlyList<Rail> allRails, IReadOnlyList<NoneEndpoint> noneEndpoints, IReadOnlyList<BoundaryPoint> boundaryPoints, IReadOnlyList<EntryPoint> entryPoints, IReadOnlyList<BufferStop> bufferStops, IReadOnlyList<Switcher> switchers)`

#### 6.2.5 Stations/FloorUnitObjects

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.BoundaryTerminal` (record struct)

| Field | Type |
|---|---|
| Id | `ObjectId` |

---

##### `public BoundaryTerminal(BoundaryPointId id)`

---

##### `public BoundaryTerminal(int value)`

---

##### `public BoundaryTerminal(string kind, int value)`

---

##### `public static BoundaryTerminal Of(StationPathWaypoint wp)`

---

##### `public static BoundaryTerminal FromEntryPoint(EntryPointId id)`

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.ConvergenceClassification` (record struct)

の戻り値。分類結果と、Errorケースのみ設定されるメッセージを保持する。

| Field | Type |
|---|---|
| Kind | `ConvergenceKind` |
| ErrorMessage | `string?` |

---

##### `public static ConvergenceClassification Ok(ConvergenceKind kind)`

Error以外の分類結果を生成する。

**Parameters**

- `kind`: 分類結果の種別。

**Returns**
を保持し、がnullの値。

---

##### `public static ConvergenceClassification Fail(string message)`

Errorケースの分類結果を生成する。

**Parameters**

- `message`: エラー内容を説明するメッセージ。

**Returns**
とを保持する値。

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.ConvergenceKind` (enum)

Rail端点収束変換ルールに基づく分類結果の種別。

> Vanish：N=0。削除されたRail自身のみがこの端点を参照していた（孤立・行き止まり）。端点オブジェクト自体を削除してよい（再分類の余地がない）。
> Keep：N=1、既存フロー据置（ユーザーが種別を明示選択）。他に参照Railが残っているため端点オブジェクトは維持する。
> BoundaryPoint：N=2。
> SwitcherNew：N=3,4かつ収束集合中に既存Switcher参照が無い（新規Switcher作成）。
> SwitcherExpand：N=3,4かつ収束集合中に既存Switcher参照が含まれる（既存Switcher拡張）。
> Error：N&gt;=5。

| Value | 説明 |
|---|---|
| Vanish |  |
| Keep |  |
| BoundaryPoint |  |
| SwitcherNew |  |
| SwitcherExpand |  |
| Error |  |

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.MergeNameConflict` (record)

| Field | Type |
|---|---|
| NameA | `string` |
| NameB | `string` |

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.MergeNameResolution` (record)

name統合の判定結果。両方非空かつ異なる場合はユーザー選択が必要（呼び出し側UI層の責務）。

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.MergeNameResolved` (record)

| Field | Type |
|---|---|
| Name | `string` |

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.RailEnd` (enum)

| Value | 説明 |
|---|---|
| A |  |
| B |  |

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.RailEndpointConvergenceResolver` (class)

「ある座標にどのRail端点が収束しているか」を検出し、収束数から変換種別を分類する純粋関数群。

> 対象がドラッグで新規に動いてきた端点か既存の確定済み端点かは区別しない（座標一致のみで判定）。
> Switcherは複数PortIndexが同一オブジェクト（＝同一Base.Position）に属するため、
> 「異なるPortIndexのSwitcherEndpointRef」も同一座標として自然に収束集合へ含まれる。

---

##### `public static Point ResolvePosition(RailEndpointRef endpointRef, IReadOnlyList<NoneEndpoint> noneEndpoints, IReadOnlyList<BoundaryPoint> boundaryPoints, IReadOnlyList<EntryPoint> entryPoints, IReadOnlyList<BufferStop> bufferStops, IReadOnlyList<Switcher> switchers)`

RailEndpointRefが指す実体オブジェクトのBase.Positionを解決する。

**Parameters**

- `endpointRef`: 解決対象のRail端点参照。
- `noneEndpoints`: 現在のNoneEndpointコレクション。
- `boundaryPoints`: 現在のBoundaryPointコレクション。
- `entryPoints`: 現在のEntryPointコレクション。
- `bufferStops`: 現在のBufferStopコレクション。
- `switchers`: 現在のSwitcherコレクション。

**Returns**
参照先オブジェクトの座標。

---

##### `public static IReadOnlyList<RailEndpointLocation> FindConvergingEndpoints(Point position, IReadOnlyList<Rail> rails, IReadOnlyList<NoneEndpoint> noneEndpoints, IReadOnlyList<BoundaryPoint> boundaryPoints, IReadOnlyList<EntryPoint> entryPoints, IReadOnlyList<BufferStop> bufferStops, IReadOnlyList<Switcher> switchers)`

指定座標に収束している(RailId, RailEnd, RailEndpointRef)の組を、全Railを走査して列挙する。

**Parameters**

- `position`: 収束を検出する座標。
- `rails`: 走査対象の全Rail。
- `noneEndpoints`: 現在のNoneEndpointコレクション。
- `boundaryPoints`: 現在のBoundaryPointコレクション。
- `entryPoints`: 現在のEntryPointコレクション。
- `bufferStops`: 現在のBufferStopコレクション。
- `switchers`: 現在のSwitcherコレクション。

**Returns**
に収束しているRail端点の一覧。0件の場合は空リスト。

**Remarks**
同一Railの両端が同一座標に収束するケース（自己ループ）は現行モデルでは考慮不要
（Rail作成・移動のいずれの経路でも両端を同一点にする操作はUI上想定されていないため、
検出はするが特別扱いはしない＝両端とも収束集合に普通に加わる）。

---

##### `public static ConvergenceClassification Classify(IReadOnlyList<RailEndpointLocation> converging)`

収束集合から変換種別を判定する。

**Parameters**

- `converging`: が返す収束集合。

**Returns**
収束数に応じた。N=3,4の場合、収束集合中に既存
が1件でも含まれていれば、
含まれていなければを返す。N&gt;=5は
によるエラーを返す（例外は投げない）。

**Remarks**
N=0（削除方向で呼ばれ、当該座標を参照するRailがもう1本もない場合）を独立ケースとして扱う点が
v13.13時点との差分：Rail削除に伴う「再分類」呼び出しで初めて起こりうるケースであり、
Rail新規作成・ドラッグ方向の呼び出しでは通常発生しない（自分自身は集合に含まれるため）。
前提：同一Switcherの複数ポートが収束集合に混在していても、Idが同じなので拡張と判定して問題ない。

---

##### `public static IReadOnlyList<(RailEndpointLocation Location, int PortIndex)> AssignSwitcherPorts(IReadOnlyList<RailEndpointLocation> converging)`

新規Switcher作成時のPort機械採番（暫定案、v13.13：Rail.Id昇順で0〜N-1）。

**Parameters**

- `converging`: Port割当対象の収束集合。呼び出し前提：が
を返したケースでのみ使う
（は既存PortCountへの追加のため別ロジックが必要、本メソッドの対象外）。

**Returns**
各に対応するPortIndexの割当。同一Rail.Id内の順序に依存しない
一意な並びを保証するため、Rail.Id→(必要ならEnd)で安定ソートした結果を0始まりで返す。

---

##### `public static IReadOnlyList<StationPathId> FindBlockingStationPaths(IReadOnlyCollection<ObjectId> disappearingIds, IReadOnlyList<StationPath> stationPaths)`

収束変換により消滅する既存EntryPoint/BoundaryPoint/BufferStop/Switcherを
StationPath.Waypointsが参照していないか確認する。

**Parameters**

- `disappearingIds`: 収束によって消滅する側（＝収束集合中、これから作成するBoundaryPoint/Switcher以外の既存
BoundaryPoint/EntryPoint/BufferStop/SwitcherのObjectId）。
既存Switcherが「拡張」される場合（）はSwitcher自体は
消滅しない点に注意（呼び出し側でdisappearingIdsに含めないこと）。
- `stationPaths`: 検証対象の全StationPath。

**Returns**
のいずれかをWaypointsに含むStationPathのId一覧。
該当が無ければ空リスト。

**Remarks**
DependencyResolver.ResolveDirectDependentsには本チェックに相当するルールが存在しない
（BoundaryPointObjectId／EntryPointObjectId／BufferStopObjectId／SwitcherObjectIdは
いずれも意図的な終端[]。StationWork CRUD横展開と一体実装の方針でスコープ外とされている）。
そのためDeleteRailCommandがPlatform/TemporaryRestriction/Trainに対して行っているのと同じ
「専用Indexを作らずStationPathsを直接線形走査する」パターンを踏襲する。
NoneEndpointは対象外：StationPathSuggesterの前提（Rail端点種別が全て確定済みであること）により、
NoneEndpointがWaypointsへ現れることは構造的にない。

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.RailEndpointLocation` (record struct)

収束点に集まっているRail端点1件を表す値。

| Field | Type | 説明 |
|---|---|---|
| RailId | `RailId` | 収束点に端点を持つRailのId。 |
| End | `RailEnd` | そのRailのうち、収束点に位置する側の端（A/B）。 |
| Ref | `RailEndpointRef` | 収束点における実際のRail端点参照。 |

> はRailMerger.RailEnd（A/B）を再利用する（新規enumを増やさない。）

---

##### `public RailEndpointLocation(RailId RailId, RailEnd End, RailEndpointRef Ref)`

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.RailMerger` (class)

「N=2の自動Rail統合」の純粋関数実装。2本のRailのうち、収束点側の2端点は破棄し、
反対側の2端点をそのまま新Railの両端に据える（PortIndex等を含むRailEndpointRefを一切改変しない）。
収束点自体はもはやSwitcherを形成しないため、収束点側の端点情報は意味を失う。

---

##### `public static MergeNameResolution ResolveName(string nameA, string nameB)`

nameの統合方針のみを判定する（副作用なし）。空文字列でない方を優先し、
両方非空かつ異なる場合は呼び出し側（UI層）にユーザー選択を促す。

---

##### `public static Rail MergeAtConvergence(Rail railA, RailEnd convergingSideA, Rail railB, RailEnd convergingSideB, RailId newId, string resolvedName)`

railA・railBを、それぞれの収束側端点（convergingSideA/B）を破棄する形で1本のRailへ統合する。
呼び出し前提：railA.Role == railB.Role == RailRole.Normal
nameが両方非空かつ異なる場合は resolvedName に確定済みの文字列を明示的に渡すこと
（ResolveNameがMergeNameConflictを返したケースをユーザー選択で解決した後の値）。

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.RailSequenceResolver` (class)

StationPathのPathWayPointから通過Rail列を引き当てる。

---

##### `public RailSequenceResolver(IReadOnlyList<Rail> rails)`

---

##### `public IReadOnlyList<RailId> Resolve(StationPath path)`

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.StationPathSuggester` (class)

駅単位で構内探索を行い、StationPath の候補 waypoint 列を返す。
保存は行わず、UI に一時提示するだけ。

---

##### `public StationPathSuggester(StationId stationId, IReadOnlyDictionary<RailId, Rail> rails, IReadOnlyDictionary<BoundaryPointId, BoundaryPoint> bps, IReadOnlyDictionary<EntryPointId, EntryPoint> eps, IReadOnlyDictionary<BufferStopId, BufferStop> bufferStops, IReadOnlyDictionary<SwitcherId, Switcher> switchers, IReadOnlyDictionary<FloorUnitId, StationId> floorUnitToStation)`

---

##### `public IReadOnlyList<StationPathWaypoint[]> Suggest(RailEndpointRef start)`

入力：BoundaryPointEndpointRef または EntryPointEndpointRef
出力：候補 StationPathWaypoint 配列のリスト

---

#### `DiaEditCore.Algorithm.Stations.FloorUnitObjects.StationPathTrackIndexBuilder` (class)

---

##### `public static (

        Dictionary<(EntryPointId, RailId), StationPathId> ArrivalIndex,

        Dictionary<(RailId, EntryPointId), StationPathId> DepartureIndex

    ) Build(IReadOnlyList<StationPath> allPaths, IReadOnlyList<Rail> allRails)`

---

##### `public static (

        Dictionary<(BoundaryTerminal Terminal, RailId TrackRailId), StationPathId> ArrivalIndex,

        Dictionary<(RailId TrackRailId, BoundaryTerminal Terminal), StationPathId> DepartureIndex

    ) BuildWithBoundaryTerminals(IReadOnlyList<StationPath> allPaths, IReadOnlyList<Rail> allRails)`

#### 6.2.6 TimeTable/Trains

#### `DiaEditCore.Algorithm.TimeTable.Trains.AnchorMode` (enum)

| Value | 説明 |
|---|---|
| Auto |  |
| Manual |  |
| Disabled |  |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.CarConsistResolver` (class)

---

##### `public static ResolvedConsist ResolveConsistAt(Train train, StopKey stopKey, ConsistResolutionContext context)`

train自身のWorks列をStartOp（またはPrevTrain.SplitOrigin）から対象stopKeyまで時系列順にたどり、実編成を復元する。
起点が見つからない場合、または対象stopKeyが起点より先行する場合は空を返す。

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.ConnectionCandidate` (record)

| Field | Type |
|---|---|
| TrainId | `TrainId` |
| DepartureSeconds | `int` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.ConsistResolutionContext` (record)

StartOp.startOpConsist、またはPrevTrain.SplitOrigin経由で他Trainのconsistsequenceを
起点として、以降のCoupling/Decouplingイベントを時系列順にたどることで任意時点の実編成を復元する。
都度導出・非保存。
DecouplingWork.IsRearBaseを直読みするだけで「自Trainがfront/rearどちらを引き継いだか」が一意に決まるため、全Train横断の事前計算は不要
（train.StopTimes走査中にDecoupling作業へ遭遇した時点で、trainは常にそのDecouplingの継続側＝originであることが構造的に保証される。
子Train側はPrevTrain.SplitOrigin経由の別メソッド ResolveSplitOriginConsist でのみ解決されるため混線しない）。
Couplingは相手Train（CouplingWork.PartnerTrainId）を再帰的にResolveConsistAtで解決する。

| Field | Type |
|---|---|
| CarConsists | `IReadOnlyDictionary<CarConsistId, CarConsist>` |
| CarCompositions | `IReadOnlyDictionary<CarCompositionId, CarComposition>` |
| AllTrainsById | `IReadOnlyDictionary<TrainId, Train>` |

---

##### `public static ConsistResolutionContext Empty(IReadOnlyDictionary<CarConsistId, CarConsist> carConsists, IReadOnlyDictionary<CarCompositionId, CarComposition> carCompositions)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.EffectiveLengthChecker` (class)

---

##### `public static LengthCheckResult CheckEffectiveLength(Train train, StopKey stopKey, IReadOnlyDictionary<RailId, Rail> rails, IReadOnlyDictionary<PlatformId, Platform> platforms, IReadOnlyDictionary<StopKey, StopTime> stopTimes, IReadOnlyDictionary<CarId, Car> cars, ConsistResolutionContext consistContext)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.HopResolution` (record)

ホップ（隣接駅1区間）ごとの解決結果。3種のケースを判別共用体で表現する。

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.HopResolved` (record)

| Field | Type |
|---|---|
| Segment | `TrainRunSegment` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.HopRunTimeOk` (record)

基準実績が見つかり、区間所要時分（アンカー調整適用後、モードに応じた値）が確定した。

| Field | Type |
|---|---|
| Seconds | `int` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.HopRunTimeResult` (record)

ホップ単位の所要時分判定結果。EffectiveLengthCheckerの
LengthCheckOk／LengthCheckNotApplicable／LengthCheckOverflowと同型の判別共用体パターンを踏襲する。

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.HopRunTimeUndefined` (record)

基準Train実績が見つからない（BaseRunTimeIndexBuilder.SelectionKeyに一致するTrainが
BaseTimeTableSet内に存在しない、またはDiagramRevision.BaseTimeTableSetId自体が未設定）。
UI上はOuDiaSecond互換の薄黄背景で表現する想定。

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.HopTransferBlocked` (record)

| Field | Type |
|---|---|
| FromStationId | `StationId` |
| ToStationId | `StationId` |
| AttemptedScId | `StationConnectionId` |
| PreviousScId | `StationConnectionId` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.HopUnresolved` (record)

| Field | Type |
|---|---|
| FromStationId | `StationId` |
| ToStationId | `StationId` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.LengthCheckNotApplicable` (record)

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.LengthCheckOk` (record)

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.LengthCheckOverflow` (record)

| Field | Type |
|---|---|
| OverflowMeters | `double` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.LengthCheckResult` (record)

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.ProposedAdjustment` (record)

Manualモードで提案される、特定区間への差分適用案。

| Field | Type |
|---|---|
| SegmentIndex | `int` |
| OriginalSeconds | `int` |
| AdjustedSeconds | `int` |
| Anchor | `RunTimeAnchor` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.ResolvedConsist` (record)

| Field | Type |
|---|---|
| ConsistBlocks | `IReadOnlyList<CarCompositionId>` |
| Cars | `IReadOnlyList<CarRef>` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.RunTimeAnchor` (record)

所要時分計算のアンカー（実測値が既知の地点）。
StationIndexはstationPaths配列内のindexを指す。
駅0（出発駅）はIsArrival=trueを持てない。最終駅（stationPaths[^1]）はIsArrival=falseを持てない。

| Field | Type |
|---|---|
| StationIndex | `int` |
| IsArrival | `bool` |
| ActualElapsedSec | `int` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.RunTimeCalculationResult` (record)

| Field | Type |
|---|---|
| Hops | `IReadOnlyList<HopRunTimeResult>` |
| ProposedAdjustments | `IReadOnlyList<ProposedAdjustment>` |
| Warnings | `IReadOnlyList<RunTimeWarning>` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.RunTimeCalculator` (class)

区間所要時分算出：DiagramRevision.BaseTimeTableSetIdが指すTimeTableSet内のTrain実績から都度導出する。
baselineIndexの構築はBaseRunTimeIndexBuilder(Algorithm/CacheBuilder）の責務とし、
本クラスは「確定済みindexを引いてアンカー調整を適用する」計算処理に専念する（責務分離）。
基準実績が見つからないホップ（Undefined）は、アンカー調整の対象から除外する：
- baseline未確定のためAuto/Manualの差分計算そのものが定義できない
- そのホップを含む区間へのアンカー到達判定（SumRange）は、Undefinedホップを跨ぐ場合
「その区間全体もUndefined」として扱い、アンカーによる自動調整・警告の対象から除外する
（区間ごとに個別判定する方針。「区間ごとに他区間はOkのまま」方針に従い、Undefinedの伝播は当該アンカー区間内に限定する）

---

##### `public static RunTimeCalculationResult Calculate(IReadOnlyList<StationPath> stationPaths, IReadOnlyList<RunTimeHopInput> hops, IReadOnlyDictionary<BaseRunTimeIndexBuilder.SelectionKey, int> baseRunTimeIndex, Model.VehicleTypeId? vehicleTypeId, IReadOnlyList<RunTimeAnchor> anchors, AnchorMode mode)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.RunTimeHopInput` (record)

あるホップ（隣接駅1区間）の基準実績照合に必要な入力。
StationConnectionSegmentIdへの解決は呼び出し側（Train編集コマンド等）の責務とする
（BaseRunTimeIndexBuilderのコメント参照：TrainRunSegment.StationConnectionIdからの解決は
StationConnection.SegmentsとallSegmentsの突き合わせを要するため、Calculate自体はSCSId確定後の
単純な辞書引きに専念させ、責務を分離する）。

| Field | Type |
|---|---|
| SegmentId | `StationConnectionSegmentId` |
| FromIsStop | `bool` |
| ToIsStop | `bool` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.RunTimeWarning` (record)

Disabledモードで検出される警告：実測経過秒数がderived（baseline累積）を下回っている。

| Field | Type |
|---|---|
| Anchor | `RunTimeAnchor` |
| DerivedElapsedSec | `int` |
| ActualElapsedSec | `int` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.ServiceRouteToRunSegmentsResolver` (class)

ServiceRoute＋方向（上り/下り）から、ホップ単位でStationConnectionを確定させた
TrainRunSegment列を導出する。新規Train追加（4.9.2節 経路③）専用。都度導出・非保存。
v12.29 SCS direction-agnostic renameセッションでの変更点：
- ResolveHopCandidatesを、StationConnectionSegment.StationIdA/StationIdBに対する
無向マッチングへ変更した。旧実装は「SegmentのFrom/Toは走行方向と厳密に一致し、
方向ごとに別Segmentエンティティを用意する」前提だったため、双単線区間で同一SCSを
上り方向SCと下り方向SCの双方が参照するケースを取りこぼしていた
（fromStationId/toStationIdを逆に指定すると0件になっていた）。今回の変更により、
このケースが正しく候補として拾われるようになる（本セッションの主目的）。
- CanTransferAtはEntryPointSequenceResolver.Resolve（系統(ii)）の新シグネチャ
（allMainRoutes追加）に追従した。

---

##### `public static IReadOnlyList<HopResolution> Resolve(ServiceRoute sr, bool isUpDirection, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<StationPath> allStationPaths, IReadOnlyList<Rail> allRails, HopCandidateSelector selector)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.StopPatternElement` (record)

| Field | Type |
|---|---|
| StationId | `StationId` |
| StationDisplayName | `DisplayName` |
| Mark | `StopPatternMark` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.StopPatternMark` (enum)

| Value | 説明 |
|---|---|
| Stop |  |
| Pass |  |
| OutOfRange |  |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.StopPatternResolver` (class)

resolveServiceRouteStationOrderの結果と、対象TrainのstopTimes・TrainRunSegmentを
突き合わせ、駅名付きの停車パターン列（基準列車選択UI用）を導出する。都度導出・非保存。

---

##### `public static IReadOnlyList<StopPatternElement> ResolveStopPattern(Train train, IReadOnlyList<ServiceRoute> allServiceRoutes, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<Station> allStations)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.TrainConnectionResolver` (class)

駅の同一番線を使用する終着・始発列車を時刻順に走査して
PrevTrain/NextTrainを導出する。都度導出・非保存（Train自身はPrevTrainId/NextTrainId
を保持しない）。OpNumberが未設定のTrainに対しても、折り返し・接続候補をUI表示できるようにする。
パフォーマンス：TimeTableSetCache.DepartureByStationTrackIndex（駅×番線→発車時刻昇順のTrain列）
をBuildDepartureIndexで構築し、ResolveNextTrainCandidatesはこのインデックスを引き当てるだけで
候補を求める（全Train走査のO(N)ではなく、該当駅・番線分のみのO(K)）。
一意マッチングについて：
ResolveNextTrainCandidates／ResolveNextTrainは、到着列車ごとに独立して「最短接続となる
出発列車」を選ぶAPIのため、複数の到着列車が同じ出発列車を候補として選びうる（非単射）。
これはUI上の接続候補表示（ユーザーに選択肢を見せる用途）としては正しい挙動だが、
TrackOccupancyProvider・TrainOperationChainResolverのように
「物理的に一意な折り返しペア」を前提とする用途では非単射なマッチングをそのまま使うと
誤ったTrack占有の重複や運用チェーンの上書きを引き起こす。
そのため、全体で整合する一意なマッチングをResolveUniqueNextTrainMap／
ResolveUniquePrevTrainMapとして別途提供する。マッチング規則：各出発列車(departure)につき、
それを候補とする到着列車のうち到着時刻が最も遅い（＝乗継時間が最短の）到着列車のみを
唯一のPrevTrainとして確定する。敗れた到着列車は、その出発列車をNextTrainとして採用しない
（＝独立した列車として扱われる。他に候補がなければ接続なしとなる）。

---

##### `public static IReadOnlyList<ConnectionCandidate> ResolveNextTrainCandidates(Train arrivingTrain, IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex, ProjectSettings settings)`

到着列車(arrivingTrain)を起点に、接続候補となる出発列車を発車時刻の昇順で返す。
UI上の接続候補表示用途（複数候補をユーザーに提示する）。一意性は保証しない
（非単射になりうる点はクラスコメント参照）。
絞り込み条件：
1. arrivingTrainの終着駅（RunSegments末尾のToStationId）と、候補の始発駅が一致すること
（departureIndexのキーに含めることで担保）
2. 両者のTrackRailIdが一致すること（同一番線。departureIndexのキーに含めることで担保）
3. 発車時刻 - 到着時刻 が ProjectSettings.ValidationRules.MinTurnaroundSec 以上であること
（MinTurnaroundSecがnullの場合は下限チェックを行わず、0以上であれば候補とする）

---

##### `public static TrainId? ResolveNextTrain(Train arrivingTrain, IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex, ProjectSettings settings)`

ResolveNextTrainCandidatesの先頭（最も早く発車する＝最短接続となる候補）を採用する、
単一列車視点の簡易API。
注意：このAPIは非単射でありうる（クラスコメント参照）。TrackOccupancyProvider・
TrainOperationChainResolverのように物理的に一意な折り返しペアを前提とする用途では、
必ずResolveUniqueNextTrainMap／ResolveUniquePrevTrainMapを使うこと。
このAPIは主にUI上の単純表示（「とりあえず1つ候補を見せる」用途）に留める。

---

##### `public static Dictionary<TrainId, TrainId> ResolveUniqueNextTrainMap(IReadOnlyList<Train> allTrains, IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex, ProjectSettings settings)`

全体で整合する一意なNextTrainマッチングを構築する（到着列車→出発列車）。
アルゴリズム：
1. 全Trainについて、ResolveNextTrainCandidatesで候補列を求める
2. 各出発列車(departure)ごとに、それを候補とする到着列車の中から
到着時刻が最も遅い（＝乗継時間が最短の）到着列車を1つだけ選ぶ
（同着の場合はTrainIdの値で決定的にタイブレークする）
3. 選ばれなかった到着列車は、その出発列車をNextTrainとしない
（他に候補があれば次点、なければ接続なし＝nullとなる。今回は
「複数出発列車を跨いだ再割当」は行わず、単純に「その出発列車は使えない」
として扱う。1出発列車=最大1到着列車という制約のみを保証する）
停止性：全Train・全候補に対する有限回の走査のみで、再帰・連鎖探索を行わないため必ず停止する。
一意性：departureごとに高々1つのarrivingTrainのみがマッチするため、本メソッドが返す
マップは常に単射（NextTrainマップの値に重複がない）であることが保証される。

---

##### `public static Dictionary<TrainId, TrainId> ResolveUniquePrevTrainMap(IReadOnlyList<Train> allTrains, IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex, ProjectSettings settings)`

ResolveUniqueNextTrainMapを反転させたPrevTrainマップ（出発列車→到着列車）を構築する。
ResolveUniqueNextTrainMap自体がdepartureごとに高々1つのarrivingTrainしか持たないことを
保証しているため、反転操作は単純なDictionary変換で安全に行える（キー重複は発生しない）。

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.TrainOperationChainResolver` (class)

CarCompositionがどのTrainOperationに属すかを、StartOpCarSlot.OperationId
（ResolvedOperationRef）を起点とし、PrevTrainOperationOverrideによる明示的な変更を
反映しながらNextTrainチェーンをたどって導出する。都度導出・非保存。
vNEXT改訂：以下2つの欠落を解消した。
(A) Decouplingで離脱した子TrainはStartOpを持たないためチェーン起点に一切登場せず、
result辞書に登録されなかった。→ TryFollowDecouplingでSplitOriginRef経由の子Trainへ
チェーンを付け替える処理を追加。
(B) Couplingで自Trainが相手（Host）Trainへ合流した場合、旧実装はnextTrainMapのみに
依存していたため合流先を認識できず、そこでチェーンが打ち切られていた。
→ TryFollowCouplingでHost Train側へチェーンを付け替える処理を追加
（OperationIdはCarComposition自身の属性であり合流によって変化しないため据え置き）。
判定順序：毎周、Decoupling判定→Coupling判定→通常のnextTrainMapの順に確認する
（同一Trainが同一駅でDecoupling/Coupling双方に関与するケースを想定。visitedはcurrent.Id
ベースのため、host側が別チェーンで訪問済みなら合流時に正しく打ち切られる）。

---

##### `public static Dictionary<(TrainId TrainId, CarCompositionId CarCompositionId), TrainOperationId> Resolve(IReadOnlyList<Train> allTrains, IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex, IReadOnlyList<TrainOperation> trainOperations, ProjectSettings settings)`

#### 6.2.7 TimeTable/Trains/Conflicts

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.ConflictChecker` (class)

交差支障検知：あるオブジェクトを列車が占有しているとき、別の列車が同じオブジェクトを同時に占有していないかを検証する汎用チェッカー。
「対象オブジェクトID」と「占有時間帯」のみをキーとし、Track・StationPath・StationConnectionSegmentの3用途を同一の仕組みで扱う。

| Field | Type |
|---|---|
| TargetObjectId | `ObjectId` |

---

##### `public ConflictChecker(ObjectId targetObjectId, IReadOnlyList<Occupancy> occupancyRanges)`

---

##### `public IReadOnlyList<(TrainId A, TrainId B)> CheckOverlap()`

同一targetObjectId内の時間帯重複を検出する（スイープライン方式）。
1. occupancyRangesをstartSeconds昇順にソート                         … O(n log n)
2. endSecondsを比較キーとする最小ヒープ（アクティブ区間集合）を用意
3. ソート順に各区間を走査：
a. ヒープ先頭のendSecondsが現区間のstartSeconds以下なら順次pop      … 全体でO(n log n)
b. ヒープに残っている区間は全て現区間と重複しているため、重複ペアとして記録  … O(k)
c. 現区間をヒープにpush                                            … O(log n)
計算量：合計O((n + k) log n)。
停止性：ソート・ヒープ操作とも有限要素の1回走査のみで完結し必ず停止する。
一意性：開始時刻が同値の場合の走査順によって戻り値配列内の表示順は変わりうるが、検出される重複ペアの集合自体は入力順序に依存せず一意。
返り値：支障列車のTrainIdリスト

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.EntryPointSequenceCache` (class)

StationConnectionIdごとのEntryPointSequence解決結果をメモ化するだけの小さなキャッシュ。
v12.29対応：EntryPointSequenceResolver.Resolveの系統(ii)化に伴いallMainRoutesが必須引数になった。

---

##### `public static Func<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>> Build(IReadOnlyList<StationConnection> stationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<MainRoute> allMainRoutes)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.Occupancy` (record)

| Field | Type |
|---|---|
| TrainId | `TrainId` |
| StartSeconds | `int` |
| EndSeconds | `int` |

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.StationConnectionSegmentOccupancyProvider` (class)

StationConnectionSegment用途のConflictChecker。
v12.29対応：EntryPointSequenceCache.Buildの系統(ii)化に伴い、allMainRoutesを新規に受け取る。
（クラス冒頭の詳細コメントは既存版から変更なし）
v12.29追加修正：ホップ→SCS解決を「StationConnection.Segments[0]固定」から、
そのホップの実際の発着駅（TrainRunSegment.FromStationId/ToStationId）と一致するSCSを
EntryPointSequenceResolver.ResolveOriented（系統(i)、無向マッチング）で特定する方式へ変更した。
旧実装は「1RunSegment=1SCS」を暗黙に仮定しており、複数ホップを1本のStationConnectionが
カバーする広域SC（本セッションでServiceRouteToRunSegmentsResolverが正式にサポートした構成。
例：A→B→CをカバーするSCが、A→BとB→Cの両方のTrainRunSegmentから同一StationConnectionIdとして
参照される）に対して、常にSegments[0]（＝A→B用のSCS）を誤って採用してしまい、
B→C区間の占有がA→B側のSCSに誤計上される・B→C側のSCSには一切計上されない、という
サイレントな不具合があった。

---

##### `public static Dictionary<StationConnectionSegmentId, List<ConflictChecker.Occupancy>> BuildOccupancy(IReadOnlyList<Train> trains, IReadOnlyList<StationConnection> stationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyDictionary<StationPathId, StationPath> pathsById, IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex, IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.StationPathConflictObjectResolver` (class)

「統一グルーピング方式」：StationPathが占有する対象オブジェクトID群を求め、
全StationPathを対象オブジェクトIDでグルーピングする（同じ対象オブジェクトIDを含む
StationPath同士が、ConflictCheckerの1インスタンスに対応する）。
対象オブジェクトID群 = resolveRailSequence(sp) ∪ waypoints中のSwitcherId ∪ manualConflictObjectIds
Switcherの判定について：Switcher自体が「常に単一の物理的収束点を表す」よう設計されているため、
1つのSwitcherを共有するStationPathは、使用した経路の組み合わせによらず常に競合する。
waypoints中のSwitcherWaypointをそのままグルーピング対象に含めるだけで、特別な判定ロジックは不要。

---

##### `public static IReadOnlyList<ObjectId> Resolve(StationPath sp, RailSequenceResolver railSequenceResolver)`

---

##### `public static Dictionary<ObjectId, List<StationPathId>> GroupAll(IReadOnlyList<StationPath> allPaths, RailSequenceResolver railSequenceResolver)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.StationPathOccupancyProvider` (class)

全Trainを走査し、StationPathごとの占有区間（ConflictChecker.Occupancy）を構築する。
v12.29対応：EntryPointSequenceResolver.Resolveが系統(ii)化（allMainRoutes必須）されたため、
本ProviderもallMainRoutesを新規に受け取り、EntryPointSequenceCache.Buildへ渡す。

---

##### `public static Dictionary<StationPathId, List<ConflictChecker.Occupancy>> BuildOccupancy(IReadOnlyList<Train> trains, IReadOnlyList<StationConnection> stationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyDictionary<StationPathId, StationPath> pathsById, IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex, IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.StopVisitOccupancyResolver` (class)

1回の駅訪問(visitSeq)について、到着StationPath/出発StationPathそれぞれの占有区間
(StationPathId・開始秒・終了秒)を導出する共通ロジック。
StationPathOccupancyProvider・TrackOccupancyProviderの両方から同一の算出式を共有するために切り出した。
基準時刻の統一：停車時はarrivalSeconds/departureSeconds、通過時はdepartureSecondsを通過時刻として流用する。いずれも未設定(-1)なら対象外。
v12.29追加修正：arrivalEp/departureEpの取得を、resolveEpが返す配列の位置決め打ち
（[0]・[^1]）から、そのホップ自身の発着駅（TrainRunSegment.FromStationId/ToStationId）と
一致する要素を検索する方式へ変更した。広域SC（複数ホップを1つのStationConnectionが
カバーする構成。ServiceRouteToRunSegmentsResolverが正式サポート）では、resolveEpが
2件以上の要素を返すため、位置決め打ちだと中間駅で隣接ホップのEntryPointを誤って
取得してしまうバグがあった（例：A→B→CをカバーするSCで、B駅到着時に本来必要な
A→B側の到着EPではなく、B→C側の到着EP（C駅側）を誤取得していた）。
FirstOrDefaultが一致要素を見つけられない場合（データ不整合等）はnullを返し、
占有情報なしとして扱う（例外は投げない）。

---

##### `public static VisitOccupancy? Resolve(Train train, int visitSeq, Func<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>> resolveEp, IReadOnlyDictionary<StationPathId, StationPath> pathsById, IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex, IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex)`

train.RunSegmentsに対するvisitSeq(0..segs.Count)を1つ受け取り、その訪問のStationPath占有情報を返す。
対象のStopTimeが存在しない、またはTrackRailIdが未設定の場合はnullを返す。

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.TrackOccupancyProvider` (class)

Track（番線）用途のConflictChecker（6.5節）。
v12.29対応：EntryPointSequenceCache.Buildの系統(ii)化に伴い、allMainRoutesを新規に受け取る。
（クラス冒頭の詳細コメントは既存版から変更なし。シグネチャ・呼び出し箇所のみ更新）

---

##### `public static Dictionary<RailId, List<ConflictChecker.Occupancy>> BuildOccupancy(IReadOnlyList<Train> trains, IReadOnlyList<StationConnection> stationConnections, IReadOnlyList<StationConnectionSegment> allSegments, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyDictionary<StationPathId, StationPath> pathsById, IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex, IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex, IReadOnlyList<Rail> rails, ProjectSettings projectSettings)`

---

#### `DiaEditCore.Algorithm.TimeTable.Trains.Conflicts.VisitOccupancy` (record struct)

| Field | Type |
|---|---|
| TrackRailId | `RailId` |
| ArrivalEp | `EntryPointId?` |
| ArrivalSpId | `StationPathId?` |
| ArrivalStart | `int?` |
| ArrivalEnd | `int?` |
| DepartureEp | `EntryPointId?` |
| DepartureSpId | `StationPathId?` |
| DepartureStart | `int?` |
| DepartureEnd | `int?` |

### 6.3 Session

#### `DiaEditCore.Session.IdAllocator` (class)

モデル種別ごとの単調カウンタ方式Id採番器（§9.2項目27）。
Undo・削除の有無に関わらず、一度発行したIdは同一セッション内で二度と発行しない。
これにより、Undo後の再作成で異なるインスタンスが同じIdを持ち参照が衝突するリスクを排除する。
ProjectSession.Load()時にモデル種別ごとに1つ生成し、既存の最大Id+1から開始する
（保存ファイル上のIdコンパクション：§9.2項目30とは独立。コンパクションは
JsonProjectFileSerializer.Save()内でのみ行い、本Allocatorのライブな状態には影響させない）。

---

##### `public IdAllocator(Func<int, TId> factory, IEnumerable<int> existingIds)`

---

##### `public TId AllocateNext()`

---

#### `DiaEditCore.Session.ProjectSession` (class)

読込中のProjectFileとその派生キャッシュ(TimeTableSetCache)のライフサイクルを一元管理する。
CommandInvokerからのICacheChangeObserver通知を受けてキャッシュをdirty化し、
次にキャッシュへアクセスする直前に遅延再構築する（discard-and-regenerateの原則、§8.2項目1）。
構造的防止の方針：生のTimeTableSetCacheを各Commandのコンストラクタへ
直接渡す現行シグネチャは、呼び出し側がRebuildを忘れても静的に検知できないため廃止する。
各Commandは本クラスを受け取り、GetCache()経由でのみキャッシュへアクセスする形に統一する。
Composition層での登録単位：Singleton（CommandInvoker・ChangeNotificationBridgeと同じライフタイム）。

| Field | Type |
|---|---|
| Current | `ProjectFile` (既定値 `null!`) |
| StationIds | `IdAllocator<StationId>` (既定値 `null!`) |
| RailIds | `IdAllocator<RailId>` (既定値 `null!`) |
| FloorUnitIds | `IdAllocator<FloorUnitId>` (既定値 `null!`) |
| NoneEndpointIds | `IdAllocator<NoneEndpointId>` (既定値 `null!`) |
| BoundaryPointIds | `IdAllocator<BoundaryPointId>` (既定値 `null!`) |
| EntryPointIds | `IdAllocator<EntryPointId>` (既定値 `null!`) |
| BufferStopIds | `IdAllocator<BufferStopId>` (既定値 `null!`) |
| SwitcherIds | `IdAllocator<SwitcherId>` (既定値 `null!`) |
| PlatformIds | `IdAllocator<PlatformId>` (既定値 `null!`) |

---

##### `public ProjectSession(CommandInvoker invoker)`

---

##### `public void Load(ProjectFile project)`

---

##### `public TimeTableSetCache GetCache()`

派生キャッシュを取得する。dirtyなら全Builderを再実行してから返す。
Commandのコンストラクタは、生のTimeTableSetCacheではなく本メソッド経由でのみ
キャッシュへアクセスすること（構造的防止の主眼）。

### 6.4 Commands

#### `DiaEditCore.Commands.CommandInvoker` (class)

UndoableCommandの実行・取り消し・やり直しを管理し、ICacheChangeObserverへの通知を担う
（6.11節・7.3節「変更通知の流れ」）。
Composition層での登録単位：Singleton想定（アプリ全体でUndo/Redo履歴・購読者一覧を共有するため）。
DiaEditApp.ViewModels側のChangeNotificationBridgeが、コンストラクタでこのCommandInvokerに
Subscribeすることで通知を受け取る（論点K：ChangeNotificationBridge自体もSingleton想定）。

| Field | Type |
|---|---|
| CanUndo | `bool` |
| CanRedo | `bool` |

---

##### `public void Subscribe(ICacheChangeObserver observer)`

---

##### `public void Unsubscribe(ICacheChangeObserver observer)`

---

##### `public void Execute(IUndoableCommand command)`

コマンドを実行し、Undoスタックに積む。新規コマンド実行によりRedo履歴は破棄される
（Undo→別の変更、という操作列でRedoが無効になるのは一般的なエディタの挙動を踏襲）。

---

##### `public void Undo()`

---

##### `public void Redo()`

---

#### `DiaEditCore.Commands.IUndoableCommand` (interface)

CommandInvoker（呼び出し元）がコマンドの型引数を意識せずUndo/Redoスタックへ積めるようにするための
非ジェネリック契約。UndoableCommand&lt;TTarget, TSnapshot&gt;はこれを実装する。

---

##### `IReadOnlySet<ObjectId> Execute()`

---

##### `IReadOnlySet<ObjectId> Undo()`

---

#### `DiaEditCore.Commands.TransActionCommand` (class)

5.10.1節「(c) TransActionCommand」の実装。複数のUndoableCommandを1つの操作として束ねる。
設計方針（v12.14）：
- コンストラクタはIUndoableCommandそのものではなくFunc&lt;IUndoableCommand&gt;（遅延評価ファクトリ）の
リストを受け取る。理由：Station作成時の空FloorUnit同時生成（4.2節）のように、後続コマンドが
先行コマンドの実行結果（例：CreateStationCommand.Created.Idとして採番されるStationId）に
依存するケースがあり、コンストラクタ時点では後続コマンドを構築できないため。
ファクトリはExecute()内で、直前までのコマンドが実際にExecute()された後に順次呼び出される。
- Undo()は実行済みコマンドを逆順にUndo()する（後から作られた依存が先に取り消される）。
- AffectedIdsは実行時に確定するため、UndoableCommand&lt;TTarget,TSnapshot&gt;基底は使わず
IUndoableCommandを直接実装する。

---

##### `public TransActionCommand(IReadOnlyList<Func<IUndoableCommand>> factories)`

---

##### `public IReadOnlySet<ObjectId> Execute()`

---

##### `public IReadOnlySet<ObjectId> Undo()`

---

#### `DiaEditCore.Commands.UndoableCommand` (class)

6.11節：Undo/Redo可能なコマンドの基底型。
確定した設計方針（v11.39、Composition層セッション）：
- 論点M：スナップショット方式を採用。Execute()前に対象オブジェクトの状態をまるごと複製し、
Undo()はその複製を書き戻すだけにする（逆操作を個別に書く方式は採用しない）。
「複製元＝正」という単純な構造にすることで、コマンドの種類が増えても事故が起きにくい。
- 論点N：AffectedIds（影響を受けるObjectIdの集合）は、DependencyResolver（§6.11、未実装）
による自動計算ではなく、コマンド実装者がコンストラクタで手動列挙する。将来
DependencyResolverができた場合も、この基底型のシグネチャは変えずに済む
（具象コマンド側でAffectedIdsの構築ロジックだけ差し替えればよい）。
- 論点O：Execute()/Undo()はaffectedIdsを返すだけの薄い型とし、ICacheChangeObserverへの
通知責務は持たない（単体テストでObserverのモックが常に必要になることを避けるため）。
通知はCommandInvoker（呼び出し元）が担う。
型引数：
TTarget   ：このコマンドが変更する対象オブジェクトの型（例：Train、StationConnection）。
TSnapshot ：TTargetの状態を複製したスナップショットの型。
不変な値（record等）にして、CaptureSnapshot後にTargetを変更してもスナップショット
自体が影響を受けないようにすること（参照をそのまま持ち回すと複製の意味が無くなる）。

| Field | Type |
|---|---|
| Target | `TTarget` |
| AffectedIds | `IReadOnlySet<ObjectId>` |

---

##### `protected UndoableCommand(TTarget target, IReadOnlySet<ObjectId> affectedIds)`

---

##### `protected abstract TSnapshot CaptureSnapshot(TTarget target)`

---

##### `protected abstract void Apply(TTarget target)`

---

##### `protected abstract void Restore(TTarget target, TSnapshot snapshot)`

---

##### `protected virtual IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(TTarget target)`

Execute()（Apply直後）で呼ばれ、以降Undo()が返すAffectedIdsを確定・凍結する。
既定はコンストラクタ渡しのAffectedIdsをそのまま返す（既存の全コマンドと同じ挙動、
後方互換）。Create系コマンドなど、Apply()完了までIdが定まらずコンストラクタ時点では
自身のObjectIdを含められないケースのみオーバーライドする（§9.1項目23関連の追加対応）。

---

##### `public IReadOnlySet<ObjectId> Execute()`

---

##### `public IReadOnlySet<ObjectId> Undo()`

#### 6.4.1 Stations

#### `DiaEditCore.Commands.Stations.ChangeFloorUnitAttributesCommand` (class)

「属性変更」パターンのFloorUnit向け実装。対象フィールドはNameのみ。

---

##### `public ChangeFloorUnitAttributesCommand(FloorUnit target, FloorUnitSnapshot newValues, ProjectSession session)`

---

##### `protected override FloorUnitSnapshot CaptureSnapshot(FloorUnit target)`

---

##### `protected override void Apply(FloorUnit target)`

---

##### `protected override void Restore(FloorUnit target, FloorUnitSnapshot snapshot)`

---

#### `DiaEditCore.Commands.Stations.ChangeStationAttributesCommand` (class)

「属性変更」パターンの最初の具象実装。
AffectedIdsはStation自身のObjectIdからDependencyResolver.ResolveAffectedで算出する

---

##### `public ChangeStationAttributesCommand(Station target, StationSnapshot newValues, ProjectSession session)`

---

##### `protected override StationSnapshot CaptureSnapshot(Station target)`

---

##### `protected override void Apply(Station target)`

---

##### `protected override void Restore(Station target, StationSnapshot snapshot)`

---

#### `DiaEditCore.Commands.Stations.CreateFloorUnitCommand` (class)

「新規登録（Create）」パターンのFloorUnit向け実装。CreateStationCommandと同型。
AffectedIdsについて：ObjectId.csにFloorUnitObjectIdが未定義のため（DependencyResolver
のグラフにFloorUnitは組み込まれていない）、DependencyResolver.ResolveAffectedは使わず空集合を渡す。
将来FloorUnitObjectIdを追加しグラフに組み込む場合は、本コマンドとDeleteFloorUnitCommand（未実装）
の両方でAffectedIds算出方法を見直すこと。
単独では通常呼び出さない想定：Station作成時はStationCreationWorkflow.CreateStationWithDefaultFloorUnit
（TransActionCommand経由）が本コマンドを内包する形で使う（n≥1制約：Stationは1件以上のFloorUnitを持つ、を保存時検証違反にしないため）。

| Field | Type | 説明 |
|---|---|---|
| Created | `FloorUnit?` | Execute()実行後、生成されたFloorUnitを呼び出し元が参照するためのプロパティ。 |

---

##### `public CreateFloorUnitCommand(List<FloorUnit> floorUnits, IdAllocator<FloorUnitId> idAllocator, StationId stationId, string name = "", int displayOrder = 0)`

---

##### `protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<FloorUnit> target)`

---

##### `protected override FloorUnit? CaptureSnapshot(List<FloorUnit> target)`

---

##### `protected override void Apply(List<FloorUnit> target)`

---

##### `protected override void Restore(List<FloorUnit> target, FloorUnit? snapshot)`

---

#### `DiaEditCore.Commands.Stations.CreateStationCommand` (class)

「新規登録（Create）」パターンの最初の具象実装。
ID採番方針：セッション中は既存Stationの最大IdValue+1を単純採番する
（欠番は詰めない）。Undo/Redoスタックが生きている間はIDの安定性を優先するため。
欠番を詰める再採番（コンパクション）はプロジェクト読込時（Undoスタックがまだ存在しない
タイミング）に限定して行う方針とし、別タスクとして切り出す。
AffectedIdsは「新規オブジェクトは他から参照されないため空」。
TTargetは追加先のList&lt;Station&gt;そのもの（ProjectFile.Stations等、呼び出し元が渡す）。
CaptureSnapshotは「実行前は存在しない」ことを表すためnullを返し、Restoreでは
Apply時に生成したインスタンスをリストから除去する（属性変更パターンと異なり、
スナップショットは「値の複製」ではなく「実行前に存在しなかった」という事実そのもの）。

| Field | Type | 説明 |
|---|---|---|
| Created | `Station?` | Execute()実行後、生成されたStationを呼び出し元（ViewModel/UI層）が参照するためのプロパティ。
AffectedIdsが空集合のため、生成結果をUIへ伝える手段として別途公開する。 |

---

##### `public CreateStationCommand(List<Station> stations, IdAllocator<StationId> idAllocator, DisplayName displayName, StationType type, string operatingCode = "", string telegraphCode = "")`

---

##### `protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<Station> target)`

---

##### `protected override Station? CaptureSnapshot(List<Station> target)`

---

##### `protected override void Apply(List<Station> target)`

---

##### `protected override void Restore(List<Station> target, Station? snapshot)`

---

#### `DiaEditCore.Commands.Stations.DeleteFloorUnitCommand` (class)

「削除（Delete）」パターンのFloorUnit向け実装。
検査順序：①n≥1制約（Stationカーディナリティ、コマンド固有のドメインルール）→
②直接参照元（DependencyResolver.ResolveDirectDependents、汎用の1ホップ拒否ロジック）。
双方とも「コマンド層とUI層の責務分担」における「ハード制約」に該当するため、
いずれか一方でも該当すればコンストラクタで例外を送出しコマンド生成自体を失敗させる。

---

##### `public DeleteFloorUnitCommand(List<FloorUnit> floorUnits, FloorUnit floorUnitToDelete, ProjectSession session)`

---

##### `protected override FloorUnit CaptureSnapshot(List<FloorUnit> target)`

---

##### `protected override void Apply(List<FloorUnit> target)`

---

##### `protected override void Restore(List<FloorUnit> target, FloorUnit snapshot)`

---

#### `DiaEditCore.Commands.Stations.DeleteStationCommand` (class)

6.1節「削除（Delete）」パターンの最初の具象実装。
参照元が残ったまま削除された場合の扱い（6.1節の未確定事項、v12.11セッションで確定）：
execute時点で拒否する方針を採用。コンストラクタで直接の参照元（1ホップ、
DependencyResolver.ResolveDirectDependentsで判定）の存在を検査し、1件でもあれば
InvalidOperationExceptionを送出してコマンド生成自体を失敗させる
（SyncRunSegmentsToTrainCommandのCreate()失敗パターンを踏襲）。
波及的な影響先（ResolveAffectedによる多ホップ探索）ではなく直接の参照元のみを判定対象とする。
ResolveAffectedはあくまで「削除時に道連れで無効化されるキャッシュの洗い出し」用途であり、
「削除を妨げるべき参照」とは別の関心事であるため（例：StationConnectionSegmentはStationConnection
を経由して間接的にStationへ辿り着くが、これを理由に削除を拒むのは過剰）。
AffectedIdsはDependencyResolver.ResolveAffectedによる通常の波及算出（6.1節の削除パターン規約通り）。
StationはAppy/Restoreで内部フィールドを書き換えないため、TSnapshotはStation自身の参照を
そのまま保持する（Cloneは不要。属性変更パターンと異なり、削除パターンでは対象を書き換えず
リストへの出し入れのみを行うため、参照共有による事故が起きない）。
v12.21：コンストラクタ引数をTimeTableSetCache cache → ProjectSession sessionへ移行
（§9.1項目5、構造的防止の方針）。

---

##### `public DeleteStationCommand(List<Station> stations, Station stationToDelete, ProjectSession session)`

---

##### `protected override Station CaptureSnapshot(List<Station> target)`

---

##### `protected override void Apply(List<Station> target)`

---

##### `protected override void Restore(List<Station> target, Station snapshot)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitSnapshot` (record)

FloorUnit.Nameのスナップショット。
DisplayOrderはこのコマンドのスコープ外とする（並べ替え操作専用のReorderFloorUnitsCommandへ
責務分離。「同一StationId内で一意」という制約を、単発の属性変更コマンドが個別に
満たそうとするとバリデーションが必要になるが、並べ替えという専用操作の形にすることで
重複したDisplayOrderを持つ中間状態自体を構造的に生成不能にできるため）。

| Field | Type |
|---|---|
| Name | `string` |

---

#### `DiaEditCore.Commands.Stations.ReorderFloorUnitsCommand` (class)

「並べ替え」パターンの新規実装（§9.2項目24の解消）。
DisplayOrderを生の整数値として個別編集させず、「同一Station配下FloorUnit全件の新しい並び順」
という単位でのみ変更を受け付ける。これにより重複したDisplayOrderを経由する中間状態が
構造的に発生し得ない（4.0節：構造的予防 over ランタイム検証）。
AffectedIdsについて：対象はStation配下の複数FloorUnitのため、それぞれのFloorUnitObjectIdを
changedIdsとしてDependencyResolver.ResolveAffectedへ渡し、和集合を取る
（ChangeStationAttributesCommand等の単一対象パターンとは異なる点に注意）。
TargetはList&lt;FloorUnit&gt;全体（該当Stationに限らずプロジェクト全FloorUnitのリスト）とする。
Apply/Restoreは渡されたnewOrder（またはスナップショット）に含まれるFloorUnitId群のみを
target内から検索して書き換えるため、リスト自体の要素追加・削除は行わない
（CreateFloorUnitCommand等のCreate/Deleteパターンと異なり、このコマンドは既存要素の
属性書き換えのみを行う「属性変更」パターンの一種と位置づけられる）。

---

##### `public ReorderFloorUnitsCommand(List<FloorUnit> floorUnits, StationId stationId, IReadOnlyList<FloorUnitId> newOrder, ProjectSession session)`

---

##### `protected override IReadOnlyList<(FloorUnitId Id, int DisplayOrder)> CaptureSnapshot(List<FloorUnit> target)`

---

##### `protected override void Apply(List<FloorUnit> target)`

---

##### `protected override void Restore(List<FloorUnit> target, IReadOnlyList<(FloorUnitId Id, int DisplayOrder)> snapshot)`

---

#### `DiaEditCore.Commands.Stations.StationCreationWorkflow` (class)

4.2節「運用：n≥1制約を保つため、Station作成操作は空のFloorUnit同時生成までを1つの複合コマンド
（TransActionCommand）とする」の実装。
CreateStationCommand単体をUI層が直接呼び出すと、Station作成直後〜FloorUnit追加までの間
n≥1制約（保存時検証）に違反した状態が生じうる。本ワークフローはCreateStationCommandと
CreateFloorUnitCommandをTransActionCommandで束ね、常に両方が揃った状態のみをUndo単位とする。
FloorUnitのStationIdはCreateStationCommand.Execute()実行後でなければ確定しないため、
2つ目のファクトリはTransActionCommand内部での遅延評価（Func&lt;IUndoableCommand&gt;）を利用し、
createStation.Createdへのクロージャ参照を通じて実行時に解決する。

---

##### `public static TransActionCommand CreateStationWithDefaultFloorUnit(List<Station> stations, List<FloorUnit> floorUnits, IdAllocator<StationId> stationIds, IdAllocator<FloorUnitId> floorUnitIds, DisplayName displayName, StationType type, string operatingCode = "", string telegraphCode = "")`

---

#### `DiaEditCore.Commands.Stations.StationSnapshot` (record)

Station.DisplayName / Type / OperatingCode / TelegraphCode /
ShowsInStationTimetableOverride のスナップショット。
DisplayNameは参照型（class）のため、CaptureSnapshot時・コンストラクタでのnewValues受け取り時の
両方でDisplayName.Clone()を経由し、外部インスタンスへの参照を一切保持しない（UndoableCommand基底の
「不変な値にすること」規約への対応。DisplayNameそのものをrecord化する変更は影響範囲が大きいため
見送り、Clone()による防御的コピーで対応する）。

| Field | Type |
|---|---|
| DisplayName | `DisplayName` |
| Type | `StationType` |
| OperatingCode | `string` |
| TelegraphCode | `string` |
| ShowsInStationTimetableOverride | `bool?` |

#### 6.4.2 Stations/FloorUnitObjects

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.BoundaryPointCreationSpec` (record)

| Field | Type |
|---|---|
| FloorUnitId | `FloorUnitId` |
| Position | `Point` |
| Name | `string` |

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.BufferStopCreationSpec` (record)

| Field | Type |
|---|---|
| FloorUnitId | `FloorUnitId` |
| Position | `Point` |
| Name | `string` |

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.ChangePlatformAttributesCommand` (class)

「属性変更」パターンのPlatform向け実装（§9.2項目34）。ChangeRailAttributesCommandに続く
実装例だが、Rail側4フィールドが全て値型だったのに対し、Platformは参照型コレクション
（FacingRailIds）を含む点が異なる。
AffectedIdsはDependencyResolver.ResolveAffectedで算出。DependencyResolverのグラフ上、
PlatformObjectIdは現時点で終端ノード（他オブジェクトへの波及ルール未定義）のため、
AffectedIdsは対象自身のみとなる。

---

##### `public ChangePlatformAttributesCommand(Platform target, PlatformSnapshot newValues, ProjectSession session)`

---

##### `protected override PlatformSnapshot CaptureSnapshot(Platform target)`

---

##### `protected override void Apply(Platform target)`

---

##### `protected override void Restore(Platform target, PlatformSnapshot snapshot)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.ChangeRailAttributesCommand` (class)

「属性変更」パターンのRail向け実装。ChangeStationAttributesCommandに続く
2例目であり、値型のみで構成されるフィールド集合に対する適用例となる。
AffectedIdsは対象自身のObjectIdからDependencyResolver.ResolveAffectedで算出する。
DependencyResolverのグラフ上、RailObjectIdは現時点で終端ノード（他オブジェクトへの波及ルールが未定義）のため、
AffectedIdsは対象自身のみとなる。
session.GetCache()は一度だけ呼び出し、以降はローカル変数に保持したTimeTableSetCacheをそのままDependencyResolverへ渡す
（コンストラクタ実行中に複数回GetCache()を呼ぶと、その間にdirty化されるケースは無いはずだが、
同一コンストラクタ内で参照するキャッシュ内容が呼び出しごとにブレる可能性を構造的に排除するため）。

---

##### `public ChangeRailAttributesCommand(Rail target, RailSnapshot newValues, ProjectSession session)`

---

##### `protected override RailSnapshot CaptureSnapshot(Rail target)`

---

##### `protected override void Apply(Rail target)`

---

##### `protected override void Restore(Rail target, RailSnapshot snapshot)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.ChangeSwitcherAttributesCommand` (class)

Switcherの属性変更コマンド（PortCount／Mechanism／ValidRoutesの3フィールド）。

> 「属性変更コマンドのスコープ分割方針」に基づき検討した結果、Switcherは
> Rail（純粋属性／接続トポロジー／形状データの3分割）のような分割対象を持たない
> （Base.Position・FloorUnitIdはSwitcher単体では変更されず、収束変換ワークフロー側の
> 責務のため本コマンドのスコープには含めない）。3フィールドとも「フォーム一括保存で完結する」
> 単一の編集単位と判断し、分割しない。
> 呼び出し元は主に2通り：
> (1) UI側の通常編集（Switcher選択→Port関連設定パネルでMechanism/ValidRoutesを編集→決定）。
> (2) のSwitcherExpandケース
> （N=3,4への収束によるPortCount拡張）。この場合はMechanism/ValidRoutesをクリア（null／空配列）した
> 状態で一旦確定させ、ユーザーに再設定を促す（v13.13確定仕様）。この呼び出しでは新しいMechanism/ValidRoutes
> の入力は伴わない＝クリアするだけの1回のコマンド発行で足りる。

---

##### `public ChangeSwitcherAttributesCommand(List<Switcher> switchers, SwitcherId switcherId, int newPortCount, SwitchMechanism? newMechanism, IReadOnlyList<PortPair> newValidRoutes, IReadOnlySet<ObjectId> affectedIds)`

コマンドを構築する。この時点では属性変更は行われない。

**Parameters**

- `switchers`: 変更対象のSwitcherを含むリスト。
- `switcherId`: 変更対象SwitcherのId。
- `newPortCount`: 変更後のPortCount。
- `newMechanism`: 変更後のMechanism。クリアする場合はnull。
- `newValidRoutes`: 変更後のValidRoutes。呼び出し元がUI編集用に保持する配列と共有される可能性があるため、
コンストラクタ内で防御的コピーを行う。
- `affectedIds`: 呼び出し元が事前に確定させたAffectedIds。

---

##### `protected override (int PortCount, SwitchMechanism? Mechanism, IReadOnlyList<PortPair> ValidRoutes) CaptureSnapshot(List<Switcher> target)`

変更対象Switcherの現在値（PortCount／Mechanism／ValidRoutes）をスナップショットとして取得する。

**Parameters**

- `target`: Switcherを含むリスト。

**Returns**
変更前のPortCount／Mechanism／ValidRoutes（ValidRoutesは防御的コピー済み）。

---

##### `protected override void Apply(List<Switcher> target)`

対象SwitcherのPortCount／Mechanism／ValidRoutesを新しい値へ変更する。

**Parameters**

- `target`: Switcherを含むリスト。

---

##### `protected override void Restore(List<Switcher> target, (int PortCount, SwitchMechanism? Mechanism, IReadOnlyList<PortPair> ValidRoutes) snapshot)`

対象SwitcherのPortCount／Mechanism／ValidRoutesを変更前の状態へ戻す。

**Parameters**

- `target`: Switcherを含むリスト。
- `snapshot`: が返したスナップショット。

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.CreateFloorUnitObjectCommand` (class)

FloorUnitObjectBase＋Idのみで構成される「新規登録（Create）」パターンの汎用実装。
BoundaryPoint／BufferStop／NoneEndpointはこの形に完全一致するため個別クラスを持たず本コマンドを直接使う。
EntryPointのみType追加フィールドを持つため、factoryのクロージャでTypeを固定して吸収する
（§9.2項目10横展開の一環、コード重複解消のため個別クラス3種を統合）。

| Field | Type | 説明 |
|---|---|---|
| Created | `T?` | Execute()実行後、生成されたオブジェクトを呼び出し元が参照するためのプロパティ。 |

---

##### `public CreateFloorUnitObjectCommand(List<T> target, IdAllocator<TId> idAllocator, Func<TId, T> factory, Func<T, ObjectId> toObjectId)`

---

##### `protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<T> target)`

---

##### `protected override T? CaptureSnapshot(List<T> target)`

---

##### `protected override void Apply(List<T> target)`

---

##### `protected override void Restore(List<T> target, T? snapshot)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.CreateRailCommand` (class)

「新規登録（Create）」パターンのRail向け実装。
v13.9変更（NoneEndpoint実体化に伴う再設計）：EndpointA/Bは常に確定済みの参照として生成する
（旧実装のNoneEndpointRef仮置き→AttachRailEndpointsCommandによる後続上書き、という
2段階方式は廃止）。TransActionCommand内での実行順序を「端点作成→Rail作成」に入れ替えたことで、
Railが一度も無効な参照を持たない（構造的防止の原則により忠実な）状態を実現する。
endpointA/BFactory：RailCreationWorkflow内でTransActionCommandの各ステップが順に実行される際、
端点作成コマンド（Create*Command）のApply()が完了した"後"でなければ生成されたIdが確定しない
ため、StationCreationWorkflow・旧AttachRailEndpointsCommandと同じ遅延評価パターン
（Func&lt;RailEndpointRef&gt;によるクロージャ参照）を用いる。
ID採番はCreateStationCommandと同じ方針（セッション中は最大IdValue+1、欠番は詰めない）。
AffectedIdsは新規登録パターンの規約通り空集合。

| Field | Type | 説明 |
|---|---|---|
| Created | `Rail?` | Execute()実行後、生成されたRailを呼び出し元が参照するためのプロパティ。 |

---

##### `public CreateRailCommand(List<Rail> rails, IdAllocator<RailId> idAllocator, string name, double lengthM, double speedLimitKph, RailRole role, Func<RailEndpointRef> endpointAFactory, Func<RailEndpointRef> endpointBFactory)`

---

##### `protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<Rail> target)`

---

##### `protected override Rail? CaptureSnapshot(List<Rail> target)`

---

##### `protected override void Apply(List<Rail> target)`

---

##### `protected override void Restore(List<Rail> target, Rail? snapshot)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.DeleteFloorUnitObjectCommand` (class)

CreateFloorUnitObjectCommand&lt;TId, T&gt;と対称な、汎用「削除（Delete）」パターンの実装。

> BoundaryPoint／BufferStop／NoneEndpoint／EntryPoint／Switcherのいずれも
> 「Listから対象1件をRemoveするだけ」という同型の削除操作のため、個別クラスを持たず本コマンドを使う。
> 無条件削除である点に注意：本コマンドは参照チェックを一切行わない。
> 呼び出し元（収束変換ワークフロー、Converge*Command）が、
> (1)
> によるStationPath参照チェック、
> (2) による、削除対象を参照していたRail端点の張替え完了、
> を本コマンドの実行前に必ず済ませておく前提とする（TransActionCommand内の実行順序：
> 新規移行先作成 → RailEndPointRefChangerで参照を移行先へ張替え → 本コマンドで旧端点を削除）。
> この順序を守れば、本コマンドが実行される時点で削除対象を参照するRailは存在しない
> （DeleteRailCommandのような実行時の参照元チェックは不要、構造的に安全）。
> AffectedIdsはDelete系の通常パターン通り、呼び出し元がコンストラクタで確定値を渡す
> （Create系と異なりExecute前から削除対象のIdが判明しているため、
> ComputeAffectedIdsAfterApplyのオーバーライドは不要）。

---

##### `public DeleteFloorUnitObjectCommand(List<T> target, T toDelete, IReadOnlySet<ObjectId> affectedIds)`

コマンドを構築する。この時点では削除は行われない（まで副作用なし）。

**Parameters**

- `target`: 削除対象を含むリスト。
- `toDelete`: 削除対象のインスタンス。
- `affectedIds`: 呼び出し元が事前に確定させたAffectedIds。本コマンドは参照チェックを行わないため、
影響範囲の妥当性は呼び出し元の責務。

---

##### `protected override T CaptureSnapshot(List<T> target)`

削除対象インスタンス自身をスナップショットとして返す。

**Parameters**

- `target`: 削除対象を含むリスト（未使用）。

**Returns**
コンストラクタで受け取った削除対象インスタンス。

---

##### `protected override void Apply(List<T> target)`

削除対象をから取り除く。

**Parameters**

- `target`: 削除対象を含むリスト。

---

##### `protected override void Restore(List<T> target, T snapshot)`

削除前の状態へ戻す（削除対象インスタンスをへ再追加する）。

**Parameters**

- `target`: 削除対象を含むリスト。
- `snapshot`: が返したスナップショット（削除対象インスタンス）。

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.DeletePlatformCommand` (class)

「削除（Delete）」パターンのPlatform向け実装（§9.2項目34）。
DeleteRailCommandと異なり、Platformを直接参照する他モデルは現行実装スコープに存在しない
（TemporaryRestriction.TargetはRailのみを対象、Train.StopTime.TrackRailIdもRail参照であり
Platformは非参照）。よってDependencyResolver.ResolveDirectDependents経由のObjectIdグラフ
チェック（現状PlatformObjectId => []で終端）のみで削除可否判定が完結し、DeleteRailCommandの
ような「グラフ外の生ID参照を直接走査する」処理は不要。
将来Platformへの参照を持つモデル（例：StationWork等）が追加された場合は、
DependencyResolver側のPlatformObjectIdケースを更新するだけで本コマンド自体は
変更不要という設計になっている（構造的予防の方針）。

---

##### `public DeletePlatformCommand(List<Platform> platforms, Platform platformToDelete, ProjectSession session)`

---

##### `protected override Platform CaptureSnapshot(List<Platform> target)`

---

##### `protected override void Apply(List<Platform> target)`

---

##### `protected override void Restore(List<Platform> target, Platform snapshot)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.DeleteRailCommand` (class)

「削除（Delete）」パターンのRail向け実装。
v12.18で判明した不備の修正：旧実装（v12.13）はDependencyResolverのObjectIdグラフ
（RailObjectId => []）のみをチェックしていたが、Railへの逆参照3経路は
いずれもObjectIdグラフの外側にある生のRailId参照であり、一度もチェックされていなかった：
1. Platform.FacingRailIds（List&lt;RailId&gt;）
2. TemporaryRestriction.Target is RestrictionTarget.Rail
3. Train.StopTimes[...].TrackRailId（RailId?）
これら3経路は、TemporaryRestrictionBySegmentIndexBuilderのコメントで明言した方針
（「Rail起点の逆引き消費者はDeleteRailCommandのみであり、Rail削除時のチェックは
対象コレクションを直接1回線形走査すれば足りる規模のため、専用インデックス化は見送る」）
に従い、専用キャッシュを設けずコンストラクタ内で直接走査する。
DependencyResolverのObjectIdグラフチェックも引き続き実施する（将来Railへの
ObjectId経由の逆参照を持つモデルが追加された場合に自動的に効くようにするため）。
v12.21：コンストラクタ引数をTimeTableSetCache cache → ProjectSession sessionへ移行
（§9.1項目5、構造的防止の方針）。Platform／TemporaryRestriction／Trainの3コレクションは
TimeTableSetCacheが管理する対象ではない（ProjectFileの生データ）ため、引き続き
呼び出し側から個別に受け取る（ProjectSessionはこれらのコレクション自体を集約管理しない。
5.14.2節：ProjectSessionの責務はTimeTableSetCacheのライフサイクル管理に限定）。

---

##### `public DeleteRailCommand(List<Rail> rails, Rail railToDelete, ProjectSession session, IReadOnlyList<Platform> allPlatforms, IReadOnlyList<TemporaryRestriction> allRestrictions, IReadOnlyList<Train> allTrains)`

---

##### `protected override Rail CaptureSnapshot(List<Rail> target)`

---

##### `protected override void Apply(List<Rail> target)`

---

##### `protected override void Restore(List<Rail> target, Rail snapshot)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.EntryPointCreationSpec` (record)

| Field | Type |
|---|---|
| FloorUnitId | `FloorUnitId` |
| Position | `Point` |
| Type | `EntryPointType` |
| Name | `string` |

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.NoOpCommand` (class)

Reconcileがnull（既に整合済み・何もしない）を返した際のプレースホルダー。
IUndoableCommand.Execute()/Undo()はIReadOnlySet&lt;ObjectId&gt;を返す設計のため、
何もしない場合は空集合を返す（呼び出し側のChangeNotificationBridge等が
空集合を渡された場合に何も通知しないのは、他の変更なしコマンドと同じ挙動のはず）。

---

##### `public IReadOnlySet<ObjectId> Execute()`

---

##### `public IReadOnlySet<ObjectId> Undo()`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.NoneEndpointCreationSpec` (record)

端点を作成せず未接続（NoneEndpointRef）のままにする。

| Field | Type |
|---|---|
| FloorUnitId | `FloorUnitId` |
| Position | `Point` |
| Name | `string` |

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.PlatformSnapshot` (record)

Platform.Name / FacingRailIds / EffectiveLength / SecondaryPosition のスナップショット。
SecondaryPosition（矩形の対角のもう一方の頂点）は座標編集用フィールドとして本コマンドに含める。
Base.Position（対角のもう一方の頂点）自体は他のFloorUnitObjectと共通の仕組み（今後の
ドラッグ操作・端点収束ワークフロー経由）で変更される想定のため、本コマンドの対象外とする。
FacingRailIds（List&lt;RailId&gt;）は参照型のミュータブルなコレクションであるため、
DisplayName（§9.2項目31修正時に判明した問題）と同様の理由で、コンストラクタ・
CaptureSnapshot双方でToList()による防御的コピーを行う。呼び出し元が渡したリスト
インスタンスをスナップショット側がそのまま保持すると、呼び出し元の後続変更が
Undo用スナップショットを汚染しうるため。

| Field | Type |
|---|---|
| Name | `string` |
| FacingRailIds | `IReadOnlyList<RailId>` |
| EffectiveLength | `double?` |
| SecondaryPosition | `Point` |

---

##### `public PlatformSnapshot(string name, IReadOnlyList<RailId> facingRailIds, double? effectiveLength, Point secondaryPosition)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.RailCreationWorkflow` (class)

「Rail作成＝両端点オブジェクトの作成と等価」（Tao様確認済み、v13.7セッション）という業務理解に基づく
複合コマンド。StationCreationWorkflow（Station＋FloorUnit）と同じ設計パターンを踏襲する：
CreateRailCommandと各端点用Create*Commandを独立に実行した後、最後にAttachRailEndpointsCommandで
Rail.EndpointA/Bへ確定させる。この4ステップ（Rail／端点A／端点B／アタッチ）を1つの
TransActionCommandに束ねることで、「両端未接続のRailが宙に浮いた状態のまま保存される」
（n≥1制約と同種の中間不整合状態）をUndo単位のレベルで発生させない。
Switcherはこのワークフローの対象外（コンストラクタ引数に含めない）。既存端点への接続
（新規作成ではなく既存BoundaryPoint/EntryPoint/BufferStop/Switcherへ繋ぐ導線）も対象外とし、
別ワークフローとして将来切り出す（Tao様確認済み）。

---

##### `public static TransActionCommand CreateRailWithEndpoints(List<Rail> rails, IdAllocator<RailId> railIds, string name, double lengthM, double speedLimitKph, RailRole role, RailEndpointCreationSpec endpointA, RailEndpointCreationSpec endpointB, List<NoneEndpoint> noneEndpoints, IdAllocator<NoneEndpointId> noneEndpointIds, List<BoundaryPoint> boundaryPoints, IdAllocator<BoundaryPointId> boundaryPointIds, List<EntryPoint> entryPoints, IdAllocator<EntryPointId> entryPointIds, List<BufferStop> bufferStops, IdAllocator<BufferStopId> bufferStopIds, ProjectSession session)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.RailDeletionWorkflow` (class)

Rail削除を「削除＋両端点の収束再照合（RailEndpointConvergenceWorkflow.Reconcile）」まで
1つのUndo単位にまとめるワークフロー（RailCreationWorkflowと対称）。
既存DeleteRailCommand自体は無改修：Platform/TemporaryRestriction/Train.StopTimeの
3経路参照チェックという既存の責務単体のコマンドとして維持し、本ワークフローは
それを1ステップ目として呼び出すだけの薄いラッパーとする。
呼び出し順序（TransActionCommand内、Execute順）：
1. DeleteRailCommand（既存の3経路チェック＋Rail削除本体）
2. EndpointA側の再照合（Rail削除後の状態でFindConvergingEndpointsを再計算してから呼ぶ）
3. EndpointB側の再照合（同上）
Reconcile呼び出しをFunc&lt;IUndoableCommand&gt;で遅延させているのは、ステップ2・3の
FindConvergingEndpointsがステップ1実行後（railToDeleteがrailsから除去された後）の
状態を見る必要があるため（TransActionCommandの各ステップはExecute時に順次評価される）。
未対応のまま残る論点（次回セッション確認事項）：
RailEndpointConvergenceWorkflow.Reconcile内部のStationPathブロックチェック
（FindBlockingStationPaths）は、BoundaryPoint/SwitcherNew/SwitcherExpandケースの
AddDeleteStepsForVanishingEndpoints経由でのみ呼ばれており、Vanish・Keepケースの
AddDeleteStepForObjectId直接呼び出し経路にはブロックチェックが無い（Reconcile実装時点の
既存の設計、本ワークフロー新設に伴う新規発見）。Vanish/Keepで消滅する端点が
StationPath.Waypointsから参照されるケースが実際にあり得るかは未検証。

---

##### `public static IUndoableCommand Create(List<Rail> rails, Rail railToDelete, ProjectSession session, IReadOnlyList<Platform> allPlatforms, IReadOnlyList<TemporaryRestriction> allRestrictions, IReadOnlyList<Train> allTrains, List<NoneEndpoint> noneEndpoints, List<BoundaryPoint> boundaryPoints, List<EntryPoint> entryPoints, List<BufferStop> bufferStops, List<Switcher> switchers, List<StationPath> stationPaths)`

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.RailEndPointRefChanger` (class)

収束変換ワークフローの内部ステップ：複数のRail端点参照（RailEndpointRef）を
一括で張り替える専用コマンド。

> ChangeRailAttributesCommandのスコープ（EndpointA/Bを除く4フィールド、v13.13確定のサイドパネル仕様）を
> 拡張せず、別クラスとして切り出す。
> 個々のRailごとに専用コマンドを発行せず、収束操作全体（N本のRailの該当端を1つの新しい
> 参照先へ同時に付け替える）を1つのUndo単位として扱う。
> が返す収束集合を
> そのまま_targetsへ渡せる形にしている。
> 「収束（Converge）」＝複数Rail端点を1つの新しい参照先へ集約する操作をここで実装する。
> 「発散（Diverge）」＝RailSplitter（RailMergerの逆操作）に相当する対称操作は
> 未設計・未実装のため、本クラスには含めない（設計確定後に対となるコマンドとして追加する）。
> AffectedIdsについて：呼び出し側（Converge*Command）が、張替え対象Rail群のRailObjectIdに加え、
> 消滅する旧端点オブジェクトのObjectId・新規作成した端点オブジェクトのObjectIdを
> DependencyResolver.ResolveAffectedへ渡した結果を、コンストラクタ引数として明示的に受け取る
> （Create系コマンドと異なり、張替え対象は呼び出し時点で全て確定しているため、
> ComputeAffectedIdsAfterApplyフックは不要）。

---

##### `public RailEndPointRefChanger(List<Rail> rails, IReadOnlyList<(RailId RailId, RailEnd End)> targets, RailEndpointRef newRef, IReadOnlySet<ObjectId> affectedIds)`

コマンドを構築する。この時点では張替えは行われない。

**Parameters**

- `rails`: 張替え対象のRailを含むリスト。
- `targets`: 張替え対象の(RailId, RailEnd)組（収束集合、
の戻り値からRef部分を除いたもの）。
- `newRef`: 張替え後、全対象が共通して指す新しい参照（新規BoundaryPoint／新規または拡張後Switcherの
SwitcherEndpointRef＋対応PortIndexなど、呼び出し側で確定済みのもの）。
- `affectedIds`: 呼び出し元が事前に確定させたAffectedIds。

---

##### `protected override IReadOnlyList<(RailId RailId, RailEnd End, RailEndpointRef OldRef)> CaptureSnapshot(List<Rail> target)`

張替え対象各RailについてExecute前の(RailId, RailEnd, 旧参照)の組をスナップショットとして取得する。

**Parameters**

- `target`: Railを含むリスト。

**Returns**
張替え対象ごとの旧参照を保持するリスト。

---

##### `protected override void Apply(List<Rail> target)`

張替え対象の全端点をへ変更する。

**Parameters**

- `target`: Railを含むリスト。

---

##### `protected override void Restore(List<Rail> target, IReadOnlyList<(RailId RailId, RailEnd End, RailEndpointRef OldRef)> snapshot)`

張替え対象の全端点を、張替え前の参照へ戻す。

**Parameters**

- `target`: Railを含むリスト。
- `snapshot`: が返したスナップショット。

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.RailEndpointConvergenceWorkflow` (class)

ある座標における収束状態（の結果）と、
その座標に現在存在する端点オブジェクトの型が一致しているかを比較し、不一致の場合のみ
「新規作成→で参照張替え→旧端点削除」の変換ステップを組み立てる。

> 増加方向（Ctrl+ドラッグでの新規Rail作成・既存端点へのドラッグ接続）・
> 減少方向（DeleteRailCommandによるRail削除）のいずれから呼ばれても同一ロジックで動作する
> （Converge/Divergeを別実装にしない）。
> 前提：converging（の戻り値）は
> 呼び出し時点で最新の状態を反映していること（DeleteRailCommandから呼ぶ場合は、対象RailをTargetから
> 除去した後に再計算すること）。
> StationPathブロックチェック（）は
> 本ワークフローの責務外：呼び出し元（実際にコマンドをExecuteする側）が事前に呼び、
> ブロックされていれば本ワークフローを呼ばずに操作自体を中断すること。

---

##### `public static IUndoableCommand? Reconcile(Point position, FloorUnitId floorUnitId, IReadOnlyList<RailEndpointLocation> converging, ObjectId? oldObjectId, ProjectSession session, List<Rail> rails, List<NoneEndpoint> noneEndpoints, IdAllocator<NoneEndpointId> noneEndpointIds, List<BoundaryPoint> boundaryPoints, IdAllocator<BoundaryPointId> boundaryPointIds, List<EntryPoint> entryPoints, List<BufferStop> bufferStops, List<Switcher> switchers, IdAllocator<SwitcherId> switcherIds, List<StationPath> stationPaths)`

再照合を実行する。既に整合していればnullを返す（TransActionCommandを組まない＝Undo単位を作らない）。

**Parameters**

- `position`: 再照合対象の座標。
- `floorUnitId`: 新規作成する端点オブジェクトが属するFloorUnitId。
- `converging`: 現在の収束集合。
- `oldObjectId`: この座標に元々存在していた端点オブジェクトのObjectId（Vanish判定・削除対象特定に使用）。
元々何も存在しなかった座標（真の新規収束）ではnullを渡す。
- `session`: 呼び出し規約上受け取るが、本メソッド内部では未使用（将来の拡張余地として引数のみ保持）。
- `rails`: Rail端点張替え対象となる全Rail。
- `noneEndpoints`: 現在のNoneEndpointコレクション。
- `noneEndpointIds`: NoneEndpoint用のId採番器。
- `boundaryPoints`: 現在のBoundaryPointコレクション。
- `boundaryPointIds`: BoundaryPoint用のId採番器。
- `entryPoints`: 現在のEntryPointコレクション。
- `bufferStops`: 現在のBufferStopコレクション。
- `switchers`: 現在のSwitcherコレクション。
- `switcherIds`: Switcher用のId採番器。
- `stationPaths`: StationPathブロックチェック対象の全StationPath。

**Returns**
変換が必要な場合は、その変換ステップ一式を束ねた。
既に整合済み、またはVanishかつがnull（元々何も存在しなかった）の場合はnull。

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.RailEndpointCreationSpec` (record)

Rail作成時の各端点の生成方法を指定する判別共用体。
§7.1確定仕様「駅構内オブジェクト新規配置」：独立配置操作はRail・Platform・SignalSetの3種のみで、
BoundaryPoint/EntryPoint/BufferStopはRail端点クリック→属性タブでの端点種別選択から生成される
（Switcherは収束検出・テンプレート配置の別導線のため、本ワークフローの選択肢に含めない）。

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.RailSnapshot` (record)

Rail.Name / LengthM / SpeedLimitKph / Role のスナップショット。
スコープについて：Rail.EndpointA/EndpointB（接続トポロジー変更）とControlPoints
（線路形状編集）はこのコマンドのスコープ外とする。前者はSwitcher等のコマンド実装時に、
DependencyResolverのグラフ更新との整合を含めて別途設計する。後者はUI上「フォーム一括保存」
ではなく「制御点の個別追加・移動」という異なる編集単位になるため、専用コマンドとして
将来切り出す。
4フィールドは全て値型（string／double／enum）であり、DisplayNameのような参照型が絡まないため、
StationSnapshotで必要だったClone()経由の防御的コピーは不要（単純代入で「不変な値」規約を満たす）。

| Field | Type |
|---|---|
| Name | `string` |
| LengthM | `double` |
| SpeedLimitKph | `double` |
| Role | `RailRole` |

#### 6.4.3 TimeTable/Trains

#### `DiaEditCore.Commands.TimeTable.Trains.SyncRunSegmentsSnapshot` (record)

Sync前のTrain状態のスナップショット（Undo用）。

| Field | Type |
|---|---|
| RunSegments | `List<TrainRunSegment>` |
| StopTimes | `Dictionary<StopKey, StopTime>` |

---

#### `DiaEditCore.Commands.TimeTable.Trains.SyncRunSegmentsToTrainCommand` (class)

6.2節：Train.RunSegmentsをServiceRoute（経由MainRoute.StationOrder）へ明示的に再同期する。
v12.5確定：RunSegmentsはユーザーが直接編集せず、この派生データを再生成するコマンドのみが
構造を変更する。ユーザーが直接編集できるのは「使用StationConnectionの選択」（汎用属性変更
パターン、本コマンドの対象外）のみ。
孤立StopTimeの扱い（セッション確定）：新StopKey列に含まれなくなったStopTimeは、Worksの
有無に関わらず無条件破棄する。他Trainからの外部参照（StopKeyReferenceIndex経由）がある
場合もexecute自体はブロックしない。参照元Trainは本コマンドのAffectedIdsに含め、実在性の
検証はSaveValidationRunner側のCross Validator（§9.2項目10、未実装）に委ねる。
個別上書きの引き継ぎ：既存Train.RunSegmentsのホップ(From,To)が新経路にも残存し、かつ
IsOverriddenFromTemplate=trueの場合は、そのStationConnectionId選択をそのまま引き継ぐ。

---

##### `public static SyncRunSegmentsToTrainCommand Create(Train train, ServiceRoute serviceRoute, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments, ProjectSession session)`

同期計画（新RunSegments・新StopTimes・AffectedIds）を確定させたうえでコマンドを生成する。
ServiceRouteSegmentに対応するStationConnectionが0件／複数件で一意に決まらない場合は
InvalidOperationExceptionを送出し、コマンド生成自体を失敗させる（Execute()には進ませない）。

---

##### `protected override SyncRunSegmentsSnapshot CaptureSnapshot(Train target)`

---

##### `protected override void Apply(Train target)`

---

##### `protected override void Restore(Train target, SyncRunSegmentsSnapshot snapshot)`

---

##### `internal static List<TrainRunSegment> BuildNewRunSegments(Train train, ServiceRoute serviceRoute, IReadOnlyList<MainRoute> allMainRoutes, IReadOnlyList<StationConnection> allStationConnections, IReadOnlyList<StationConnectionSegment> allSegments)`

---

##### `internal static (Dictionary<StopKey, StopTime> NewStopTimes, List<StopKey> OrphanedKeys) BuildNewStopTimes(Train train, List<TrainRunSegment> newRunSegments)`

---

##### `internal static IReadOnlySet<ObjectId> BuildAffectedIds(Train train, List<StopKey> orphanedKeys, ProjectSession session)`

### 6.5 Composition

#### `DiaEditCore.Composition.CoreServiceCollectionExtensions` (class)

DiaEditCore側のDIコンテナ登録（7.3節・8.2節旧項目9）。
論点J（v11.39確定）：IValidator&lt;T&gt;実装（StationValidator等22個）は、アセンブリスキャンによる
自動登録ではなく明示的登録の方針とした。ただし現時点ではSaveValidationRunnerがそれぞれを
`new`で直接インスタンス化しており（状態を持たないためDI経由で共有する必要が無い）、
DIコンテナ経由でIValidator&lt;T&gt;を解決する具体的な利用箇所が無い。そのため、本メソッドでは
Validatorの登録は行わない。ViewModel層等がValidatorをDI経由で必要とする具体的な用途が
出てきた時点で、SaveValidationRunner.csの列挙をそのままこちらへ複製する形で追加する
（§8.2項目9の教訓：紛らわしい命名の実装を誤登録しないよう、コピー元を1箇所に保つ）。

---

##### `public static IServiceCollection AddDiaEditCore(this IServiceCollection services)`

### 6.6 ChangeNotification

#### `DiaEditCore.ChangeNotification.ICacheChangeObserver` (interface)

UndoableCommand.Execute()/Undo()実行後、影響を受けたObjectIdの集合を受け取る通知インターフェース。
DiaEditCoreはこのインターフェースを公開するのみで、実装（購読側、DiaEditApp.ViewModels側のChangeNotificationBridge）
が誰かを一切知らない（DIPを満たす形でApp側との依存を切る）。

---

##### `void OnChanged(IReadOnlySet<ObjectId> affectedIds)`

### 6.7 Serialization

#### 6.7.1 Json

#### `DiaEditCore.Serialization.Json.IntIdJsonConverter` (class)

---

##### `public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)`

---

##### `public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)`

---

#### `DiaEditCore.Serialization.Json.IntIdJsonConverterFactory` (class)

IIntIdを実装するreadonly record struct（StationId等）を、
{"value": 5} のようなネストしたオブジェクトではなく素朴な数値 5 としてシリアライズ／デシリアライズする。
§8.2項目2（v11.36でクローズ）。
対象型はIIntIdを実装し、かつ「int1つだけを受け取るコンストラクタ」を持つことを前提とする
（Ids.csの`readonly record struct XxxId(int Value) : IIntId`という宣言パターンに合致する型のみ対応）。
対象外の型に対してCanConvertがfalseを返すことで、他の型のシリアライズには一切影響しない。

---

##### `public override bool CanConvert(Type typeToConvert)`

---

##### `public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)`

---

#### `DiaEditCore.Serialization.Json.JsonProjectFileSerializer` (class)

ProjectFileの保存・読込（1プロジェクト1JSON、7.3.1節・§8.2項目2/13クローズ）。
SchemaVersion検証・保存時の全Validator実行（SaveValidationRunner）ゲートをここに集約する。

---

##### `public static void Save(ProjectFile project, string path)`

保存する。SaveValidationRunner.ValidateAllで1件でもissueが検出された場合、
ファイルへは一切書き込まずProjectFileValidationExceptionを送出する
（本プロジェクトの運用ではValidationSeverity.Warning＝保存不可相当）。

---

##### `public static ProjectFile Load(string path)`

読込む。SchemaVersionが未対応の場合はUnsupportedSchemaVersionExceptionを送出する。
読込内容自体のバリデーション（ファイルは開けたが内容が不正）は呼び出し側の判断とする
（読込直後にSaveValidationRunner.ValidateAllを呼んでUIへ警告表示する等）。

---

#### `DiaEditCore.Serialization.Json.ObjectIdJsonConverter` (class)

ObjectId（判別共用体：BoundaryPoint / EntryPoint / BufferStop / Switcher /
VirtualConflictObject / Rail / StationConnectionSegment）用の独自JsonConverter。
実装パターンはRestrictionTargetJsonConverterと同一（7.3.1節、§8.2項目13）。
各派生型は対応するID型を1つだけ保持するという共通の形を持つが、将来この前提が崩れる
可能性を排除できないため、あえて汎用化せず本Converter内で派生型ごとに明示的に分岐する。

---

##### `public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)`

---

##### `public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options)`

---

#### `DiaEditCore.Serialization.Json.ProjectFileValidationException` (class)

| Field | Type |
|---|---|
| Issues | `IReadOnlyList<IValidationIssue>` |

---

##### `public ProjectFileValidationException(IReadOnlyList<IValidationIssue> issues)`

---

#### `DiaEditCore.Serialization.Json.RailEndpointRefJsonConverter` (class)

RailEndpointRef（判別共用体：None / BoundaryPoint / EntryPoint / BufferStop / Switcher）用の
独自JsonConverter。実装パターンはRestrictionTargetJsonConverterと同一（7.3.1節、§8.2項目13）。
SwitcherEndpointRefのみportIndexという追加フィールドを持つ点に注意。

---

##### `public override RailEndpointRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)`

---

##### `public override void Write(Utf8JsonWriter writer, RailEndpointRef value, JsonSerializerOptions options)`

---

#### `DiaEditCore.Serialization.Json.RestrictionTargetJsonConverter` (class)

RestrictionTarget（判別共用体：Segment / Rail）用の独自JsonConverter。
組み込みの[JsonPolymorphic]/[JsonDerivedType]属性を使わない理由：それらをModel層のクラスに
直接付けると、Model層がSystem.Text.Json.Serializationへ依存することになり、
「Modelは薄いPOCOであるべき」という層分離の原則に反するため（§8.2項目2、v11.36で確定）。
種別ラベルは"kind"フィールドに明示する（値の形からの逆算による判別は、将来型が増えた際に
形が衝突する可能性を排除できないため採用しない。§8.2項目2 v11.36変更履歴参照）。
他の判別共用体（TrainCutPoint関連やRailEndpointRef等）にも同一パターンを適用する。
このクラスをテンプレートとして、対象の共用体ごとに専用Converterを1つずつ用意する方針とする
（JsonConverterFactoryによる汎用化は、共用体ごとに派生型・保持フィールドの形が異なるため見送った）。

---

##### `public override RestrictionTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)`

---

##### `public override void Write(Utf8JsonWriter writer, RestrictionTarget value, JsonSerializerOptions options)`

---

#### `DiaEditCore.Serialization.Json.UnsupportedSchemaVersionException` (class)

| Field | Type |
|---|---|
| FoundVersion | `int` |
| SupportedVersion | `int` |

---

##### `public UnsupportedSchemaVersionException(int foundVersion, int supportedVersion)`

#### 6.7.2 Validation

#### `DiaEditCore.Serialization.Validation.DisplayNameValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(DisplayName target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.IValidationIssue` (interface)

| Field | Type |
|---|---|
| Message | `string` |
| Severity | `ValidationSeverity` |

---

#### `DiaEditCore.Serialization.Validation.IValidator` (interface)

---

##### `IReadOnlyList<IValidationIssue> Validate(T target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.ProjectSettingsValidator` (class)

ProjectSettings.ValidationRulesの値域を検証する（5.16節）。
スコープ：
- 各int?フィールド（MinDwellTimeSec/MinHeadwaySec/MinTurnaroundSec/
TrackEntryMarginSec/TrackPassMarginSec）は、値がある場合0以上であること
スコープ外：
- フィールド間の相関チェック（例：MinTurnaroundSecとTrackEntryMarginSecの大小関係）は行わない

---

##### `public IReadOnlyList<IValidationIssue> Validate(ProjectSettings target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.SaveValidationRunner` (class)

ProjectFile全体に対して全Validatorを実行し、issueの一覧を返す。
1件でもissueがあれば保存不可（本プロジェクトの運用ではValidationSeverity.Warning＝保存不可相当。
5.13節等参照）とみなす判断は呼び出し側（JsonProjectFileSerializer.Save）が行う。
実装状況・既知のギャップ（v11.38、①ProjectFile設計セッションで判明）：
- DisplayNameValidatorは特定の集約トップレベルコレクションを持たない（DisplayName型を
フィールドとして持つ他オブジェクトの内部で個別に呼ばれる想定）ため、本Runnerでは呼び出さない。
各Validator（StationValidator等）が自身のDisplayNameフィールドについて委譲済みかどうかは
未確認。呼び出し漏れがないか次回確認が必要（§8.2への追加候補）。
- InsertionConfigValidatorはInsertionConfig自体がv11.32でスコープ外・凍結されているため、
対応するコレクションがProjectFileに存在せず、意図的に呼び出さない。
- Train横断検証（Rule 2）はTrainOperationCrossValidator.Runへ委譲する（v11.38で確認済み。
TrainOperationCrossValidator自体が「単一オブジェクトValidatorの契約に収まらない検証」専用の
個別呼び出しランナーとして既に実装されていたため、本Runner側で重複実装しない）。
- TrainOperationsコレクション自体（TrainOperation.OperationNumberの非空チェック等）を
直接検証するValidatorが現時点で存在しない。必要になった時点で追加する。

---

##### `public static IReadOnlyList<IValidationIssue> ValidateAll(ProjectFile project)`

---

#### `DiaEditCore.Serialization.Validation.ValidationContext` (class)

他オブジェクトを跨いだ検証に必要な参照一式。

| Field | Type |
|---|---|
| FloorUnits | `IReadOnlyList<FloorUnit>` (既定値 `Array.Empty<FloorUnit>()`) |
| Stations | `IReadOnlyList<Station>` (既定値 `Array.Empty<Station>()`) |
| Rails | `IReadOnlyList<Rail>` (既定値 `Array.Empty<Rail>()`) |
| EntryPoints | `IReadOnlyList<EntryPoint>` (既定値 `Array.Empty<EntryPoint>()`) |
| BoundaryPoints | `IReadOnlyList<BoundaryPoint>` (既定値 `Array.Empty<BoundaryPoint>()`) |
| Switchers | `IReadOnlyList<Switcher>` (既定値 `Array.Empty<Switcher>()`) |
| BufferStops | `IReadOnlyList<BufferStop>` (既定値 `Array.Empty<BufferStop>()`) |
| Platforms | `IReadOnlyList<Platform>` (既定値 `Array.Empty<Platform>()`) |
| StationPaths | `IReadOnlyList<StationPath>` (既定値 `Array.Empty<StationPath>()`) |
| StationConnectionSegments | `IReadOnlyList<StationConnectionSegment>` (既定値 `Array.Empty<StationConnectionSegment>()`) |
| MainRoutes | `IReadOnlyList<MainRoute>` (既定値 `Array.Empty<MainRoute>()`) |
| StationConnections | `IReadOnlyList<StationConnection>` (既定値 `Array.Empty<StationConnection>()`) |
| ServiceRoutes | `IReadOnlyList<ServiceRoute>` (既定値 `Array.Empty<ServiceRoute>()`) |
| Cars | `IReadOnlyList<Car>` (既定値 `Array.Empty<Car>()`) |
| CarConsists | `IReadOnlyList<CarConsist>` (既定値 `Array.Empty<CarConsist>()`) |
| CarCompositions | `IReadOnlyList<CarComposition>` (既定値 `Array.Empty<CarComposition>()`) |
| VehicleTypes | `IReadOnlyList<VehicleType>` (既定値 `Array.Empty<VehicleType>()`) |
| TrainTypes | `IReadOnlyList<TrainType>` (既定値 `Array.Empty<TrainType>()`) |
| Trains | `IReadOnlyList<Train>` (既定値 `Array.Empty<Train>()`) |
| TimeTableSets | `IReadOnlyList<TimeTableSet>` (既定値 `Array.Empty<TimeTableSet>()`) |
| DiagramRevisions | `IReadOnlyList<DiagramRevision>` (既定値 `Array.Empty<DiagramRevision>()`) |
| TemporaryRestrictions | `IReadOnlyList<TemporaryRestriction>` (既定値 `Array.Empty<TemporaryRestriction>()`) |
| DisplayContexts | `IReadOnlyList<DisplayContext>` (既定値 `Array.Empty<DisplayContext>()`) |
| TrainOperations | `IReadOnlyList<TrainOperation>` (既定値 `Array.Empty<TrainOperation>()`) |

---

#### `DiaEditCore.Serialization.Validation.ValidationIssue` (record)

| Field | Type |
|---|---|
| Message | `string` |
| Severity | `ValidationSeverity` |

---

#### `DiaEditCore.Serialization.Validation.ValidationSeverity` (enum)

| Value | 説明 |
|---|---|
| Warning |  |
| Notice |  |

#### 6.7.3 Validation/Cars

#### `DiaEditCore.Serialization.Validation.Cars.CarCompositionValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(CarComposition target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Cars.CarConsistValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(CarConsist target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Cars.CarValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(Car target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Cars.VehicleTypeValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(VehicleType target, ValidationContext context)`

#### 6.7.4 Validation/Routes

#### `DiaEditCore.Serialization.Validation.Routes.MainRouteValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(MainRoute target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Routes.ServiceRouteValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(ServiceRoute target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Routes.StationConnectionSegmentOverlapCrossValidator` (class)

複線区間（同一MainRoute・同一Direction）内で、同一StationConnectionSegmentが
2つ以上のStationConnectionから参照されていないかを検証する。
v12.19設計セッションで確定した前提：SCは「複々線・双単線区間における経路をユーザーが
分かりやすいようにグルーピングするための用途」であり、複線区間にまで2つ以上のSCを
持たせることは許可しない。この1本のルールだけで以下3ケースが正しく分類される：
- 複々線区間：緩行線・急行線は別の物理Rail（別EntryPoint経由）を通るため、区間ごとに
別のSCSを参照する。同一SCSを共有することはない → 検知されない（正しく許可）。
- 単純な複線区間：物理的な経路が1本のため、その区間のSCSは1個のみ存在しうる。それを
2つのSCが同一方向で参照しようとすれば必ず同一SCSを共有する → 検知される（正しく禁止）。
- 双単線区間：同一SCSを上り方向SCと下り方向SCの両方が参照するが、Directionが異なるため
本ルールの対象外 → 検知されない（正しく許可）。
実装はScsUsedByIndexBuilderと同じ「StationConnection.Segmentsを1回走査するだけ」の
ロジックを踏襲するが、TimeTableSetCacheには依存せずValidationContextの生データから
都度算出する。
TrainOperationCrossValidator／TrainOperationUniquenessValidatorと同じ「単一オブジェクト
Validatorの契約（IValidator&lt;T&gt;）に収まらない検証」向けの静的Runパターンを踏襲する
（ICrossValidatorのようなインターフェースは存在しないため、これらと同じ静的メソッド呼び出し規約に揃える）。
ProjectSettingsへの依存が無いため、他の2つと異なりRunはValidationContextのみを引数に取る。

---

##### `public static IReadOnlyList<IValidationIssue> Run(ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Routes.StationConnectionSegmentValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(StationConnectionSegment target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Routes.StationConnectionValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(StationConnection target, ValidationContext context)`

#### 6.7.5 Validation/Stations

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(FloorUnit target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.StationValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(Station target, ValidationContext context)`

#### 6.7.6 Validation/Stations/FloorUnitObjects

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.BoundaryPointValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(BoundaryPoint target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.BufferStopValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(BufferStop target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.EntryPointValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(EntryPoint target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.NoneEndpointValidator` (class)

BufferStopValidatorと同型。Halt駅制約（4.4.7節）はSwitcher/BoundaryPointのみ対象のため対象外。

---

##### `public IReadOnlyList<IValidationIssue> Validate(NoneEndpoint target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.PlatformValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(Platform target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.RailEndpointCardinalityCrossValidator` (class)

Railの端点オブジェクト（BufferStop/EntryPoint/BoundaryPoint/Switcherの各ポート）が、
物理的に許容される本数を超えて複数のRailから参照されていないかを検証する。
許容数：
BufferStop：1（行き止まり）
EntryPoint：1（駅への入口）
BoundaryPoint：2（線路の連続）
Switcher：ポート単位(SwitcherId, PortIndex)で1（ポート数自体の妥当性はSwitcherValidatorの担当）
RailValidator（単一Rail向け）では他Railとの重複を検知できないため、
StationConnectionSegmentOverlapCrossValidator等と同じ「単一オブジェクトValidatorの契約に
収まらない検証」向けの静的Runパターンを踏襲する（SaveValidationRunner.ValidateAllから呼ぶ）。
NoneEndpointRef（CreateRailCommand直後の未接続状態）は検証対象外。

---

##### `public static IReadOnlyList<IValidationIssue> Run(ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.RailValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(Rail target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.StationPathValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(StationPath target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects.SwitcherValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(Switcher target, ValidationContext context)`

#### 6.7.7 Validation/TimeTable

#### `DiaEditCore.Serialization.Validation.TimeTable.DiagramRevisionValidator` (class)

DiagramRevisionの参照整合性を検証する（5.13節）。
スコープ：
- BaseRevisionId：値ありの場合、参照先DiagramRevisionの実在性
- TimeTableSetIds：各要素が指すTimeTableSetの実在性
- BaseTimeTableSetId：値ありの場合、自身のTimeTableSetIdsに含まれているか（1025行目の保存時検証）
スコープ外（設計書上、保存時検証としては明記されていないためValidatorでは扱わない）：
- TimeTableSetIdsが空の間のBaseTimeTableSetId必須/null要求は、
DiagramRevision作成直後の編集フローにおけるUIレベルの運用ルール（ユーザーへの入力促し）であり、
保存時の構造的検証対象ではない。

---

##### `public IReadOnlyList<IValidationIssue> Validate(DiagramRevision target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.DisplayContextValidator` (class)

DisplayContextの参照整合性・範囲妥当性を検証する（5.15節）。
スコープ：
- MainRouteRangesが空でないこと（表示対象が存在しないDisplayContextを禁止）
- 各MainRouteRange.MainRouteIdが実在するか
- FromIndex/ToIndexが対象MainRoute.StationOrderの範囲内（0以上、StationOrder.Count未満）か
スコープ外：
- FromIndex == ToIndex（単一駅区間）の妥当性：MainRoute側で単一駅路線自体が
構造的に作れない（MainRouteValidator側の制約）ため、本Validatorでは考慮しない。

---

##### `public IReadOnlyList<IValidationIssue> Validate(DisplayContext target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.TemporaryRestrictionValidator` (class)

TemporaryRestrictionの参照整合性・値域を検証する（5.14節）。
スコープ：
- Target（Segment/Rail）が指すStationConnectionSegment/Railの実在性
- DateRange.Start &lt;= DateRange.End
- ExtraRunTimeSec：値ありの場合、0以上（負の追加所要時分は無意味）
- SpeedLimitKph：値ありの場合、正の値（0km/h以下は速度制限として無意味）

---

##### `public IReadOnlyList<IValidationIssue> Validate(TemporaryRestriction target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.TimeTableSetValidator` (class)

TimeTableSet.TrainIdsが参照するTrainの実在性を検証する（5.13節）。

---

##### `public IReadOnlyList<IValidationIssue> Validate(TimeTableSet target, ValidationContext context)`

#### 6.7.8 Validation/TimeTable/Trains

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.BaseTimeTableSetTrainDuplicationCrossValidator` (class)

5.6.1節：RunTimeCalculatorの基準実績選定における一意性制約
（同一選定キー(StationConnectionSegmentId, FromIsStop, ToIsStop, DefaultVehicleTypeId)に
該当するTrainがBaseTimeTableSet内に2件以上存在してはならない）を検証する。
StationConnectionSegmentOverlapCrossValidator／TrainOperationCrossValidatorと同じ
「単一オブジェクトValidatorの契約（IValidator&lt;T&gt;）に収まらない検証」向けの
静的Runパターンを踏襲する。ProjectSettingsへの依存が無いため、
StationConnectionSegmentOverlapCrossValidatorと同様RunはValidationContextのみを引数に取る。
ホップ→StationConnectionSegmentId解決は BaseRunTimeIndexBuilder.ResolveHopSegmentId を
共用する（DRY原則。BaseRunTimeIndexBuilder自体は一意性違反時「後勝ち」で黙って上書きする
防御的実装のため、一意性の強制自体は本Validatorの責務として分離されている）。
対象は DiagramRevision.BaseTimeTableSetId が設定されている全DiagramRevisionそれぞれについて、
独立に検証する（同一TimeTableSetが複数DiagramRevisionからBaseTimeTableSetIdとして参照される
ケースは現行モデル上想定しにくいが、DiagramRevision単位で走査することで自然にカバーされる）。

---

##### `public static IReadOnlyList<IValidationIssue> Run(ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.StationWorkValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(StationWork target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.StopTimeValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(StopTime target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.TrainCrossValidationData` (class)

| Field | Type |
|---|---|
| TrainOperationIndex | `IReadOnlyDictionary<(TrainId TrainId, CarCompositionId CarCompositionId), TrainOperationId>` (既定値 `new Dictionary<(TrainId, CarCompositionId), TrainOperationId>()`) |
| PrevTrainMap | `IReadOnlyDictionary<TrainId, TrainId>` (既定値 `new Dictionary<TrainId, TrainId>()`) |
| TrainOperationsById | `IReadOnlyDictionary<TrainOperationId, TrainOperation>` (既定値 `new Dictionary<TrainOperationId, TrainOperation>()`) |

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.TrainOperationCrossValidator` (class)

Rule 2（5.11.5節）専用の横断検証ランナー。TrainOperationValidatorの3引数版が要求する
TrainCrossValidationData（TrainOperationChainResolver・TrainConnectionResolverの全Train横断出力）を
1回だけ構築し、全Trainに対して適用する。単一オブジェクトValidator（IValidator&lt;T&gt;）の
契約に収まらない検証のための、保存時に個別呼び出しする専用ランナーであり、他Validatorを
束ねる汎用SaveValidationRunnerではない（8.2節項目4はStationConnectionSegmentValidator、
項目5はServiceRouteValidatorへ直接実装済みのため、横断検証として残るのはRule 2のみ）。

---

##### `public static IReadOnlyList<IValidationIssue> Run(ValidationContext context, ProjectSettings settings)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.TrainOperationNonEmptyValidator` (class)

TrainOperation単体で完結する検証（OperationNumberの非空チェックのみ）。
一意性検証（TimeTableSet単位）はTrainOperationUniquenessValidator（横断検証）が別途担う。

---

##### `public IReadOnlyList<IValidationIssue> Validate(TrainOperation target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.TrainOperationUniquenessValidator` (class)

§8.3項目1の方針（operationNumberは同一TimeTableSet内で常に一意）に基づく横断検証。
TrainOperation.OperationNumberの一意性はTimeTableSet単位でのみ意味を持ち、TrainOperation自体は
どのTimeTableSetに属するか正データを持たない（Train.StopTimes[].Works[].TrainOperationId経由の
間接参照）ため、TrainOperationCrossValidatorと同じ「単一オブジェクトValidatorの契約に収まらない
検証」専用の個別呼び出しランナーとして実装する。TimeTableSetCache.TrainOperationIndexは非永続の
導出キャッシュであり保存時検証の入力に使わず、TrainOperationChainResolver.Resolveをその場で
（対象TimeTableSetのTrainのみに限定して）呼び出す。

---

##### `public static IReadOnlyList<IValidationIssue> Run(ValidationContext context, ProjectSettings settings)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.TrainOperationValidator` (class)

Rule 2（改訂）：PrevTrainOperationOverrideの各NewOperationIdは、直前Trainにおける同一
CarCompositionIdの運用（TrainOperationIndex、Composition単位）と異なっていなければならない。
v11.44改訂前はTrain単位のスカラー比較だったが、Composition単位のリスト比較に変更した。

---

##### `public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context)`

---

##### `public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context, TrainCrossValidationData? crossData)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.TrainTypeValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(TrainType target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.TimeTable.Trains.TrainValidator` (class)

---

##### `public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context)`

<!-- DOCGEN:CHAPTER END DiaEditCore -->

---

<!-- DOCGEN:CHAPTER DiaEditApp.ViewModels -->
## 7. DiaEditApp.ViewModels

### 7.1 Composition

#### `DiaEditApp.ViewModels.Composition.ViewModelServiceCollectionExtensions` (class)

DiaEditApp.ViewModels側のDIコンテナ登録（7.3節）。
DiaEditCore.Composition.CoreServiceCollectionExtensions.AddDiaEditCore()の後に呼ぶ想定
（ChangeNotificationBridgeの登録がCommandInvokerに依存するため）。

---

##### `public static IServiceCollection AddDiaEditAppViewModels(this IServiceCollection services)`

### 7.2 Navigation

#### `DiaEditApp.ViewModels.Navigation.NavigationNodeViewModel` (class)

ナビゲーションツリー（UI設計書4.1節）の1ノードを表す軽量ViewModel。
M2-1時点では「駅／駅一覧」のみが実ノード（ContentFactoryを持つリーフ）で、
他カテゴリ（路線／車両／時刻表／お気に入り）は空のフォルダノード
（IsLeaf=false・ContentFactory=null・Children=空）として先行実装する。
ContentFactoryはFunc&lt;IServiceProvider, ViewModelBase&gt;とし、選択時に
MainViewModel側がDIコンテナから解決する（画面ViewModelはTransient登録のため、
ノード選択のたびに新規インスタンスが生成される想定。§7.3 論点L：Dispose規約に従う）。

| Field | Type |
|---|---|
| Header | `string` |
| IsLeaf | `bool` |
| Children | `ObservableCollection<NavigationNodeViewModel>` (既定値 `new()`) |
| ContentFactory | `Func<IServiceProvider, ViewModelBase>?` |
| IsExpanded | `bool` (既定値 `true`) |

---

##### `public static NavigationNodeViewModel Folder(string header, params NavigationNodeViewModel[] children)`

フォルダノード（子を持つが自身はコンテンツを持たない）を作る。

---

##### `public static NavigationNodeViewModel Leaf(string header, Func<IServiceProvider, ViewModelBase> contentFactory)`

リーフノード（クリックでメインワークスペースにコンテンツを表示する）を作る。

### 7.3 Stations

#### `DiaEditApp.ViewModels.Stations.CanvasMode` (enum)

UI設計書§4.2.3のキャンバスモード（v13.13確定の3モード制）。
StationPathEditは今回スコープ外のためボタン配置のみ（§4.4.2-23）。

| Value | 説明 |
|---|---|
| View |  |
| TrackEndpointPlatformEdit |  |
| StationPathEdit |  |

---

#### `DiaEditApp.ViewModels.Stations.CanvasPoint` (record struct)

キャンバス表示専用の座標型（double、View非依存）。
モデルのPoint（int、モデル空間の生座標）とは区別する。ReloadCanvasShapesで
バウンディングボックスに基づきスケール変換された後の「画面表示用座標」を保持する。
DiaEditApp.ViewModelsプロジェクトはAvalonia非依存方針のため、Avalonia.Pointを
直接使わずこの型を経由する（FloorUnitCanvasConverters誤配置の教訓、v13.13セッション）。

| Field | Type |
|---|---|
| X | `double` |
| Y | `double` |

---

#### `DiaEditApp.ViewModels.Stations.EndpointCanvasShape` (record)

キャンバス上に描画する端点1件分の座標・種別情報（表示専用DTO）。

| Field | Type |
|---|---|
| ObjectId | `ObjectId` |
| Position | `CanvasPoint` |
| Kind | `EndpointVisualKind` |
| IsSelected | `bool` |

---

#### `DiaEditApp.ViewModels.Stations.EndpointVisualKind` (enum)

端点の視覚表現種別（NoneEndpoint/BoundaryPoint/EntryPoint/BufferStop/Switcherの5種、形状で区別）。

| Value | 説明 |
|---|---|
| None |  |
| BoundaryPoint |  |
| EntryPoint |  |
| BufferStop |  |
| Switcher |  |

---

#### `DiaEditApp.ViewModels.Stations.FloorUnitDetailViewModel` (class)

UI設計書§4.2.3「構内配線図ポップアップ」のキャンバス実装（ステージ1：描画・モード切替・選択のみ）。

| Field | Type | 説明 |
|---|---|---|
| RailRoles | `IReadOnlyList<RailRole>` (既定値 `Enum.GetValues<RailRole>()`) |  |
| EndpointKinds | `IReadOnlyList<RailEndpointKind>` (既定値 `Enum.GetValues<RailEndpointKind>()`) |  |
| EntryPointTypes | `IReadOnlyList<EntryPointType>` (既定値 `Enum.GetValues<EntryPointType>()`) |  |
| CanvasModes | `IReadOnlyList<CanvasMode>` (既定値 `Enum.GetValues<CanvasMode>()`) |  |
| FloorUnitName | `string` |  |
| Rails | `ObservableCollection<Rail>` (既定値 `new()`) |  |
| CanvasRails | `ObservableCollection<RailCanvasShape>` (既定値 `new()`) |  |
| CanvasEndpoints | `ObservableCollection<EndpointCanvasShape>` (既定値 `new()`) |  |
| CanvasContentWidth | `double` (既定値 `CanvasMargin * 2`) | 内側Canvas（XAML）のWidth/Heightに束縛する、スケール変換後の実サイズ（画面ピクセル単位）。 |
| CanvasContentHeight | `double` (既定値 `CanvasMargin * 2`) |  |
| Mode | `CanvasMode` (既定値 `CanvasMode.View`) |  |
| IsEditableMode | `bool` | 閲覧モードではRail・端点とも選択のみ可（属性パネルは参照専用にする想定、
パネル自体のReadOnly化はステージ2でドラッグ編集導入時にあわせて対応）。
線路・端点・ホーム編集モードでのみ新規作成・削除・属性変更を許可する。 |
| EditFloorUnitName | `string` (既定値 `""`) |  |
| IsFloorUnitNameDirty | `bool` |  |
| Summary | `FloorUnitSummary` (既定値 `null!`) |  |
| SelectedRail | `Rail?` |  |
| NewRailName | `string` (既定値 `""`) |  |
| NewRailLengthM | `double` |  |
| NewRailSpeedLimitKph | `double` |  |
| NewRailRole | `RailRole` (既定値 `RailRole.Normal`) |  |
| NewEndpointAKind | `RailEndpointKind` (既定値 `RailEndpointKind.None`) |  |
| NewEndpointAX | `int` |  |
| NewEndpointAY | `int` |  |
| NewEndpointAEntryType | `EntryPointType` (既定値 `EntryPointType.Both`) |  |
| NewEndpointBKind | `RailEndpointKind` (既定値 `RailEndpointKind.None`) |  |
| NewEndpointBX | `int` |  |
| NewEndpointBY | `int` |  |
| NewEndpointBEntryType | `EntryPointType` (既定値 `EntryPointType.Both`) |  |
| EditName | `string` (既定値 `""`) |  |
| EditLengthM | `double` |  |
| EditSpeedLimitKph | `double` |  |
| EditRole | `RailRole` (既定値 `RailRole.Normal`) |  |
| IsDirty | `bool` | 選択中Railが無い間はIsDirty=falseとし、「決定」ボタンを無効化する。 |
| DeleteRailError | `string?` |  |
| HasDeleteRailError | `bool` |  |
| ObservedIds | `IReadOnlySet<ObjectId>` | StationDetailViewModel.ObservedIdsと同じ設計。FloorUnit自身に加え、現在このFloorUnitに
属する（端点経由で導出される）Rail群のIdを都度算出する。 |

> FloorUnit詳細画面＝Rail（線路）管理画面と位置づける（Tao様確認済み、v13.7セッション）。
> FloorUnit自身のName編集は本画面の責務外（StationDetailViewModel側で行う）。
> §9.2項目29のテンプレート（StationDetailViewModelで確立したBuildEditedSnapshot／
> CaptureCurrentSnapshot／IsDirty／差分判定Saveパターン）を、Rail属性編集（選択中Rail）へ適用する。
> Railは自身のFloorUnitIdを持たない（§4.4.3、EndpointA/Bの接続先端点オブジェクト経由で導出される
> 派生関係）ため、「このFloorUnitに属するRail」は都度Rails全体をフィルタして導出する
> （専用逆引きIndexは今回新設しない。DeleteRailCommandの3経路チェックと同じ判断基準：
> 消費者がこのViewModelのみで件数規模も小さいため線形走査で足りる）。
> ObservedIdsはStationDetailViewModelと同じ設計：_session.Current側の生きたコレクションを
> 都度再評価する。Create系コマンドはComputeAffectedIdsAfterApplyで新規オブジェクト自身の
> ObjectIdのみをAffectedIdsとするが、Apply()は既にNotifyより前に完了しているため、
> ObservedIdsの再評価時点では新規Rail（＋アタッチ済みの端点）が既にセッション側の
> コレクションに反映済みであり、結果的に自動的に拾える（親IDを明示的に含める工夫は不要）。
> キャンバス（ステージ1）：ドラッグ操作（端点移動・新規Rail作成・範囲選択）はステージ2で対応する。
> 現段階ではCanvasRails/CanvasEndpointsは表示専用（読み取り）であり、実際の新規作成・属性変更・
> 削除は既存のフォーム入力系コマンド（AddRail／SaveSelectedRail／DeleteSelectedRail）を通じて行う。

---

##### `public FloorUnitDetailViewModel(FloorUnit floorUnit, ProjectSession session, CommandInvoker invoker, ChangeNotificationBridge bridge, Action goBack)`

---

##### `public void SelectRailFromCanvas(Rail rail)`

キャンバス上でRailの図形がクリックされた際、Viewのコードビハインドから呼ばれる。

---

##### `public void Dispose()`

---

#### `DiaEditApp.ViewModels.Stations.RailCanvasShape` (record)

キャンバス上に描画するRail 1本分の座標情報（表示専用DTO）。

| Field | Type |
|---|---|
| Rail | `Rail` |
| A | `CanvasPoint` |
| B | `CanvasPoint` |
| IsSelected | `bool` |

> Model層のRailを直接バインドせず、解決済み座標を都度計算して持たせることで
> XAML側からPoint解決ロジック（ResolvePosition呼び出し）を隠蔽する。

---

#### `DiaEditApp.ViewModels.Stations.RailEndpointKind` (enum)

Rail新規作成UI向けの端点種別選択肢。Switcherは§7.1確定仕様により別導線のためここに含めない。

| Value | 説明 |
|---|---|
| None |  |
| BoundaryPoint |  |
| EntryPoint |  |
| BufferStop |  |

---

#### `DiaEditApp.ViewModels.Stations.StationDetailViewModel` (class)

| Field | Type | 説明 |
|---|---|---|
| StationTypes | `IReadOnlyList<StationType>` (既定値 `Enum.GetValues<StationType>()`) |  |
| FloorUnits | `ObservableCollection<FloorUnit>` (既定値 `new()`) |  |
| DisplayNameText | `string` (既定値 `""`) |  |
| Type | `StationType` |  |
| OperatingCode | `string` (既定値 `""`) |  |
| TelegraphCode | `string` (既定値 `""`) |  |
| ShowsInStationTimetableOverride | `bool?` |  |
| IsDirty | `bool` | §9.2項目31：現在の入力値がStationの現状値と一致しているかどうか。
Save()側の差分判定（BuildEditedSnapshotとCaptureCurrentSnapshotの比較）と
同じ比較規約を使う（DisplayNameのIEquatable実装、§9.2項目31関連対応）。 |
| ResolvedShowsInStationTimetable | `bool` |  |
| SelectedFloorUnit | `FloorUnit?` |  |
| DeleteFloorUnitError | `string?` |  |
| HasDeleteFloorUnitError | `bool` |  |
| ObservedIds | `IReadOnlySet<ObjectId>` | M2-4：ChangeNotificationBridge向けの監視対象。Station自身に加え、現在この駅に属する
全FloorUnitのIdを含める。固定集合ではなく都度算出するプロパティとすることで、
FloorUnit追加直後（Execute完了後・Notify呼び出し前）の新規Idも自動的に拾える
（ChangeNotificationBridge.OnChangedはOverlaps判定のたびにこのgetterを再評価するため）。 |

---

##### `public StationDetailViewModel(Station station, ProjectSession session, CommandInvoker invoker, ChangeNotificationBridge bridge, Action goBack)`

---

##### `public void Dispose()`

---

#### `DiaEditApp.ViewModels.Stations.StationListViewModel` (class)

M2-2：駅一覧（マスター）画面のViewModel。UI設計書4.2.1節の列構成
（駅名／種別／事業者管理番号／電報略号）でテーブル表示する。
データ取得・更新方針（DI登録方式の検討結果、案C採用）：ProjectSession／CommandInvokerを
そのままコンストラクタ注入する。Stationsは表示専用のObservableCollectionへコピーし、
CommandInvokerへICacheChangeObserverとして直接購読することで同期する
（M2-4でChangeNotificationBridge経由の配線に置き換えるまでの暫定対応。MainViewModelの
Undo/Redoボタンなど、本VM外から発生した変更もOnChanged経由で拾えるようにするため、
自分がExecuteしたときだけReload()する方式では不十分だったことがM2-5動作確認で判明した）。

| Field | Type |
|---|---|
| Stations | `ObservableCollection<Station>` (既定値 `new()`) |
| SelectedStation | `Station?` |

---

##### `public StationListViewModel(ProjectSession session, CommandInvoker invoker)`

---

##### `public void Reload()`

ProjectSession.Current.Stationsの内容でStationsを再同期する。

---

##### `public void Dispose()`

<!-- DOCGEN:CHAPTER END DiaEditApp.ViewModels -->

---


<!-- DOCGEN:CHAPTER DiaEditApp -->
## 8. DiaEditApp

### 8.1 Services

#### `DiaEditApp.Services.AppSettings` (class)

アプリ全体の設定（現時点では直近使用プロジェクトファイルパスのみ）。
ProjectFile本体（DiaEditCore側、1プロジェクト1JSON）とは別に、
%AppData%\DiaEdit\settings.json へ保存する軽量な設定ファイル。
Avalonia依存を持たない単純なPOCO＋staticな読み書きヘルパーとして
DiaEditApp.Services（Avalonia依存があってよい層、7.3節）に置く。

| Field | Type |
|---|---|
| LastProjectFilePath | `string?` |

---

##### `public static AppSettings Load()`

設定ファイルを読み込む。存在しない・破損している場合は例外を送出せず、
LastProjectFilePath=nullの既定値を返す（起動シーケンスを止めないため）。

---

##### `public void Save()`

---

#### `DiaEditApp.Services.AppSettingsService` (class)

AppSettings（読み書きの実体、静的Load/Saveを持つPOCO）をIAppSettingsServiceとして
DIコンテナへ公開するアダプタ。App.axaml.cs起動時にAppSettings.Load()で読み込んだ
1つのインスタンスをそのままラップすることで、MainViewModel側の変更がAppSettings本体へ
反映される（同一インスタンス共有）。

| Field | Type |
|---|---|
| LastProjectFilePath | `string?` |

---

##### `public AppSettingsService(AppSettings settings)`

---

##### `public void Save()`

---

#### `DiaEditApp.Services.FileDialogService` (class)

---

##### `public async Task<string?> PickSaveProjectFileAsync(string? suggestedFileName)`

---

#### `DiaEditApp.Services.ServiceCollectionExtensions` (class)

---

##### `public static IServiceCollection AddDiaEditAppServices(this IServiceCollection services)`

### 8.2 Views

#### `DiaEditApp.Views.MainWindow` (class)

---

##### `public MainWindow()`

#### 8.2.1 Stations

#### `DiaEditApp.Views.Stations.FloorUnitCanvasConverters` (class)

FloorUnitDetailView（キャンバス、§4.2.3・§4.4.2-23ステージ1）専用の値コンバータ群。

---

#### `DiaEditApp.Views.Stations.FloorUnitDetailView` (class)

---

##### `public FloorUnitDetailView()`

---

#### `DiaEditApp.Views.Stations.StationDetailView` (class)

---

##### `public StationDetailView()`

---

#### `DiaEditApp.Views.Stations.StationListView` (class)

---

##### `public StationListView()`

<!-- DOCGEN:CHAPTER END DiaEditApp -->

---

## 9. 変更履歴
