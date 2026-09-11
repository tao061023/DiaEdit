<!-- DOCGEN:CHAPTER DiaEditCore -->
## 6. DiaEditCore

### 6.1 Model

**ID型一覧**：`BoundaryPointId`, `BoundaryPointObjectId`, `BufferStopId`, `BufferStopObjectId`, `CarCompositionId`, `CarCompositionObjectId`, `CarConsistId`, `CarConsistObjectId`, `CarId`, `CarObjectId`, `DiagramRevisionId`, `DiagramRevisionObjectId`, `DisplayContextId`, `DisplayContextObjectId`, `EntryPointId`, `EntryPointObjectId`, `FloorUnitId`, `FloorUnitObjectId`, `MainRouteId`, `MainRouteObjectId`, `NoneEndpointId`, `NoneEndpointObjectId`, `PlatformId`, `PlatformObjectId`, `RailId`, `RailObjectId`, `ServiceRouteId`, `ServiceRouteObjectId`, `StationConnectionId`, `StationConnectionObjectId`, `StationConnectionSegmentId`, `StationConnectionSegmentObjectId`, `StationId`, `StationObjectId`, `StationPathId`, `StationPathObjectId`, `SwitcherId`, `SwitcherObjectId`, `TemporaryRestrictionId`, `TemporaryRestrictionObjectId`, `TimeTableSetId`, `TimeTableSetObjectId`, `TrainId`, `TrainObjectId`, `TrainOperationId`, `TrainTypeId`, `TrainTypeObjectId`, `VehicleTypeId`, `VehicleTypeObjectId`, `VirtualConflictObjectId`, `VirtualConflictObjectIdObject`

---

#### `DiaEditCore.Model.DisplayName` (class)

多言語対応、および略称を保持するための共通型

| Field | Type | 説明 |
|---|---|---|
| Name | `string` | 正式名称 |
| Abbreviation | `string?` | 略称 |
| Translations | `Dictionary<string, string>` (既定値 `new()`) | BCP47言語コード＋訳語 |

---

##### `public string Resolve(string localeCode)`

localCodeに紐づくNameまたはAbbreviationを引き当てる

**Parameters**

- `localeCode`: 言語コード

**Returns**
localeCodeに紐づく値がある場合：v
localeCodeに紐づく値がない場合：Name

---

##### `public DisplayName Clone()`

Name/Abbreviation/Translationsをディープコピーした新しいDisplayNameを返す。

**Remarks**
DisplayNameは参照型（class）かつTranslationsがミュータブルなDictionaryのため、
スナップショット保持（UndoableCommand等）で外部参照を残さないために使う。

---

##### `public bool Equals(DisplayName? other)`

値等価の実装。参照型であるDisplayNameをスナップショット比較に対応させる。

---

##### `public override bool Equals(object? obj)`

---

##### `public override int GetHashCode()`

---

#### `DiaEditCore.Model.IIntId` (interface)

int一つだけを値として持つID型に実装させる共通インターフェース。

| Field | Type |
|---|---|
| Value | `int` |

> JSONシリアライズ時、ネストしたオブジェクトではなく素朴なintとして書き出すための
> IntIdJsonConverterFactory（Serialization層）が、リフレクションを使わずこのインターフェース
> 経由でValueを読み書きするために使う。

---

#### `DiaEditCore.Model.ObjectId` (record)

ObjectIdは、ID型をラップして、オブジェクトの種類を区別するための型。

---

#### `DiaEditCore.Model.Point` (record struct)

座標点を保持する共通基底型

| Field | Type |
|---|---|
| X | `int` |
| Y | `int` |

> intである理由は座標をグリッドで管理することが共通事項であるため。

---

#### `DiaEditCore.Model.ProjectFile` (class)

1プロジェクト1JSON方針における保存ファイルのルート集約オブジェクト。

| Field | Type | 説明 |
|---|---|---|
| SchemaVersion | `int` (既定値 `1`) | 保存形式のスキーマバージョン。<br>読込時にJsonProjectFileSerializerが未対応バージョンを検知した場合は例外を送出する。 |
| ProjectSettings | `ProjectSettings` | プロジェクト設定 |
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

プロジェクト設定。
1プロジェクト1JSON方針における保存ファイルのルート集約オブジェクトProjectFileの一部として保持される。

| Field | Type |
|---|---|
| ValidationRules | `ValidationRules` |
| DiagramBasedTimeSec | `int` |

---

#### `DiaEditCore.Model.TimeOfDaySec` (record struct)

時刻の内部表現。当日0:00を基準とした経過秒数（int）として保持する。

| Field | Type |
|---|---|
| Seconds | `int` |

---

##### `public int CompareTo(TimeOfDaySec other)`

---

#### `DiaEditCore.Model.TimeTableSetCache` (class)

時刻表セットに関するキャッシュを保持する。

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

組成単位を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `CarId` | 組成単位識別子 |
| CarType | `string` | 車両種別<br><br>**制約/備考：** "クハE234" など。表記のマスタ管理はUI側の責務。 |
| Placeholder | `int` (既定値 `0`) | 編成番号で置換する数字。Placeholder+編成番号で表現。<br><br>**制約/備考：** 1000番台などの1000を設定すればよい。 |
| IsPower | `bool` | 動力車フラグ |
| LengthM | `double` | 組成単位ごとの実長 |

---

#### `DiaEditCore.Model.Cars.CarComposition` (class)

編成を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `CarCompositionId` | 編成識別子 |
| Name | `string` | 編成名<br><br>**制約/備考：** 例: "トウ01" |
| Identifier | `int` | 編成番号<br><br>**制約/備考：** 車番表記に利用 |
| CarConsistId | `CarConsistId` | 利用する組成済み編成の識別子 |

---

#### `DiaEditCore.Model.Cars.CarConsist` (class)

組成済み編成を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `CarConsistId` | 組成済み編成識別子 |
| VehicleTypeId | `VehicleTypeId` | 車両形式の識別子 |
| Type | `CarConsistType` | 組成タイプ |
| Cars | `List<CarRef>` | 編成の組成配列 |

---

#### `DiaEditCore.Model.Cars.CarConsistType` (enum)

組成タイプ

> Basic；基本編成
> Attached：付属編成

| Value | 説明 |
|---|---|
| Basic |  |
| Attached |  |

---

#### `DiaEditCore.Model.Cars.CarRef` (class)

組成単位参照型

| Field | Type | 説明 |
|---|---|---|
| CarId | `CarId` | 組成単位識別子 |
| Position | `int` | 組成位置 |

---

#### `DiaEditCore.Model.Cars.VehicleType` (class)

車両形式を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `VehicleTypeId` | 車両形式識別子 |
| Name | `string` | 車両形式名 |
| MaxSpeedKph | `double` | 設計最高速度 |

#### 6.1.2 Routes

#### `DiaEditCore.Model.Routes.MainRoute` (class)

分岐を持たない1本の駅順経路

| Field | Type | 説明 |
|---|---|---|
| Id | `MainRouteId` | 路線識別子 |
| Name | `DisplayName` | 路線名称<br><br>**制約/備考：** 全MainRouteで一意 |
| StationOrder | `List<StationId>` | 駅の順序付き配列（分岐なし）<br><br>**制約/備考：** StationOrder[0]とStationOrder[last]はStationType != Halt |
| IsLoop | `bool` (既定値 `false`) | 環状線フラグ |
| DirectionReversalStations | `List<StationId>` (既定値 `new()`) | 進行方向が変わる（スイッチバックが発生する）駅リスト |
| StationDisplayNameOverrides | `Dictionary<StationId, DisplayName>` (既定値 `new()`) | 路線固有の駅名を持つ場合の駅名保持フィールド<br><br>**制約/備考：** 未設定ならStation.DisplayNameにフォールバック |

> 現実での、「路線」が該当する。（UI上では路線と表示する）

---

#### `DiaEditCore.Model.Routes.ServiceRoute` (class)

運転系統を表す

| Field | Type | 説明 |
|---|---|---|
| Id | `ServiceRouteId` | 運転系統識別子 |
| Name | `DisplayName` | 運転系統名 |
| IsAutoGenerated | `bool` | 自動生成フラグ<br><br>**制約/備考：** 1 MainRouteにつき 1 ServiceRouteが自動で生成される。これは削除不可。 |
| Segments | `List<ServiceRouteSegment>` | 運転系統を構成する路線と区間情報 |

> 福北ゆたか線（筑豊本線、篠栗線、鹿児島本線）やJR神戸線・JR京都線・JR琵琶湖線（東海道本線）など、愛称や直通系統を表現する。

---

#### `DiaEditCore.Model.Routes.ServiceRouteSegment` (class)

運転系統を構成する路線の区間

| Field | Type | 説明 |
|---|---|---|
| MainRouteId | `MainRouteId` | 対象の路線の識別子 |
| FromStationIndex | `int` | 路線の区間の始点<br><br>**制約/備考：** IdではなくIndex参照 |
| ToStationIndex | `int` | 路線の区間の終点<br><br>**制約/備考：** IdではなくIndex参照。<br>FromStationIndexより小さな値も可能。<br>（例：特急ソニックの運行区間<br>[博多→小倉]：上り<br>[小倉→大分]：下りであるが、<br>便宜上、[博多→大分]：下りとして扱っている。） |
| IsUnidirectional | `bool` (既定値 `false`) | 片方向専用区間フラグ<br><br>**制約/備考：** 経由する路線が非対称であったり、片方向専用の連絡線を経由する運転系統を表現する。<br>対称な路線を設定する場合は、下りを基準にすること。 |
| PairedMainRouteId | `MainRouteId?` | IsUnidirectional == true の場合に指定する、対となる路線の識別子 |
| PairedFromStationIndex | `int?` | 対となる路線の区間の始点 |
| PairedToStationIndex | `int?` | 対となる路線の区間の終点 |
| ReversesAtBoundary | `bool` (既定値 `false`) | 路線の区間の終点で次の路線に移る際にスイッチバックが発生するか |
| SelectedStationConnectionId | `StationConnectionId?` | 複々線等でStationConnection候補が複数存在する区間のみ必須（候補1件なら省略可、自動選択） |
| PairedSelectedStationConnectionId | `StationConnectionId?` | 対となる路線で、複々線等でStationConnection候補が複数存在する区間のみ必須（候補1件なら省略可、自動選択） |

---

#### `DiaEditCore.Model.Routes.ServiceRouteSegmentExtensions` (class)

---

##### `public static bool IsPaired(this ServiceRouteSegment segment)`

---

#### `DiaEditCore.Model.Routes.StationConnection` (class)

走行経路を表す

| Field | Type | 説明 |
|---|---|---|
| Id | `StationConnectionId` | 走行経路識別子 |
| Name | `string` (既定値 `""`) | 走行経路名称<br><br>**制約/備考：** 同一MainRouteで一意 |
| MainRouteId | `MainRouteId` | 所属する路線の識別子 |
| Direction | `StationConnectionDirection` | 走行経路の向き |
| Segments | `List<StationConnectionSegmentId>` | 経路を構成する駅間物理区間のリスト<br><br>**制約/備考：** 実体参照。同一SCSIdを複数SCが共有しうる |

---

#### `DiaEditCore.Model.Routes.StationConnectionDirection` (enum)

走行経路の向き

> Up上り方向
> Down下り方向

| Value | 説明 |
|---|---|
| Up |  |
| Down |  |

---

#### `DiaEditCore.Model.Routes.StationConnectionSegment` (class)

駅と駅を接続する最小単位

| Field | Type | 説明 |
|---|---|---|
| Id | `StationConnectionSegmentId` | 駅間物理区間識別子 |
| StationIdA | `StationId` | 区間を形成する駅A |
| StationIdB | `StationId` | 区間を形成する駅B |
| EntryPointIdA | `EntryPointId` | 駅Aの駅境界点識別子 |
| EntryPointIdB | `EntryPointId` | 駅Bの駅境界点識別子 |
| MainRouteId | `MainRouteId` | 所属する路線の識別子 |
| LengthM | `double` | 区間長<br><br>**制約/備考：** 単位は [ km ] |
| SpeedLimitKph | `double` | 区間の営業最高速度 |

> 線路単位で管理する

#### 6.1.3 Stations

#### `DiaEditCore.Model.Stations.FloorUnit` (class)

駅階層を表現する。

| Field | Type | 説明 |
|---|---|---|
| Id | `FloorUnitId` | 駅階層識別子 |
| StationId | `StationId` | 駅階層の所属する駅の識別子 |
| Name | `string` (既定値 `""`) | 駅階層名称<br><br>**制約/備考：** 現況：空文字列許容。自動採番は行わない。<br>修正案：空文字列禁止。同一Station内で一意。 |
| DisplayOrder | `int` | 駅詳細画面における表示順<br><br>**制約/備考：** 同一StationId内で一意（保存時検証） |

---

#### `DiaEditCore.Model.Stations.Station` (class)

駅や信号場、車両基地を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `StationId` | 駅識別子 |
| DisplayName | `DisplayName` | 駅名称<br><br>**制約/備考：** 全ての駅で一意 |
| Type | `StationType` | 駅種別 |
| OperatingCode | `string` (既定値 `""`) | 事業者管理用コード<br><br>**制約/備考：** 非空文字列同士のみ全Station間で一意 |
| TelegraphCode | `string` (既定値 `""`) | 電報略号<br><br>**制約/備考：** 非空文字列同士のみ全Station間で一意 |
| ShowsInStationTimetableOverride | `bool?` | 駅時刻表の対象判別用フラグ |

> 制約：Stationを参照する`FloorUnit`が1件以上存在

---

##### `public bool ResolveShowsInStationTimetable()`

駅時刻表の対象判別用フラグをデフォルトに切り替えるメソッド

**Returns**
Standard, HaltならTrue、SignalStation, DepotならFalse

---

#### `DiaEditCore.Model.Stations.StationType` (enum)

駅種別

> Standard：停車場。在線検知の境界となる。
> Halt：停留場。在線検知の境界とならない。
> SignalStation：信号場。在線検知の境界となる。単なる路線分岐点やスイッチバック施設など、客扱いを行わない運行拠点が該当。
> Depot：車両基地。在線検知の境界となる。駅から車両基地までの間は一つの路線として登録する。
> Halt駅にはSwitcher・BoundaryPointの配置を許可しない

| Value | 説明 |
|---|---|
| Standard |  |
| Halt |  |
| SignalStation |  |
| Depot |  |

#### 6.1.4 Stations/FloorUnitObjects

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BoundaryPoint` (class)

閉塞境界点を表す

| Field | Type | 説明 |
|---|---|---|
| Id | `BoundaryPointId` | 閉塞境界点識別子 |
| Base | `FloorUnitObjectBase` | 駅階層識別子と座標情報を保持する複合フィールド |
| Name | `string` (既定値 `""`) | 閉塞境界点の名称 |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BoundaryPointEndpointRef` (record)

レール端点の「閉塞境界点」を参照する型

| Field | Type |
|---|---|
| Id | `BoundaryPointId` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BoundaryPointWaypoint` (record)

レール端点の「閉塞境界点」を参照する型

| Field | Type |
|---|---|
| Id | `BoundaryPointId` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BufferStop` (class)

車止めを表す

| Field | Type | 説明 |
|---|---|---|
| Id | `BufferStopId` | 車止め識別子 |
| Base | `FloorUnitObjectBase` | 駅階層識別子と座標情報を保持する複合フィールド |
| Name | `string` (既定値 `""`) | 車止めの名称 |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BufferStopEndpointRef` (record)

レール端点の「車止め」を参照する型

| Field | Type |
|---|---|
| Id | `BufferStopId` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.BufferStopWaypoint` (record)

レール端点の「車止め」を参照する型

| Field | Type |
|---|---|
| Id | `BufferStopId` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.EntryPoint` (class)

駅境界点を表す

| Field | Type | 説明 |
|---|---|---|
| Id | `EntryPointId` | 駅境界点識別子 |
| Base | `FloorUnitObjectBase` | 駅階層識別子と座標情報を保持する複合フィールド |
| Name | `string` (既定値 `""`) | 駅境界点の名称 |
| Type | `EntryPointType` | 駅境界点種別 |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.EntryPointEndpointRef` (record)

レール端点の「駅境界点」を参照する型

| Field | Type |
|---|---|
| Id | `EntryPointId` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.EntryPointType` (enum)

駅境界点種別

> Arrival：進入専用
> Departure：進出専用
> Both：双方向対応。単線や双単線、三複線などに用いる。

| Value | 説明 |
|---|---|
| Arrival |  |
| Departure |  |
| Both |  |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.EntryPointWaypoint` (record)

レール端点の「駅境界点」を参照する型

| Field | Type |
|---|---|
| Id | `EntryPointId` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.FloorObjectRefExtensions` (class)

---

##### `public static ObjectId? ToObjectId(this RailEndpointRef r)`

RailEndpointRef を対応する ObjectId に変換する。

**Parameters**

- `r`: 変換対象となる RailEndpointRef。

**Returns**
対応する ObjectId。未知型の場合は例外、null の場合は null。

**Remarks**
RailEndpointRef は abstract かつ非 sealed のため、既知の派生型のみを変換対象とする。
未知の派生型が渡された場合は例外を送出する。
null は変換不能として null を返す。

---

##### `public static ObjectId ToObjectId(this StationPathWaypoint w)`

StationPathWaypoint を対応する ObjectId に変換する。

**Parameters**

- `w`: 変換対象となる StationPathWaypoint。

**Returns**
対応する ObjectId。null の場合は例外。

**Remarks**
StationPathWaypoint も abstract・非 sealed のため、既知の派生型のみを変換対象とする。
null は許容されず、必ず例外を送出する。
未知の派生型が渡された場合も例外を送出する。

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.FloorUnitObjectBase` (class)

駅階層を識別する FloorUnitId と、平面上の座標を保持する基本オブジェクト。

| Field | Type | 説明 |
|---|---|---|
| FloorUnitId | `FloorUnitId` | 駅階層を識別する ID。 |
| Position | `Point` | 駅階層内での座標 (X, Y)。 |

> 本クラスは駅構内オブジェクトの基底情報として利用される。
> FloorUnitId と Position は必須であり、null を許容しない。

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.NoneEndpoint` (class)

Rail作成時における端点オブジェクトの種別決定前の仮種別

| Field | Type | 説明 |
|---|---|---|
| Id | `NoneEndpointId` | 仮種別識別子 |
| Base | `FloorUnitObjectBase` | 駅階層識別子と座標情報を保持する複合フィールド |
| Name | `string` (既定値 `""`) | 仮種別の名称 |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.NoneEndpointRef` (record)

レール端点の「仮種別」を参照する型

| Field | Type |
|---|---|
| Id | `NoneEndpointId` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.Platform` (class)

駅のプラットホームを表す

| Field | Type | 説明 |
|---|---|---|
| Id | `PlatformId` | プラットホーム識別子 |
| Base | `FloorUnitObjectBase` | 駅階層識別子と座標情報を保持する複合フィールド。ドラッグ始端。 |
| SecondaryPosition | `Point` | ドラッグ終端。<br><br>**制約/備考：** BaseのPositionとSecondaryPositionを対角線とする矩形でホームを表現する。 |
| Name | `string` (既定値 `""`) | ホームの名称 |
| FacingRailIds | `List<RailId>` (既定値 `new()`) | 当ホームで客扱いを行う番線リスト<br><br>**制約/備考：** 多対多（櫛型ホーム対応のため） |
| EffectiveLength | `double?` | ホーム有効長<br><br>**制約/備考：** 未設定の場合、FacingRailIdsのLengthMの最小値にフォールバックする |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.PortPair` (record struct)

構成可能な進路をPort対で表現する

| Field | Type |
|---|---|
| PortA | `int` |
| PortB | `int` |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.Rail` (class)

レールを表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `RailId` | レール識別子 |
| Name | `string` (既定値 `""`) | レール名称<br><br>**制約/備考：** RailRole==Trackの場合、非空文字列かつ一意（保存時検証）<br>RailRole!=Trackの場合、非空文字列は一意 |
| LengthM | `double` | レール長<br><br>**制約/備考：** 自動算出は行わない |
| SpeedLimitKph | `double` | 制限速度 |
| Role | `RailRole` | レール種別 |
| EndpointA | `RailEndpointRef` | レール端点の参照先A |
| EndpointB | `RailEndpointRef` | レール端点の参照先B |
| ControlPoints | `List<RailControlPoint>` (既定値 `new()`) | Rail描画用の中間制御点 |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.RailControlPoint` (class)

Rail描画用の中間制御点。

| Field | Type | 説明 |
|---|---|---|
| Point | `Point` | 制御点座標 |

> 現時点では意味のないフィールド。将来的にRail端点に依存せずに折れ線や曲線を表現するために仮導入している。

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.RailEndpointRef` (record)

Rail端点を参照するための抽象基底型

> 非 sealed のため派生型が増える可能性がある。
> 利用側では型の網羅性を保証できない点に注意すること。

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.RailRole` (enum)

レール種別

> Normal：本線など
> Track：番線。客扱いの有無にかかわらず、（オブジェクトとしての）駅を走行する中継地点として登録しなければならない。
> Shunting：引き上げ線、留置線。入替作業を行うための場所として登録する。

| Value | 説明 |
|---|---|
| Normal |  |
| Track |  |
| Shunting |  |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.StationPath` (class)

構内進路を表す

| Field | Type | 説明 |
|---|---|---|
| Id | `StationPathId` | 構内進路識別子 |
| FloorUnitId | `FloorUnitId` | 所属する駅階層の識別子 |
| Name | `string` | 構内進路の名称<br><br>**制約/備考：** 同一FloorUnitId内で一意 |
| Direction | `StationPathDirection` | 構内進路の方向種別 |
| Waypoints | `List<StationPathWaypoint>` | 構内進路を構成するRail端点配列<br><br>**制約/備考：** 制約：<br>WayPointsは最低1件（Halt駅単一EPパターンのみ1件、他は通常2件以上）<br>Waypoints[0]はEntryPoint/BoundaryPointのいずれか<br>Waypoints[last]も同様<br>中間要素はSwitcher/BoundaryPointのいずれか<br>隣接Waypoint間を直接結ぶRailが存在すること<br>同一参照先が2回以上出現してはならない（ループ排除）<br>Track各端部（BoundaryPoint以外）は、到達可能なArrivalEP/DepartureEPが1つ以上StationPathとして存在すること<br>Waypoints各要素の参照先オブジェクトのFloorUnitIdが、StationPath.FloorUnitIdと一致する |
| AdjustmentSec | `int` (既定値 `0`) | 停車時の構内進路の通過に必要な時間。通過時には用いない。 |
| ManualConflictObjectIds | `List<VirtualConflictObjectId>` (既定値 `new()`) | 所属する仮想支障グループ識別子 |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.StationPathDirection` (enum)

構内進路の方向種別

> Arrival：到着用
> Departure：出発用
> Shunting：入替作業用
> 単線等で進行方向が固定されている場合があり、EntryPointの種別から解決することが不可能なため、有向としている。
> Shuntingの向きは移動元、移動先から判定する。詳しくはTimeTable.Trains.StationWorkを参照。

| Value | 説明 |
|---|---|
| Arrival |  |
| Departure |  |
| Shunting |  |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.StationPathWaypoint` (record)

進路オブジェクトでRail端点を参照するための抽象基底型

> RailEndpointRefとは異なり、StationPathとして参照する、
> もしくはStationPathの自動検出の判定に利用する端点種別のみに派生型を限定している。
> 自動検出についてはAlgorithm.Stations.FloorUnitObjects.StationPathSuggesterを参照。

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.SwitchMechanism` (class)

片開き、両開き分岐器の分岐構造を表現する

| Field | Type | 説明 |
|---|---|---|
| RootPortIndex | `int` | 基底側。構成可能な2つの進路の配列で共通のRailが該当する。 |
| NormalPortIndex | `int` | 正位側 |
| ReversePortIndex | `int` | 反位側 |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.Switcher` (class)

分岐器を表す

| Field | Type | 説明 |
|---|---|---|
| Id | `SwitcherId` | 分岐器識別子 |
| Base | `FloorUnitObjectBase` | 駅階層識別子と座標情報を保持する複合フィールド |
| PortCount | `int` | 収束するRailの数。<br><br>**制約/備考：** PortCount == 3 \|\| 4 のみ整合。 |
| Mechanism | `SwitchMechanism?` | PortCount == 3 の場合に利用。<br>片開き、両開き分岐器の分岐構造を表現する。 |
| ValidRoutes | `List<PortPair>` (既定値 `new()`) | PortCount == 4 の場合に利用。<br>要素数は 2 &lt;= かつ &lt;= 4<br><br>**制約/備考：** ValidRoutes=2：シザースクロッシング<br>ValidRoutes=3：シングルスリップスイッチ<br>ValidRoutes=4：ダブルスリップスイッチ |

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.SwitcherEndpointRef` (record)

レール端点の「分岐器」を参照する型

| Field | Type |
|---|---|
| Id | `SwitcherId` |
| PortIndex | `int` |

> PortIndexはSwitcher接続時のみ意味を持つため、Switcher用派生型にのみ持たせる（構造的防止）

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.SwitcherRoutingExtensions` (class)

---

##### `public static IReadOnlySet<PortPair> GetTraversablePairs(this Switcher switcher)`

Switcherの構造（mechanism / validRoutes）から、通行可能なPortペアの集合を都度計算する。

**Remarks**
永続化はしない派生値。N=3はroot-normal・root-reverseの2組、N=4はvalidRoutesそのもの。
PortIndexの割り当て順序に業務的意味を持たせないため、各ペアはPortA&lt;=PortBに正規化する。

---

##### `public static PortPair Normalize(int a, int b)`

PortPairのPortA&lt;=PortB正規化。

**Parameters**

- `a`: 
- `b`: 

**Returns**

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.SwitcherWaypoint` (record)

レール端点の「分岐器」を参照する型

| Field | Type |
|---|---|
| Id | `SwitcherId` |

> PortIndexはRailのためのフィールドであるから持たない。

---

#### `DiaEditCore.Model.Stations.FloorUnitObjects.VirtualConflictObject` (class)

Algorithm.TimeTable.Trains.Conflicts.ConflictChecker用の仮想グループオブジェクト

| Field | Type | 説明 |
|---|---|---|
| Id | `VirtualConflictObjectId` | 仮想支障グループ識別子 |
| FloorUnitId | `FloorUnitId` | 所属する駅階層の識別子 |
| Name | `string` (既定値 `""`) | 仮想支障グループ名 |

> 信号システム、建築限界など、グラフ構造から導出できない支障をグループ化する。

#### 6.1.5 TimeTable

#### `DiaEditCore.Model.TimeTable.DateRange` (record struct)

規制期間を表現する

| Field | Type |
|---|---|
| Start | `DateTime` |
| End | `DateTime` |

---

#### `DiaEditCore.Model.TimeTable.DiagramRevision` (class)

ダイヤ改正1回分のまとまりを表す

| Field | Type | 説明 |
|---|---|---|
| Id | `DiagramRevisionId` | ダイヤ改正識別子 |
| BaseRevisionId | `DiagramRevisionId?` | 複製元追跡タグ |
| TimeTableSetIds | `List<TimeTableSetId>` (既定値 `new()`) | 所属する時刻表セットの識別子<br><br>**制約/備考：** null許容。ただし、DiagramRevisionを作成した際に、空のTimeTableSetを作成するという方針もアリ。（Stationと似たような仕組み）（要検討。） |

---

#### `DiaEditCore.Model.TimeTable.DisplayContext` (record)

ダイヤグラム・駅時刻表の表示対象を定義する。
「路線系統」を基準に表示範囲を定義し、そこにServiceRouteに属するTrainを投影する。

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

規制対象がレールの場合

| Field | Type |
|---|---|
| RailId | `RailId` |

---

#### `DiaEditCore.Model.TimeTable.RestrictionTarget` (record)

規制対象を表現する

---

#### `DiaEditCore.Model.TimeTable.Segment` (record)

規制対象が駅間の場合

| Field | Type |
|---|---|
| StationConnectionSegmentId | `StationConnectionSegmentId` |

---

#### `DiaEditCore.Model.TimeTable.TemporaryRestriction` (record)

工事や災害等による規制、時間帯・列車種別・車両形式などの制約を表現する

| Field | Type |
|---|---|
| Id | `TemporaryRestrictionId` |
| Target | `RestrictionTarget` |
| ExtraRunTimeSec | `int?` |
| SpeedLimitKph | `int?` |
| DateRange | `DateRange` |
| Note | `string` |

> 実装の優先度は低。

---

#### `DiaEditCore.Model.TimeTable.TimeTableSet` (class)

時刻表セットを表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `TimeTableSetId` | 時刻表セット識別子 |
| Name | `string` | 時刻表名 |
| TrainIds | `List<TrainId>` (既定値 `new()`) | 所属する列車リスト（表示順）<br><br>**制約/備考：** 意図的な空白欄・基準駅ソート等、ユーザーが指定する**管理上の並び順**を保持する単一の正データである。 |

#### 6.1.6 TimeTable/Trains

#### `DiaEditCore.Model.TimeTable.Trains.CouplingWork` (class)

NextTrain.Coupling専用：相手Trainへの参照型

| Field | Type | 説明 |
|---|---|---|
| PartnerTrainId | `TrainId` | 連結する（しに行く）相手Trainの識別子 |
| PartnerStopKey | `StopKey` | 連結する（しに行く）相手TrainのStopKey |
| AttachToFront | `bool` (既定値 `false`) | 編成の連結位置<br><br>**制約/備考：** 相手編成の駅到着前の進行方向に準ずる。<br>false = 相手編成の後ろに連結。true = 相手編成の前に連結。 |

---

#### `DiaEditCore.Model.TimeTable.Trains.CutGroupEntry` (class)

Decoupling専用：分割後1グループ分の要素

| Field | Type | 説明 |
|---|---|---|
| CarCompositionId | `CarCompositionId` | 編成の識別子 |
| OperationNumber | `string` | 運用番号 |

---

#### `DiaEditCore.Model.TimeTable.Trains.DecouplingWork` (class)

解結・分割作業を表す

| Field | Type | 説明 |
|---|---|---|
| FrontGroup | `List<CutGroupEntry>` | 分割後の前側のグループ<br><br>**制約/備考：** 分割前の走行方向に対する前側。最低1件必須。<br>front/rear間でCarCompositionIdの重複は不可。 |
| RearGroup | `List<CutGroupEntry>` | 分割後の後ろ側のグループ<br><br>**制約/備考：** 分割前の走行方向に対する後ろ側。最低1件必須。 |
| IsRearBase | `bool` (既定値 `false`) | 分割後、編成グループの継承対象をどちらにするか決定するフラグ<br><br>**制約/備考：** false = front側が基準（自Trainがそのまま継続）。<br>true = rear側が基準。<br>継続側でないほうが SplitOriginRef 経由の新Trainとして生まれる。 |

> OperationIdフィールドは意図的に持たない：合流するCarCompositionの運用番号は
> 合流前の値をそのまま保持するため（CarCompositionに紐づく属性であり、Couplingでは変化しない）。

---

#### `DiaEditCore.Model.TimeTable.Trains.LineStyle` (enum)

ダイヤグラム線の種類

> 具体的な値はUI（ダイヤグラム）実装時に確定
> Solid：実線
> Dashed：破線
> Dotted：点線

| Value | 説明 |
|---|---|
| Solid |  |
| Dashed |  |
| Dotted |  |

---

#### `DiaEditCore.Model.TimeTable.Trains.NextTrainType` (enum)

次列車接続種別

> Other別列車
> TypeChange列車種別変更
> InfoChange列車情報変更
> SameTrain同列車扱い
> Coupling増結・併合

| Value | 説明 |
|---|---|
| Other |  |
| TypeChange |  |
| InfoChange |  |
| SameTrain |  |
| Coupling |  |

---

#### `DiaEditCore.Model.TimeTable.Trains.PrevTrainOperationOverride` (class)

PrevTrain専用：直前Trainから引き継いだCarCompositionのうち運用を変更するものだけの差分リスト。

| Field | Type | 説明 |
|---|---|---|
| CarCompositionId | `CarCompositionId` | 編成の識別子 |
| NewOpNumber | `string` | 新規運用番号 |

> 省略時（＝該当CarCompositionIdがリストに現れない場合）＝全Composition継承。

---

#### `DiaEditCore.Model.TimeTable.Trains.SplitOriginRef` (class)

Decouplingで生じたTrain専用：自身の起点を示す

| Field | Type | 説明 |
|---|---|---|
| OriginTrainId | `TrainId` | 分割元のTrainの識別子 |
| OriginStopKey | `StopKey` | 分割元のTrainのStopKey |

> GroupIndexは持たない：どちらのグループ（front/rear）を引き継いだかはDecouplingWork.IsRearBaseを
> 直読みすれば一意に決まるため。

---

#### `DiaEditCore.Model.TimeTable.Trains.StartOpCarSlot` (class)

StartOp専用：出区編成単位を表す（1編成ずつ登録する）

| Field | Type | 説明 |
|---|---|---|
| Position | `int` | 編成位置 |
| CarCompositionId | `CarCompositionId` | 使用編成の識別子 |
| OperationNumber | `string` | 運用番号 |

---

#### `DiaEditCore.Model.TimeTable.Trains.StationWork` (class)

駅作業を表す

| Field | Type | 説明 |
|---|---|---|
| Type | `StationWorkType` | 駅作業種別 |
| StartOpConsist | `List<StartOpCarSlot>` (既定値 `new()`) | StartOp専用：出区編成リスト |
| PrevTrainOperationOverrides | `List<PrevTrainOperationOverride>` (既定値 `new()`) | PrevTrain専用：運用番号更新リスト |
| DecouplingDetail | `DecouplingWork?` | Decoupling専用：解結・分割作業を表す |
| CouplingDetail | `CouplingWork?` | Coupling専用：相手Trainへの参照型 |
| SplitOrigin | `SplitOriginRef?` | Decouplingで生じたTrain専用（PrevTrain）：自身の起点を示す |
| NextTrainType | `NextTrainType?` | 次列車接続種別 |
| StationPathId | `StationPathId?` | 入替作業で利用する構内進路の識別子 |
| StartOpSeconds | `int` (既定値 `-1`) | 作業開始時刻<br><br>**制約/備考：** -1 は未設定。 |
| EndOpSeconds | `int` (既定値 `-1`) | 作業終了時刻<br><br>**制約/備考：** -1 は未設定。 |

---

#### `DiaEditCore.Model.TimeTable.Trains.StationWorkType` (enum)

駅作業種別

> Noneなし。通常の停車・通過が該当。
> PrevTrain前列車接続
> StartOp出区
> EndOp入区
> Shunting入換
> NextTrain次列車接続
> Coupling増結・併合
> Decoupling解結・分割

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

| Field | Type |
|---|---|
| StationId | `StationId` |
| VisitCount | `int` |

> 生成は必ずStopKeySequenceBuilderを経由すること。VisitCountを手計算してnew StopKey(...)を直接構築しないこと。
> readonly record structなのでDictionaryキーとして構造的等価性がそのまま使える

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
用途：
StopTimes書き込み側（RunSegments編集コマンド）が、新規追加・リキー時のキーを本メソッドの戻り値から取得する
StopTimes読み出し側（CarConsistResolver等）が、訪問順にStopKeyを辿るために使う

---

##### `public static List<StopKey> BuildVisitedStopKeys(Train train)`

train.RunSegmentsが定める訪問順（先頭駅→各RunSegmentのToStationId）に対応する
StopKey列を、訪問順のまま返す。

**Remarks**
戻り値のインデックスiは「経路上でi番目の停車」を意味するが、各StopKey自体のVisitCountは駅ごとのローカルカウンタである点に注意。

---

#### `DiaEditCore.Model.TimeTable.Trains.StopTime` (class)

駅の停車情報を表現する

| Field | Type | 説明 |
|---|---|---|
| ArrivalSeconds | `int` (既定値 `-1`) | 到着時刻 |
| DepartureSeconds | `int` (既定値 `-1`) | 発車時刻 |
| IsStop | `bool` (既定値 `false`) | 停車フラグ |
| TrackRailId | `RailId?` | 客扱い、使用する番線<br><br>**制約/備考：** バグの可能性あり。TrackRailIdは設定する必要がある。 |
| Works | `List<StationWork>` (既定値 `new()`) | このStopTimeで発生する駅作業 |

---

#### `DiaEditCore.Model.TimeTable.Trains.Train` (class)

列車の運行を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `TrainId` | 列車識別子 |
| TimeTableSetId | `TimeTableSetId` | 所属する時刻表セットの識別子 |
| TrainNumber | `string` | 列車番号 |
| ServiceNumber | `int?` | 号数 |
| ServiceRouteId | `ServiceRouteId` | 走行する運転系統の識別子 |
| TrainTypeId | `TrainTypeId` | 列車種別の識別子 |
| TrainTypeName | `DisplayName` | 列車種別名<br><br>**制約/備考：** ReadOnly |
| Nickname | `DisplayName` | 愛称<br><br>**制約/備考：** ReadOnly |
| DefaultVehicleTypeId | `VehicleTypeId?` | 基準となる列車形式の識別子 |
| SourceTrainId | `TrainId?` | コピー元の列車識別子 |
| Revision | `int` (既定値 `0`) | 編集回数 |
| SourceRevisionAtCopy | `int?` | コピー時のコピー元列車の編集回数 |
| RunSegments | `List<TrainRunSegment>` (既定値 `new()`) | 列車の走行実績 |
| StopTimes | `IReadOnlyDictionary<StopKey, StopTime>` | 停車情報の読み取り専用ビュー。StopKeyの追加・削除・差し替えは外部から不可能<br><br>**制約/備考：** StopTimeインスタンス自体のフィールド（ArrivalSeconds等）はこのスコープの対象外で、<br>依然として可変（将来の停車時刻編集コマンド設計時に別途検討）。 |
| StopTimesInternal | `Dictionary<StopKey, StopTime>` | StopTimes辞書への書き込み専用ルート。DiaEditCoreアセンブリ内<br>（SyncRunSegmentsToTrainCommand等の正規コマンド、およびテストのフィクスチャ構築）からのみ<br>使用すること。ViewModel/UI層（別アセンブリ）からは参照できない。 |
| IsProvisional | `bool` (既定値 `false`) | 仮列車フラグ |

---

#### `DiaEditCore.Model.TimeTable.Trains.TrainOperation` (class)

列車運用を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `TrainOperationId` | 列車運用識別子 |
| OperationNumber | `string` | 列車運用番号 |

---

#### `DiaEditCore.Model.TimeTable.Trains.TrainRunSegment` (class)

駅間ごとのStationConnection使用実績

| Field | Type | 説明 |
|---|---|---|
| FromStationId | `StationId` |  |
| ToStationId | `StationId` |  |
| StationConnectionId | `StationConnectionId` |  |
| IsOverriddenFromTemplate | `bool` (既定値 `false`) | 基準列車の値からの変更有無（UI表示用） |

---

#### `DiaEditCore.Model.TimeTable.Trains.TrainType` (class)

列車種別を表現する

| Field | Type | 説明 |
|---|---|---|
| Id | `TrainTypeId` | 列車種別識別子 |
| Name | `DisplayName` | 列車種別名 |
| DiagramColor | `string` | ダイヤグラム線色 |
| DiagramLineStyle | `LineStyle` | ダイヤグラム線種 |
| SortOrder | `int` | 同じ種別がServiceRouteをまたいで複数存在しても色・線種・並び順を統一<br><br>**制約/備考：** 親種別にした方がいい可能性。<br>TrainTypeId? BaseTrainTypeId { get; set; } |

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

RailEndpointConvergenceResolver.Classifyの戻り値。分類結果と、Errorケースのみ設定されるメッセージを保持する。

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
kindを保持し、ConvergenceClassification.ErrorMessageがnullの値。

---

##### `public static ConvergenceClassification Fail(string message)`

Errorケースの分類結果を生成する。

**Parameters**

- `message`: エラー内容を説明するメッセージ。

**Returns**
ConvergenceKind.Errorとmessageを保持する値。

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
参照先オブジェクトのPoint座標。

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
positionに収束しているRail端点の一覧。0件の場合は空リスト。

**Remarks**
同一Railの両端が同一座標に収束するケース（自己ループ）は現行モデルでは考慮不要
（Rail作成・移動のいずれの経路でも両端を同一点にする操作はUI上想定されていないため、
検出はするが特別扱いはしない＝両端とも収束集合に普通に加わる）。

---

##### `public static ConvergenceClassification Classify(IReadOnlyList<RailEndpointLocation> converging)`

収束集合から変換種別を判定する。

**Parameters**

- `converging`: FindConvergingEndpointsが返す収束集合。

**Returns**
収束数に応じたConvergenceClassification。N=3,4の場合、収束集合中に既存
SwitcherEndpointRefが1件でも含まれていればConvergenceKind.SwitcherExpand、
含まれていなければConvergenceKind.SwitcherNewを返す。N&gt;=5は
ConvergenceClassification.Failによるエラーを返す（例外は投げない）。

**Remarks**
N=0（削除方向で呼ばれ、当該座標を参照するRailがもう1本もない場合）を独立ケースとして扱う点が
v13.13時点との差分：Rail削除に伴う「再分類」呼び出しで初めて起こりうるケースであり、
Rail新規作成・ドラッグ方向の呼び出しでは通常発生しない（自分自身は集合に含まれるため）。
前提：同一Switcherの複数ポートが収束集合に混在していても、Idが同じなので拡張と判定して問題ない。

---

##### `public static IReadOnlyList<(RailEndpointLocation Location, int PortIndex)> AssignSwitcherPorts(IReadOnlyList<RailEndpointLocation> converging)`

新規Switcher作成時のPort機械採番（暫定案、v13.13：Rail.Id昇順で0〜N-1）。

**Parameters**

- `converging`: Port割当対象の収束集合。呼び出し前提：Classifyが
ConvergenceKind.SwitcherNewを返したケースでのみ使う
（ConvergenceKind.SwitcherExpandは既存PortCountへの追加のため別ロジックが必要、本メソッドの対象外）。

**Returns**
各RailEndpointLocationに対応するPortIndexの割当。同一Rail.Id内の順序に依存しない
一意な並びを保証するため、Rail.Id→(必要ならEnd)で安定ソートした結果を0始まりで返す。

---

##### `public static IReadOnlyList<StationPathId> FindBlockingStationPaths(IReadOnlyCollection<ObjectId> disappearingIds, IReadOnlyList<StationPath> stationPaths)`

収束変換により消滅する既存EntryPoint/BoundaryPoint/BufferStop/Switcherを
StationPath.Waypointsが参照していないか確認する。

**Parameters**

- `disappearingIds`: 収束によって消滅する側（＝収束集合中、これから作成するBoundaryPoint/Switcher以外の既存
BoundaryPoint/EntryPoint/BufferStop/SwitcherのObjectId）。
既存Switcherが「拡張」される場合（ConvergenceKind.SwitcherExpand）はSwitcher自体は
消滅しない点に注意（呼び出し側でdisappearingIdsに含めないこと）。
- `stationPaths`: 検証対象の全StationPath。

**Returns**
disappearingIdsのいずれかをWaypointsに含むStationPathのId一覧。
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

> RailEndはRailMerger.RailEnd（A/B）を再利用する（新規enumを増やさない。）

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

モデル種別ごとの単調カウンタ方式Id採番器。
Undo・削除の有無に関わらず、一度発行したIdは同一セッション内で二度と発行しない。

> ProjectSession.Load()時にモデル種別ごとに1つ生成し、既存の最大Id+1から開始する

---

##### `public IdAllocator(Func<int, TId> factory, IEnumerable<int> existingIds)`

---

##### `public TId AllocateNext()`

---

#### `DiaEditCore.Session.ProjectSession` (class)

読込中のProjectFileとその派生キャッシュ(TimeTableSetCache)のライフサイクルを一元管理する。
CommandInvokerからのICacheChangeObserver通知を受けてキャッシュをdirty化し、
次にキャッシュへアクセスする直前に遅延再構築する。

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

Undo/Redo可能なコマンドの基底型。

| Field | Type |
|---|---|
| Target | `TTarget` |
| AffectedIds | `IReadOnlySet<ObjectId>` |

> スナップショット方式を採用。Execute()前に対象オブジェクトの状態をまるごと複製し、
> Undo()はその複製を書き戻すだけにする（逆操作を個別に書く方式は採用しない）。
> AffectedIds（影響を受けるObjectIdの集合）は、DependencyResolver による自動計算ではなく、
> コマンド実装者がコンストラクタで手動列挙する。将来 DependencyResolverができた場合も、
> この基底型のシグネチャは変えずに済む（具象コマンド側でAffectedIdsの構築ロジックだけ差し替えればよい）。
> Execute()/Undo()はaffectedIdsを返すだけの薄い型とし、ICacheChangeObserverへの
> 通知責務は持たない。通知はCommandInvoker（呼び出し元）が担う。
> 型引数：
> TTarget   ：このコマンドが変更する対象オブジェクトの型（例：Train、StationConnection）。
> TSnapshot ：TTargetの状態を複製したスナップショットの型。
> 不変な値（record等）にして、CaptureSnapshot後にTargetを変更してもスナップショット
> 自体が影響を受けないようにすること（参照をそのまま持ち回すと複製の意味が無くなる）。

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
| Created | `Station?` | Execute()実行後、生成されたStationを呼び出し元（ViewModel/UI層）が参照するためのプロパティ。<br>AffectedIdsが空集合のため、生成結果をUIへ伝える手段として別途公開する。 |

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
> (2) RailEndpointConvergenceWorkflow.ReconcileのSwitcherExpandケース
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
- `snapshot`: CaptureSnapshotが返したスナップショット。

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
> (1) Algorithm.Stations.FloorUnitObjects.RailEndpointConvergenceResolver.FindBlockingStationPaths
> によるStationPath参照チェック、
> (2) RailEndPointRefChangerによる、削除対象を参照していたRail端点の張替え完了、
> を本コマンドの実行前に必ず済ませておく前提とする（TransActionCommand内の実行順序：
> 新規移行先作成 → RailEndPointRefChangerで参照を移行先へ張替え → 本コマンドで旧端点を削除）。
> この順序を守れば、本コマンドが実行される時点で削除対象を参照するRailは存在しない
> （DeleteRailCommandのような実行時の参照元チェックは不要、構造的に安全）。
> AffectedIdsはDelete系の通常パターン通り、呼び出し元がコンストラクタで確定値を渡す
> （Create系と異なりExecute前から削除対象のIdが判明しているため、
> ComputeAffectedIdsAfterApplyのオーバーライドは不要）。

---

##### `public DeleteFloorUnitObjectCommand(List<T> target, T toDelete, IReadOnlySet<ObjectId> affectedIds)`

コマンドを構築する。この時点では削除は行われない（UndoableCommand{TTarget, TSnapshot}.Executeまで副作用なし）。

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

削除対象をtargetから取り除く。

**Parameters**

- `target`: 削除対象を含むリスト。

---

##### `protected override void Restore(List<T> target, T snapshot)`

削除前の状態へ戻す（削除対象インスタンスをtargetへ再追加する）。

**Parameters**

- `target`: 削除対象を含むリスト。
- `snapshot`: CaptureSnapshotが返したスナップショット（削除対象インスタンス）。

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

Railを削除するコマンド。

> 以下のいずれかに該当する場合、削除を拒否する（例外送出、コレクション状態は変化しない）：
> DependencyResolverのObjectIdグラフ上で、他オブジェクトから直接参照されている場合
> Platform.FacingRailIdsから参照されている場合
> TemporaryRestriction.Target（RestrictionTarget.Rail）から参照されている場合
> Train.StopTimes[...].TrackRailIdから参照されている場合
> AffectedIds（変更通知対象）には、削除対象Rail自身に加え、その所属FloorUnitのObjectIdも
> 含まれる。

---

##### `public DeleteRailCommand(List<Rail> rails, Rail railToDelete, ProjectSession session, IReadOnlyList<Platform> allPlatforms, IReadOnlyList<TemporaryRestriction> allRestrictions, IReadOnlyList<Train> allTrains)`

**Parameters**

- `rails`: 削除対象を保持するRailコレクション（Undo/Redoの対象コレクション）。
- `railToDelete`: 削除対象のRail。
- `session`: 依存関係チェック・変更通知範囲の算出に使うプロジェクトセッション。
- `allPlatforms`: FacingRailIds参照チェック対象の全Platform。
- `allRestrictions`: Target参照チェック対象の全TemporaryRestriction。
- `allTrains`: StopTime.TrackRailId参照チェック対象の全Train。

---

##### `protected override Rail CaptureSnapshot(List<Rail> target)`

削除対象のスナップショット（Undo復元用）を取得する。

**Parameters**

- `target`: 対象コレクション。

---

##### `protected override void Apply(List<Rail> target)`

対象コレクションからRailを取り除く。

**Parameters**

- `target`: 対象コレクション。

---

##### `protected override void Restore(List<Rail> target, Rail snapshot)`

Undo時、削除したRailを対象コレクションへ復元する。

**Parameters**

- `target`: 対象コレクション。
- `snapshot`: 復元するRailのスナップショット。

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
> RailEndpointConvergenceResolver.FindConvergingEndpointsが返す収束集合を
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
RailEndpointConvergenceResolver.FindConvergingEndpointsの戻り値からRef部分を除いたもの）。
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

張替え対象の全端点を_newRefへ変更する。

**Parameters**

- `target`: Railを含むリスト。

---

##### `protected override void Restore(List<Rail> target, IReadOnlyList<(RailId RailId, RailEnd End, RailEndpointRef OldRef)> snapshot)`

張替え対象の全端点を、張替え前の参照へ戻す。

**Parameters**

- `target`: Railを含むリスト。
- `snapshot`: CaptureSnapshotが返したスナップショット。

---

#### `DiaEditCore.Commands.Stations.FloorUnitObjects.RailEndpointConvergenceWorkflow` (class)

ある座標における収束状態（RailEndpointConvergenceResolver.Classifyの結果）と、
その座標に現在存在する端点オブジェクトの型が一致しているかを比較し、不一致の場合のみ
「新規作成→RailEndPointRefChangerで参照張替え→旧端点削除」の変換ステップを組み立てる。

> 増加方向（Ctrl+ドラッグでの新規Rail作成・既存端点へのドラッグ接続）・
> 減少方向（DeleteRailCommandによるRail削除）のいずれから呼ばれても同一ロジックで動作する
> （Converge/Divergeを別実装にしない）。
> 前提：converging（RailEndpointConvergenceResolver.FindConvergingEndpointsの戻り値）は
> 呼び出し時点で最新の状態を反映していること（DeleteRailCommandから呼ぶ場合は、対象RailをTargetから
> 除去した後に再計算すること）。
> StationPathブロックチェック（RailEndpointConvergenceResolver.FindBlockingStationPaths）は
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
変換が必要な場合は、その変換ステップ一式を束ねたTransActionCommand。
既に整合済み、またはVanishかつoldObjectIdがnull（元々何も存在しなかった）の場合はnull。

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

DiaEditCore側のDIコンテナ登録。

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

ProjectSettings.ValidationRulesの値域を検証する。

---

##### `public IReadOnlyList<IValidationIssue> Validate(ProjectSettings target, ValidationContext context)`

---

#### `DiaEditCore.Serialization.Validation.SaveValidationRunner` (class)

ProjectFile全体に対して全Validatorを実行し、issueの一覧を返す。
1件でもissueがあれば保存不可（本プロジェクトの運用ではValidationSeverity.Warning＝保存不可相当。）
とみなす判断は呼び出し側（JsonProjectFileSerializer.Save）が行う。

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
| IsEditableMode | `bool` | 閲覧モードではRail・端点とも選択のみ可（属性パネルは参照専用にする想定、<br>パネル自体のReadOnly化はステージ2でドラッグ編集導入時にあわせて対応）。<br>線路・端点・ホーム編集モードでのみ新規作成・削除・属性変更を許可する。 |
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
| ObservedIds | `IReadOnlySet<ObjectId>` | StationDetailViewModel.ObservedIdsと同じ設計。FloorUnit自身に加え、現在このFloorUnitに<br>属する（端点経由で導出される）Rail群のIdを都度算出する。 |

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
| IsDirty | `bool` | §9.2項目31：現在の入力値がStationの現状値と一致しているかどうか。<br>Save()側の差分判定（BuildEditedSnapshotとCaptureCurrentSnapshotの比較）と<br>同じ比較規約を使う（DisplayNameのIEquatable実装、§9.2項目31関連対応）。 |
| ResolvedShowsInStationTimetable | `bool` |  |
| SelectedFloorUnit | `FloorUnit?` |  |
| DeleteFloorUnitError | `string?` |  |
| HasDeleteFloorUnitError | `bool` |  |
| ObservedIds | `IReadOnlySet<ObjectId>` | M2-4：ChangeNotificationBridge向けの監視対象。Station自身に加え、現在この駅に属する<br>全FloorUnitのIdを含める。固定集合ではなく都度算出するプロパティとすることで、<br>FloorUnit追加直後（Execute完了後・Notify呼び出し前）の新規Idも自動的に拾える<br>（ChangeNotificationBridge.OnChangedはOverlaps判定のたびにこのgetterを再評価するため）。 |

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
