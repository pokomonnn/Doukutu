# 脚本ストーリーの本編統合

## 最新変更：フィンの治療とバルドへの搬送

ユーザー指定により、旧「戻らぬ仲間」「病に伏した仲間」を「毒に倒れたフィン」（S1S01）へ統合。旧脚本よりこの変更を優先します。全体はメイン4件＋サブ8件です。

1. stage1のロアンまたはバルドから受注。受注前にフィンを発見・治療しても進行できます。
2. フィンへ近づきTで既存救急キットを開き、解毒剤を使用。既存の処置完了時だけ1個消費し、毒を解除。「ありがとう、助かったよ」と頭上に表示します。
3. 既存の担ぎ操作でフィンをバルドのもとへ搬送。治療前や離れた場所では達成しません。帰還エレベーターはこのNPCを収納しません。
4. 到着時にMissionManager2Dで達成。バルドへEで話しかけ、「バルドからフィン救助の報酬を受け取る」を選択。報酬100Gは暫定値で、Mission_S1S01.assetのStory Money Rewardで設定します。既存のRewardClaimedにも記録し、二重受取を防ぎます。

CharacterHealthのPrevent Deathはフィンのシーン個体のみON。他のNPC・プレイヤーの死亡処理は変更しません。治療済み・搬送済み・報酬済みの状態は既存セーブに保存します。治療後に帰還した場合、再訪したフィンの毒は解除されたままです。搬送済みなら再出現しません。旧版でS1S01を完了済みのデータも再出現・再報酬の対象にしません。

旧S1S02と薬のアセットは参照互換のため保持し、本編のミッション登録・会話・取得地点から除外しています。古い薬をセーブから強制削除する処理はありません。原稿を保存したまま、StoryIntegrationBuilder.Finn.csで今回の変更を反映します。

MissionDefinition2Dは目的・受注条件フラグ・達成条件フラグ・現地報酬を定義し、MissionManager2Dが進行・達成・通知を管理します。細かな会話分岐、治療済み等の途中状態、結末条件は既存StoryFlagsとの連携です。

更新箇所：StoryFinnDelivery、MedicalKitTreatment2D、CharacterHealth、MissionDefinition2D／MissionManager2D、StoryCampaignRuntime、エレベーター・救出集計のフィン除外、Editor生成処理・統合テスト、および対応シーン・会話データ。以下の過去の検証記録は各実施時点の内容です。

今回のUnity統合テストは成功（`Logs/StoryFinnRevision.log` の `STORY PLAYTEST PASS`）。毒の初期状態、致死ダメージの防止、在庫0／別の医療用品の拒否、解毒剤1個の消費、再治療時の無消費、治療状態の再訪保持、担ぎ実績なしの拒否、実際のPlayerCarryController2Dによる担ぎ、バルド到着時のMissionManager2D完了、担ぎロック解除、報酬100Gの一度だけの付与、RewardClaimedの保存復元を確認しました。全12クエストと結末A/B/Cも通過しています。今回の検証はAPI経由のPlayモード統合テストで、画面上の手動操作確認はユーザー側で行います。

## 起動と操作

Unityで `Assets/Scenes/Town_Main.unity` を開いてPlayします。既存の町施設・出撃画面を使用します。
画面上部に町長、執事、後日談への会話ボタンを追加しました。表示処理は既存のTownConversationControllerです。サブクエストの依頼・報告は各ダンジョン内のロアン、グレン、バルドが担当します。

探索先は既存の `stage1`、`Doukutu_2`、`Doukutu_3`、`Doukutu_4`。各シーンのStoryIntegration配下に会話・調査地点があります。近づいてEで既存Dialogue Studioの会話を開始します。
プレイヤー移動、照準、射撃、Tabインベントリ、担ぎ操作は既存コンポーネントを使用します。Playerプレハブの搬送キーはF／G／Fの既存設定です。ストーリー会話中は移動・射撃・担ぎを止め、被弾・死亡・距離超過で会話を中断します。
通常の帰還エレベーターと結果画面を使用し、セーブは既存SaveManagerの枠を使用します。

## クエスト

|場所|メイン|サブ|
|---|---|---|
|stage1：旧居住区|消えた遠征隊の足跡|毒に倒れたフィン（2依頼を統合）／古城の設計図|
|Doukutu_2：忘れられた工房区|封鎖された昇降路|奪われた補給物資／新たな稼ぎ口／止まった水路|
|Doukutu_3：巡礼路と大聖堂|失われた巡礼路|争いの終わる場所／失われた交易路／眠れる古城|
|Doukutu_4：女神の庭|消えた貴族|結末A・B・C|

- 序盤3ステージの調査順は自由。町長へのメイン報告3件で庭への出撃を解放します。
- 設計図、補給物資、部品3種は既存ItemData／InventoryControllerを使用。空きがない場合は取得しません。売却不可で、紛失した用品は未完了の調査地点から再取得できます。
- メインの手がかりはStoryFlagsに記録し、持ち物の紛失に影響されません。
- フィンは既存の救助NPCプレハブ・救急キット・担ぎ処理を使用。毒を解毒剤で治療し、バルドの近くへ搬送するとMissionManager2Dで達成。会話で報酬を受け取ります。帰還エレベーターへの搬入対象からは除外します。
- フィンは毒状態で開始し、毒・攻撃で死亡しない専用設定です。治療成功時に「ありがとう、助かったよ」と話します。治療状態は保存し、搬送済みフィンは再出現しません。
- 設計図はstage1のグレン、補給物資はstage2のロアン、水路修理はstage2のグレンに現地報告します。stage3の停戦提案・報告と古城調査報告も現地で行います。各代表者の会話から受注・内容確認・報告・報告台詞の再読ができます。
- 水路部品は設置後に再取得できません。未設置の紛失品は再取得できます。
- フィン搬送の報酬受取でプレイヤーと盗賊の停戦、搬入許可の受渡しで友好を記録。指定回廊の停戦はその区域内の冒険者と盗賊だけに適用します。代表者の周囲には交渉用の局所保護区域があります。
- 局所保護区域はモンスターとの戦闘には適用しません。フィンの位置には保護区域を設けず、既存の搬送中の危険を維持します。
- 水路復旧と古城調査、搬入許可と交易路などの前提条件を維持。第3ステージの各最終報告で3派閥の協力がそろうと結末Cを選べます。
- 庭の会話を最後まで進めた後、結末を確認画面で確定します。保留して帰還・再訪できます。確定後は変更不可、会話は再読可能。重複報酬は与えません。

## 保存と編集先

- 原稿は `脚本/`。原稿自体は変更していません。
- 原稿抽出用の `Tools/ImportCampaign.ps1` と `Assets/Resources/Campaign/Story.json` を編集時の入力として使用。
- 本編用データは `Assets/Resources/StoryCampaign/`。12組のStoryQuestDefinition／MissionDefinition、32進行ステップ、既存形式の町・探索会話、5種類の用品を生成しています。補給物資の原品と代替品は同じ用品として扱います。
- `StoryCampaignRuntime` は既存のGameSessionManager、MissionManager、InventoryControllerを接続するアダプターです。
- 保存フラグは `story/` 接頭辞。既存のStoryFlags、所持金、インベントリ、ミッション保存経路を使用し、新しいセーブファイルは作成しません。
- `Tools > Story > Install into existing game` はデータを更新します。既にStoryIntegrationがあるシーンの配置は保持します。配置調整は各シーンのStoryIntegration配下で行ってください。
- 前段の独立Campaign試作シーン・コードは比較資料として残していますが、Build Settingsでは無効です。本編確認にはCampaignTownを使用しません。

## 仮設定と未検証事項

- 通常クエスト100G、結末500Gは脚本に金額指定がないための仮設定です。
- マップは既存地形・出入口・敵・施設を活用した配置です。完成版の美術改修ではありません。会話ポイントの見た目、UIの重なり、全地点までの手動移動、戦闘バランスは調整対象です。
- 任意の質問・状況別台詞の全分岐を移植したものではありません。進行に必要な主要経路と受注・断る・保留の選択を接続しています。
- フィンの救出済み状態は永続化しますが、未救出NPCの位置・負傷・死亡状態を新たに保存する処理は追加していません。既存シーンの再生成に従います。
- 既存stage1のItemBoxTutorialTriggerにMissing Scriptが残っています。今回のStoryIntegration内の参照検査と区別して警告します。
- Unityの非推奨API、既存のRopeVisual.OnValidate、商人のBox Kindなどの警告は今回の変更範囲外です。

## 自動検証

`StoryIntegrationTests.RunPlay` はUnity EditorのPlayモードで既存5シーンを順にロードして検証します。ユーザーのセーブファイルへ書き込まず、既存保存形式のメモリ内往復を確認します。
会話メニュー、前提条件拒否、満杯時の取得失敗、用品消費、実搬入コンポーネント、クエスト報酬、結末条件・重複防止を対象にします。搬送キーの手動操作や実機ビルドの検証ではありません。
仕様照合後の再生成・検証は `StoryIntegrationBuilder.ReconcileAndRunTests`。詳細ログは `Logs/StorySpecReview.log`。最新の実行結果を確認してください。

## 既存システム側で合わせて修正した点

- シーン再読み込み時、PlayerInventorySessionBridgeが破棄予定の重複GameSessionManagerを参照しないよう、稼働中のInstanceを優先します。
- 町の初期表示中の保存要求では、未復元の所持品を先に復元します。
- シーン破棄時にInventoryManagerが先に消えた場合、Player側の旧・空インベントリを再検索して保存しません。遷移直前の既存キャプチャを維持します。
- StoryProgressManagerの予備インスタンスは専用の子オブジェクトに置き、重複インスタンスの破棄が会話地点全体を巻き込まないようにしています。

### 実施結果（2026-10-20）

Unity 6000.4.10f1のPlayモードで `STORY PLAYTEST PASS` を確認しました。
- 13クエストの完了、町の既存会話メニュー、シーン間の用品保持・消費。
- メイン報告フラグの6通りの順序と、3件未満での庭の解放拒否。
- インベントリ満杯時に取得・進行を確定しないこと。
- 実際のElevatorCarryReceiverへの搬入、生存時のみの救出確定、搬入だけでは未確定であること。
- 既存JSON保存形式の往復後も協力条件・完了状態を保持すること。
- 結末A／B／Cの確定、Cの各協力欠落時の拒否、確定後の変更・重複報酬の拒否。

このテストはイベントAPIを操作する統合テストです。全経路をキー入力で踏破する手動プレイは未実施です。

仕様照合後の再実行でも `STORY SPEC RECONCILIATION PASS`、`STORY VALIDATION PASS`、`STORY PLAYTEST PASS` を確認しました（`Logs/StorySpecReview.log`）。9名分の現地会話の遷移先、町でのサブ受注・現地報告の拒否、救出後のフィン再出現防止、設置直後の部品再取得拒否、停戦範囲外・モンスターへの保護不適用も検証しています。

### 今回の仕様照合で更新したファイル

- `Assets/Script/GameRule/StoryCampaignDatabase.cs`、`StoryCampaignRuntime.cs`：部品取得の終了条件、受注場所の制限。
- `Assets/Script/GameRule/StoryNegotiationZone.cs`：人間同士の局所交渉とモンスター戦を区別。
- `Assets/Editor/StoryIntegrationBuilder.Data.cs`、`.Scenes.cs`、`.QuestLocations.cs`：脚本に沿った現地受注・報告会話、配置、フィンの表示情報。
- `Assets/Editor/StoryIntegrationTests.cs`：現地報告、会話参照、救出、部品、交渉区域の回帰検証。
- `Assets/Resources/StoryCampaign/`：上記に対応する会話・進行データ。
- `Assets/Scenes/Town_Main.unity`、`Assets/Scenes/stage/stage1.unity`、`Assets/Scenes/Doukutu_2.unity`、`Assets/Scenes/Doukutu_3.unity`：町の旧サブ依頼ボタンを無効化し、現地NPCと任務の案内先を設定。再生成処理は既存5シーンを保存します。
- `Docs/StoryCampaign.md`：操作案内・配置方針・検証結果。

手動確認はTown_Mainから出撃し、各階の代表者にEで話しかけ、依頼・保留・再読と報告選択肢を確認してください。フィンはTで既存の救急キットを開き、解毒剤で治療してから、既存の担ぎ操作でバルドのもとへ運びます。UI表示、NPCまでの実際の移動経路、運搬中の戦闘難度は手動確認が必要です。

追加地点の地面検査も完了しました（`STORY PLACEMENT PASS`）。Doukutu_2の水門とDoukutu_3の停戦交渉地点を既存足場上へ補正し、5シーンの参照検査を再実行しています。地面の存在を確認する検査であり、経路全体の手動踏破を保証するものではありません。

現地NPCを追加した後の地面検査も成功しました（`Logs/StorySpecPlacement.log` の `STORY VALIDATION PASS`、`STORY PLACEMENT PASS`）。この再検査では追加の位置補正は不要でした。
