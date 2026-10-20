# Doukutsu 開発整理メモ

更新日: 2026-10-20

## 今回整理した範囲

- 保存Version 14: 持ち物・装備・倉庫・箱・地面アイテムに4方向回転を保持。
- Version 13以前の回転boolは0/90度として復元。存在しなかった180/270度は復元できない。
- 応急キットの固定3枠数量を保存・ロード・ニューゲームへ接続。旧セーブの医療品は0個。
- 箱に保存する武器のジャム状態を保存・復元。
- セーブ上書きを一時ファイルからの置換に変更。前回分は同じ場所の `.bak` に保持。
  バックアップの自動読み込みUIは未実装。置換が利用できない環境では既存セーブを残して失敗を通知する。
- セッション内の報酬配置も通常の `InventoryGrid` を利用し、不規則形状の占有マスと4方向を判定。
- ビルド先頭を `Taitol` に変更。
- `GameSessionManager` を本体・保存変換・データ型に分割。
- Editor専用3ファイルを `.meta` と一緒に `Assets/Editor` へ移動。
- 救助結果の自動診断生成をEditor/Development Build限定に変更。

## シーンの採用方針

通常導線は既存の設定に合わせて、次を基準とする。

`Taitol → Town_Main → stage1 / Doukutu_2 / Doukutu_3 / Doukutu_4 → Result → Town_Main`

町からの各遷移先、タイトルのニューゲーム先は既存設定。今回変更したのはビルド先頭のみ。
洞窟退出は `CaveExitElevator2D` と `ExpeditionResultCollector2D` を通す。
単純な `SceneTransitionButton` による町への直行は、結果集計を迂回しないか確認する。

| シーン | 扱い |
|---|---|
| Taitol | 製品起動、ニューゲーム・ロード |
| Town_Main | 通常の町、会話・取引・準備 |
| stage1 | 通常の第1ステージ |
| Doukutu_2 / 3 / 4 | 町から選択するステージ |
| Result | 救助・治療・依頼報酬の確定 |
| Start_Town_Main / Start_Doukustu | 既存チュートリアル導線。通常ニューゲームへの組み込みは仕様決定後 |
| OpeningCutScene / Doukustu | 接続が残る導入・洞窟。使用停止と断定せず維持 |

シーン名はセーブ内に記録される。名前変更・削除には旧セーブのScene名移行が必要。

## 会話・取引の統一方針

### 会話

| 系統 | 今後の用途 |
|---|---|
| TownConversationData / TownConversationController | 町の住人・商人・依頼人、既存の洞窟会話の基本方式 |
| DialogueData2D / DialogueActor2D / DialoguePresenter2D | CharacterAIキャラクターの勢力・性格条件付き会話 |
| TownResidentDialogueData / TownDialogueController | 既存コンテンツ互換。新規会話の追加先にはしない |
| TownMissionResidentDialogueData / Controller | 既存ミッション会話互換。新規はTownConversation方式へ |
| AmbientTalk / BattleBarks / Dialogue600 | 立ち話・戦闘セリフ・独り言。対話UIとは役割が異なるため継続利用 |

会話データの形式が異なるため、旧Controllerだけを一括削除しない。
移行は住人単位で開始条件・会話履歴ID・依頼・報酬・Buttonイベントを照合し、旧コンポーネントを外す。
自動生成の参照一覧は同じフォルダの `SceneComponentUsage.md` を参照。

### 取引

- 新しい町/洞窟のグリッド売買は `TownUnifiedTradeScreen2D` と `ExpeditionUnifiedTradeController2D` の決済を使用。
- `MerchantStockInventory` を実在庫、`MerchantShopData` / `DungeonNpcTradeProfile2D` を設定として扱う。
- `DungeonNpcShop2D` の共有UI接続を利用するキャラクターは、その接続を優先する。
- 旧 `PawnShopUIController` / `MerchantPurchaseController` / 売却カート経路は既存参照用として維持。
- 物々交換・武器修理・応急キット購入は異なる処理なので、通常売買へ無理に統合しない。
- 価格変更は購入・売却・在庫残高・キャンセル返却を合わせて確認する。

## コードの配置と状態の所有者

| 配置 | 責務 |
|---|---|
| Assets/Script/Item | アイテム定義、地面アイテム |
| Assets/Script/Inventry | 個体、グリッド、持ち物・装備の操作と表示 |
| Assets/Script/Player / Gun / Enemy | プレイヤー・戦闘・敵の動作 |
| Assets/Script/Mission / Talk | 依頼と会話 |
| Assets/Script/GameRule/GameSessionManager.cs | 永続進行の実行時管理、シーン間の持ち物同期 |
| Assets/Script/GameRule/Save/GameSessionManager.SaveSerialization.cs | JSONデータとセッション間の変換。partialで公開APIを維持 |
| Assets/Script/GameRule/Save/GameSessionData.cs | セッションデータ型 |
| Assets/Script/GameRule/Save | 保存・復元・保存用ストア |
| Assets/CharacterAI2D/Runtime | 勢力と連動する行動・キャラクター会話 |
| Assets/Editor | 制作・移行・検証ツール |

`GameSessionManager` のpartial分割はファイル単位の責務整理。実行時の依存関係を解消する全面改修ではない。
元のMonoBehaviour名、既存シリアライズフィールド、元ファイルのGUIDを維持している。
既存の `Inventry` などのフォルダ名は、Editorツールの固定パス参照を調査してから変更する。

| 状態 | 実行時の保持先 | 永続保存 |
|---|---|---|
| 所持金・持ち物・装備・施設・依頼・物語・スキル・救助累計 | GameSessionManager | SaveGameData |
| HP・食料・水分・SAN・基本状態異常・松明 | PlayerStatusSessionStore | SaveGameData.PlayerStatus |
| 地面アイテム・箱 | WorldStateSessionStore | SaveGameData.WorldState |
| 応急キット数量 | MedicalKitSessionStore2D | SaveGameData.MedicalKit |
| 会話回数 | TownConversationHistoryManager | SaveGameData.ConversationHistory |
| 今回の帰還結果・治療結果 | ExpeditionResultSession等 | 一時データ。保存範囲の拡張は別途設計 |

## 残る設計課題

1. `DungeonLootBoxMarker2D` はシーン読み込み後に箱内容を再抽選する。新規出撃とロード再開を区別する仕様が必要。
2. ポーズ・操作停止が複数Controllerに分散。画面の重なりを所有者付きロックに統一する余地がある。
3. レイヤー3と7がともにPlayer。実体の割当とMaskを確認してから名称を整理する。数値を一括変更しない。
4. privateフィールドを文字列で読む互換UI・商人接続がある。専用APIへ段階的に置換する。
5. `GameSessionManager` の残りの進行管理、巨大な取引・会話Controllerは、担当機能の変更時に順次分割する。
6. オートセーブの契機、カルマ増減、導入シーン採用はゲーム仕様として確定させる。

## 検証方法

2026-10-20: Unity 6000.4.10f1をbatchmodeで起動し、プロジェクトのコンパイルと下記セルフテストが成功。
`Logs/CodexSaveCompatibility.log` にPASSと終了コード0を確認。既存の非推奨API等のコンパイル警告は残る。
プレイ操作による各ステージの通し確認と実ファイルを使った医療品ロード・ニューゲーム確認は未実施。

UnityのEdit Modeで `Tools > Project Maintenance > 保存互換性テスト` を実行。
実アセットを変更しない一時オブジェクトで、4方向・旧回転形式・箱の復元・地面保存・不規則形状への報酬配置・医療品JSONを検証する。

手動確認:

- 非対称形状を180/270度に回転して町→洞窟→帰還、保存→再起動→ロードで向きと数量を確認。
- 医療品1/2/3個を保存し、使用・購入後にロードすると元に戻ることを確認。
- 同一起動でニューゲームを開始し、医療品が前のゲームから引き継がれないことを確認。
- 箱にジャムした武器を置いて保存・復元し、ジャム状態を確認。
- タイトル起動、各ステージへの出撃、エレベーター帰還、結果報酬の一度だけの付与を確認。
- 既存セーブを上書きし、`.bak` が前回分として残ることを確認。
