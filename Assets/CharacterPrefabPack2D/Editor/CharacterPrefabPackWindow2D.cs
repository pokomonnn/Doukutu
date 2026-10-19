#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>独立したUnity Editor用18Prefab一括生成ツール。既存キャラPrefabは変更しない。</summary>
public sealed class CharacterPrefabPackWindow2D : EditorWindow
{
    private const string Root = "Assets/CharacterPrefabPack2D/Generated";
    private static readonly string[] RaceCodes={"SKEL","SLIME","DWF","ADV","CIV","BND"};
    private static readonly string[] RaceNames={"スケルトン","スライム","ドワーフ","冒険者","一般NPC","盗賊"};
    private static readonly string[] Factions={"monsters","monsters","dwarves","adventurers","civilians","bandits"};
    private static readonly Color[] Colors={
        new Color(.79f,.82f,.75f),new Color(.29f,.78f,.45f),new Color(.78f,.56f,.29f),
        new Color(.39f,.61f,.82f),new Color(.68f,.60f,.75f),new Color(.70f,.31f,.32f)};
    private static readonly string[] Modes={"近接","遠距離","商人_近接遠距離"};

    private Sprite[] sprites=new Sprite[6];
    private ItemData[,] itemFields=new ItemData[6,3];
    private bool createPlaceholderSprites=true;
    private bool replaceGeneratedPrefabs;
    private bool addNonMonsterDialogueActors=true;
    private LayerMask groundLayer;
    private LayerMask obstacleLayer;
    private float walkSpeed=1.55f;
    private int productAmount=4;
    private Vector2 scroll;
    private string lastReport;

    [MenuItem("Tools/2D Shooter/Character Studio/6種族×3タイプ Prefab一括作成")]
    public static void Open(){GetWindow<CharacterPrefabPackWindow2D>("6種族 Prefabメーカー").minSize=new Vector2(570,530);}

    private void OnEnable()
    {
        int ground=LayerMask.NameToLayer("Ground");
        groundLayer=ground>=0 ? 1<<ground : 0;
        int wall=LayerMask.NameToLayer("Ground");
        obstacleLayer=wall>=0 ? 1<<wall : 0;
    }

    private void OnGUI()
    {
        scroll=EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("Character Prefab Pack 2D — 6種族 / 18種類",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("STEP5＋600会話集 v1.5.0・STEP4共通商人UI v1.3.5・Character AI STEP4導入済みが前提です。既存Prefabは変更せず、Generatedフォルダへ新規作成します。",MessageType.Info);
        GUILayout.Label("① グラフィック",EditorStyles.boldLabel);
        createPlaceholderSprites=EditorGUILayout.ToggleLeft("未指定種族は仮ドットシルエットを自動生成",createPlaceholderSprites);
        for(int i=0;i<6;i++)sprites[i]=(Sprite)EditorGUILayout.ObjectField(RaceNames[i]+" Sprite",sprites[i],typeof(Sprite),false);
        GUILayout.Space(7);
        GUILayout.Label("② 商人の初期商品（各種族3種類まで・後からTradeProfileで追加可）",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("ItemDataが未選択なら空の店舗データを生成します。店の表示・購入テストには商品を指定してください。",MessageType.None);
        for(int i=0;i<6;i++)
        {
            EditorGUILayout.BeginHorizontal();GUILayout.Label(RaceNames[i],GUILayout.Width(88));
            for(int j=0;j<3;j++) itemFields[i,j]=(ItemData)EditorGUILayout.ObjectField(itemFields[i,j],typeof(ItemData),false);
            EditorGUILayout.EndHorizontal();
        }
        productAmount=EditorGUILayout.IntSlider("各商品の初期個数",productAmount,1,30);
        GUILayout.Space(7);
        GUILayout.Label("③ AIと地形",EditorStyles.boldLabel);
        groundLayer=LayerMaskField("Ground Layers",groundLayer);
        obstacleLayer=LayerMaskField("Obstacle Layers",obstacleLayer);
        walkSpeed=EditorGUILayout.Slider("徘徊速度",walkSpeed,.8f,3.5f);
        addNonMonsterDialogueActors=EditorGUILayout.ToggleLeft("人型にはDialogueActor2Dも付与（Eキー操作はOFF）",addNonMonsterDialogueActors);
        replaceGeneratedPrefabs=EditorGUILayout.ToggleLeft("既に同名の生成Prefabがある場合、上書きする（注意）",replaceGeneratedPrefabs);
        GUILayout.Space(8);
        if(GUILayout.Button("① 会話600件を検証・アセット作成",GUILayout.Height(37)))
        {
            if(CharacterPackDialogueBuilder2D.PrepareAssets(out string result))
            {lastReport=result;EditorUtility.DisplayDialog("台本登録完了",result,"OK");}
            else EditorUtility.DisplayDialog("台本登録失敗",result,"OK");
        }
        if(GUILayout.Button("② 18種類のPrefab・商人データ・遠距離弾を生成",GUILayout.Height(40)))
            Generate();
        if(GUILayout.Button("③ 生成したPrefabのフォルダを表示"))
        { var a=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Root);if(a!=null)EditorGUIUtility.PingObject(a); }
        if(!string.IsNullOrEmpty(lastReport))EditorGUILayout.HelpBox(lastReport,MessageType.Info);
        EditorGUILayout.HelpBox("要注意: モンスターの商人は敵対するままでは取引できません。派閥ルールで該当商人個体を中立化する等の調整が必要です。商人の会話はEキー単独取引を有効化。通常会話とEキー競合させない設定です。",MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    internal static void MakeFolders(string path)
    {
        string[] parts=path.Split('/');string p="Assets";
        for(int i=1;i<parts.Length;i++)
        { string n=p+"/"+parts[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(p,parts[i]);p=n; }
    }

    private static LayerMask LayerMaskField(string label,LayerMask layer)
    {
        var names=new List<string>();var numbers=new List<int>();
        for(int i=0;i<32;i++){string n=LayerMask.LayerToName(i);if(!string.IsNullOrEmpty(n)){names.Add(n);numbers.Add(i);}}
        int condensed=0;
        for(int i=0;i<numbers.Count;i++)if((layer.value&(1<<numbers[i]))!=0)condensed|=1<<i;
        condensed=EditorGUILayout.MaskField(label,condensed,names.ToArray());
        int expanded=0;for(int i=0;i<numbers.Count;i++)if((condensed&(1<<i))!=0)expanded|=1<<numbers[i];
        return expanded;
    }

    private void Generate()
    {
        if(!CharacterPackDialogueBuilder2D.PrepareAssets(out string dataReport))
        {EditorUtility.DisplayDialog("600台本の読み込みに失敗",dataReport,"OK");return;}
        if(!EditorUtility.DisplayDialog("18種類を作成","生成先："+Root+"\n既存の町・ダンジョンシーンやキャラクターPrefabは変更しません。\n同名の既存生成Prefab："+(replaceGeneratedPrefabs?"上書きする":"保持する")+"\n進めますか？","生成","キャンセル"))return;
        MakeFolders(Root);MakeFolders(Root+"/Prefabs");MakeFolders(Root+"/Projectiles");MakeFolders(Root+"/TradeProfiles");MakeFolders(Root+"/Sprites");
        int created=0,skipped=0,stockEmpty=0;
        try
        {
            GameObject projectile=CreateProjectile();
            for(int i=0;i<RaceCodes.Length;i++)
            {
                EditorUtility.DisplayProgressBar("18種族Prefab生成",RaceNames[i],(i+1f)/6);
                Sprite picture=sprites[i]!=null?sprites[i]:(createPlaceholderSprites?MakeSilhouette(i):null);
                for(int kind=0;kind<3;kind++)
                {
                    string prefabPath=Root+"/Prefabs/"+RaceCodes[i]+"_"+(kind==0?"Melee":kind==1?"Ranged":"MerchantHybrid")+".prefab";
                    if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)!=null && !replaceGeneratedPrefabs){skipped++;continue;}
                    if(kind==2 && !HasProducts(i))stockEmpty++;
                    BuildOne(i,kind,picture,projectile,prefabPath);
                    created++;
                }
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            lastReport=dataReport+"\nPrefab生成:"+created+"体 / 既存のためスキップ:"+skipped+"体 / 空の商品リスト:"+stockEmpty+"商人";
            EditorUtility.DisplayDialog("Prefab作成完了",lastReport+"\n地面Layer/砲弾の命中・派閥ルールはPlayテストで調整してください。","OK");
        }
        catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("生成中にエラー",e.ToString(),"OK");}
        finally{EditorUtility.ClearProgressBar();}
    }
    private bool HasProducts(int race)
    { for(int j=0;j<3;j++)if(itemFields[race,j]!=null)return true;return false; }

    private static void Set(MonoBehaviour mb,string field,object value)
    {
        var so=new SerializedObject(mb);var p=so.FindProperty(field);
        if(p==null){Debug.LogWarning("[CharacterPrefabPack] 未対応フィールド: "+mb.GetType().Name+"."+field);return;}
        if(value is UnityEngine.Object o)p.objectReferenceValue=o;
        else if(value is bool b)p.boolValue=b;
        else if(value is int n)p.intValue=n;
        else if(value is float f)p.floatValue=f;
        else if(value is string s)p.stringValue=s;
        else if(value is Color c)p.colorValue=c;
        else if(value is Vector2 v)p.vector2Value=v;
        else if(value is Vector3 vv)p.vector3Value=vv;
        else if(value is LayerMask lm)p.intValue=lm.value;
        else if(value is Enum en)
        {
            if(p.propertyType==SerializedPropertyType.Enum)p.enumValueIndex=Convert.ToInt32(en);
            else p.intValue=Convert.ToInt32(en);
        }
        else throw new ArgumentException("未対応の値:"+field+" / "+value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void BuildOne(int race,int kind,Sprite picture,GameObject projectile,string path)
    {
        string displayName=RaceNames[race]+(kind==0?"・近接":kind==1?"・遠距離":"・商人（近接＋遠距離）");
        GameObject root=new GameObject(RaceCodes[race]+"_"+(kind==0?"Melee":kind==1?"Ranged":"MerchantHybrid"));
        try
        {
            root.layer=LayerMask.NameToLayer("Enemy")>=0?LayerMask.NameToLayer("Enemy"):0;
            var renderer=root.AddComponent<SpriteRenderer>(); renderer.sprite=picture;renderer.sortingOrder=3;
            var rb=root.AddComponent<Rigidbody2D>();rb.gravityScale=2f;rb.freezeRotation=true;
            var box=root.AddComponent<BoxCollider2D>();box.size=new Vector2(.76f,1.35f);box.offset=new Vector2(0f,.23f);
            var hp=root.AddComponent<CharacterHealth>();Set(hp,"maxHealth",race==0?85:race==1?65:race==2?110:100);
            var faction=root.AddComponent<FactionMember2D>();
            // モンスター商人は通常の「常に敵対」派閥ではE取引が難しいため、
            // 見た目/種族設定は保持して中立の商人派閥へ切り替える。
            // 一般敵スケルトン/スライムは通常どおりmonsters。
            string factionId = kind==2 && race<2 ? "monster_traders" : Factions[race];
            faction.Configure(factionId,race==0?FactionMonsterType2D.Skeleton:race==1?FactionMonsterType2D.Slime:FactionMonsterType2D.None);
            var identityTag=root.AddComponent<CharacterPackRaceIdentity2D>();
            identityTag.EditorConfigure(RaceCodes[race],race<2);
            root.AddComponent<FactionEnemyTargeting2D>();
            var wander=root.AddComponent<CharacterWander2D>();
            Set(wander,"mode",kind==2?CharacterWanderMode2D.Idle:CharacterWanderMode2D.RandomWander);
            Set(wander,"moveSpeed",walkSpeed);Set(wander,"groundLayers",groundLayer);Set(wander,"obstacleLayers",obstacleLayer);
            Set(wander,"spriteRenderer",renderer);Set(wander,"minWaitSeconds",2f);Set(wander,"maxWaitSeconds",5f);
            var personality=root.AddComponent<CharacterAIPersonality2D>();
            Set(personality,"personalityType",race==0?CharacterAIPersonalityType2D.Brave:
                race==1?CharacterAIPersonalityType2D.Balanced:race==2?CharacterAIPersonalityType2D.Cautious:
                race==3?CharacterAIPersonalityType2D.Brave:race==4?CharacterAIPersonalityType2D.Cowardly:CharacterAIPersonalityType2D.Aggressive);
            var chaser=root.AddComponent<EnemyChaser2D>();
            Set(chaser,"detectionDistance",7.5f);Set(chaser,"loseTargetDistance",10f);
            Set(chaser,"moveSpeed",walkSpeed+0.45f);Set(chaser,"groundLayers",groundLayer);
            Set(chaser,"obstacleLayers",obstacleLayer);Set(chaser,"jumpObstacleLayers",obstacleLayer);
            Set(chaser,"cliffCheckLayers",groundLayer);Set(chaser,"spriteRenderer",renderer);
            Set(chaser,"combatMovementStyle",kind==0?EnemyCombatMovementStyle.MeleeChase:EnemyCombatMovementStyle.KeepDistance);
            Set(chaser,"enableBuiltInMeleeAttack",kind!=1);
            Set(chaser,"attackDistance",1.1f);Set(chaser,"attackDamage",race==0?9:race==1?7:race==2?11:8);
            Set(chaser,"attackInterval",1.4f);
            if(kind!=0)
            {
                var spawn=new GameObject("ProjectileSpawnPoint");spawn.transform.SetParent(root.transform,false);
                spawn.transform.localPosition=new Vector3(.35f,.65f,0);
                var ranged=root.AddComponent<EnemyRangedAttack2D>();
                Set(ranged,"enemyChaser",chaser);Set(ranged,"ownHealth",hp);
                Set(ranged,"projectileSpawnPoint",spawn.transform);Set(ranged,"projectilePrefab",projectile);
                Set(ranged,"minimumAttackDistance",1.2f);Set(ranged,"maximumAttackDistance",7.5f);
                Set(ranged,"useBallisticArc",false);Set(ranged,"projectileSpeed",race==1?7.5f:11f);
                Set(ranged,"attackInterval",2f);Set(ranged,"obstacleLayers",obstacleLayer);
            }
            var actor=addNonMonsterDialogueActors || race<2 ? root.AddComponent<DialogueActor2D>() : null;
            // 自動会話と商人Eキーの競合を防ぐため、通常のDialogueActorはEキーを使わない。
            if(actor!=null)Set(actor,"useEKeyInteraction",false);
            var talk=root.AddComponent<DialogueAmbientTalk2D>();
            var solo=root.AddComponent<Dialogue600SoloSpeaker2D>();
            string group=race==0?(kind==2?"SKEL_MERCHANT":"SKEL"):
                race==1?(kind==2?"SLIME_MERCHANT":"SLIME"):
                race==2?"DWF":race==3?"ADV":race==4?"CIV_GENERAL":"BND";
            string dialogPath=race<2?CharacterPackDialogueBuilder2D.CustomPath:CharacterPackDialogueBuilder2D.ExistingPath;
            string ambientName=race<2?"AMB_NEW_"+group:"AMB600_"+group;
            string soloName=race<2?"SOLO_NEW_"+group:"SOLO600_"+(race==4?"CIV":group);
            var ambient=AssetDatabase.LoadAssetAtPath<DialogueAmbientTalkData2D>(dialogPath+"/"+ambientName+".asset");
            var solos=AssetDatabase.LoadAssetAtPath<Dialogue600SoloLibrary2D>(dialogPath+"/"+soloName+".asset");
            Set(talk,"additionalDialogue600TalkData",ambient);
            Set(solo,"regularLibrary",solos);
            if(race==3||race==5)
            {
                var hostile=AssetDatabase.LoadAssetAtPath<Dialogue600SoloLibrary2D>(CharacterPackDialogueBuilder2D.ExistingPath+"/SOLO600_HOST_"+group+".asset");
                Set(solo,"hostileLibrary",hostile);
            }
            Set(talk,"minimumRestSeconds",120f);Set(talk,"maximumRestSeconds",240f);
            Set(talk,"startProbability",.46f);Set(talk,"scanInterval",4.5f);
            Set(talk,"allowDifferentNonHostileFactions",race>=2);
            if(kind==2)
            {
                DungeonNpcTradeProfile2D trade=CreateTradeProfile(race);
                var identity=root.AddComponent<DungeonNpcIdentity2D>();
                Set(identity,"npcId","shop_"+RaceCodes[race].ToLowerInvariant());
                Set(identity,"displayName",displayName);Set(identity,"occupation","行商人");
                Set(identity,"tradeProfile",trade);
                var stockGO=new GameObject("MerchantStock");stockGO.transform.SetParent(root.transform,false);
                var boxInv=stockGO.AddComponent<ItemBoxInventory>();
                Set(boxInv,"boxKind",ItemBoxKind.Shop);Set(boxInv,"boxDisplayName",displayName+"の商品棚");
                Set(boxInv,"gridWidth",12);Set(boxInv,"gridHeight",12);
                Set(boxInv,"addStartingItemsOnAwake",false);
                var merchantStock=stockGO.AddComponent<MerchantStockInventory>();
                Set(merchantStock,"stockInventory",boxInv);Set(merchantStock,"dungeonTradeProfile",trade);
                var shop=root.AddComponent<DungeonNpcShop2D>();
                Set(shop,"tradeProfile",trade);Set(shop,"useSharedTownTradeUI",true);
                Set(shop,"interactionKey",KeyCode.None);
                Set(shop,"sharedMerchantStock",merchantStock);
                // Scene内の共通売買画面はPrefabから参照できないので、起動時に配線する。
                root.AddComponent<CharacterPackMerchantAutoWire2D>();
            }
            // Unity側でPrefabを作成するので、スクリプトGUIDと参照がUnityによって設定される。
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    private DungeonNpcTradeProfile2D CreateTradeProfile(int race)
    {
        string path=Root+"/TradeProfiles/Trade_"+RaceCodes[race]+".asset";
        var profile=AssetDatabase.LoadAssetAtPath<DungeonNpcTradeProfile2D>(path);
        if(profile!=null)
        {
            // 既存のショップ在庫マスタは生成Prefabを上書きしても上書きしない。
            return profile;
        }
        profile=ScriptableObject.CreateInstance<DungeonNpcTradeProfile2D>();
        profile.buyAnyItem=true;profile.sellPriceMultiplier=.5f;
        profile.startingNpcMoney=-1;
        var used=new HashSet<ItemData>();
        for(int j=0;j<3;j++)if(itemFields[race,j]!=null&&used.Add(itemFields[race,j]))
            profile.products.Add(new DungeonNpcTradeProfile2D.Product{item=itemFields[race,j],stock=productAmount,priceOverride=-1});
        AssetDatabase.CreateAsset(profile,path);
        return profile;
    }

    private static GameObject CreateProjectile()
    {
        string path=Root+"/Projectiles/PrefabPack_Projectile.prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(existing!=null)return existing;
        GameObject root=new GameObject("PrefabPack_Projectile");
        try
        {
            var sr=root.AddComponent<SpriteRenderer>();sr.sprite=MakeProjectileSprite();sr.sortingOrder=6;
            var rb=root.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var col=root.AddComponent<CircleCollider2D>();col.radius=.11f;col.isTrigger=true;
            var damage=root.AddComponent<DamageDealer>();
            Set(damage,"damage",7);
            // 敵/味方の防止はDamageDealer.ConfigureShooter()が担当する。
            // Player含む全レイヤーを対象にし、派閥無関係の自傷/味方誤射を避ける。
            Set(damage,"targetLayers",(LayerMask)(~0));
            PrefabUtility.SaveAsPrefabAsset(root,path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    private static Sprite MakeProjectileSprite()
    {
        string path=Root+"/Sprites/Projectile_Dot.png";
        if(!File.Exists(path))
        {
            var tex=new Texture2D(16,16,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Point;
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
            {float d=((x-7.5f)*(x-7.5f)+(y-7.5f)*(y-7.5f));tex.SetPixel(x,y,d<28f?new Color(1,.87f,.34f,1):Color.clear);}
            tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;
            imp.spritePixelsPerUnit=16;imp.filterMode=FilterMode.Point;imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Sprite MakeSilhouette(int race)
    {
        string path=Root+"/Sprites/Temporary_"+RaceCodes[race]+".png";
        if(!File.Exists(path))
        {
            var tex=new Texture2D(32,48,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Point;
            for(int y=0;y<48;y++)for(int x=0;x<32;x++)
            {
                float u=(x-15.5f)/16f,v=(y-22f)/23f;
                bool on=false;
                if(race==1) on=v<0.5f && v>-0.8f && ((u*u/.80f)+(v*v/1.3f)<1f ||(Mathf.Abs(u)<.7f && v<-.3f));
                else if(race==0)on=(v>.29f && u*u+(v-.52f)*(v-.52f)<.15f) ||(v<.16f&&v>-.58f&&Mathf.Abs(u)<.24f)||(v<-.45f&&v>-.94f&&Mathf.Abs(Mathf.Abs(u)-.17f)<.105f);
                else
                {
                    float head=v-.41f;
                    on=(u*u/.16f+head*head/.11f<1f)||(Mathf.Abs(u)<(race==2?.50f:.39f)&&v<.15f&&v>-.5f)||
                        (Mathf.Abs(Mathf.Abs(u)-.19f)<.16f && v<=-.42f && v>-.96f);
                    if(race==2&&v<.34f&&v>.10f&&Mathf.Abs(u)<.42f)on=true;
                }
                Color c=on?Colors[race]:Color.clear;
                if(on&&v>.30f&&Mathf.Abs(u)<.08f)c=Color.Lerp(c,Color.black,.13f);
                tex.SetPixel(x,y,c);
            }
            tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;
            imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
