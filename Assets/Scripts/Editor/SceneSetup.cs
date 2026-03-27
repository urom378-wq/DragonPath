#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 【エディター専用】DragonPath ゲームシーン自動構築ツール
/// メニュー: Tools > DragonPath > Setup Game Scene
/// </summary>
public static class SceneSetup
{
    [MenuItem("Tools/DragonPath/🐉 Setup Game Scene", priority = 1)]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog("DragonPath Scene Setup",
            "現在のシーンにゲームオブジェクトを自動生成します。\n既存のオブジェクトは上書きされません。\n\n続行しますか？", "はい", "キャンセル"))
            return;

        RunSetup();
    }

    /// <summary>
    /// バッチモード実行用エントリーポイント（ダイアログなし）
    /// Unity -batchmode -executeMethod SceneSetup.BatchSetupScene
    /// </summary>
    public static void BatchSetupScene()
    {
        Debug.Log("[DragonPath] === バッチセットアップ開始 ===");

        // バッチモードでシーンを明示的に開く
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        RunSetup();

        // シーンを保存
        bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log(saved
            ? $"[DragonPath] ✅ シーン保存完了: {scenePath}"
            : "[DragonPath] ⚠️ シーンの保存に失敗しました");
    }

    private static void RunSetup()
    {
        CreateManagers();
        CreateTerrain();
        CreatePlayer();
        CreateCamera();
        CreateEnemies();
        CreateEnvironment();
        CreateUI();

        Debug.Log("[DragonPath] ✅ シーンのセットアップが完了しました！\n" +
                  "次のステップ:\n" +
                  "1. NavMesh を Bake する (Window > AI > Navigation)\n" +
                  "2. アニメーターコントローラーをプレイヤー/敵に割り当てる\n" +
                  "3. Audio Clips を AudioManager にアサインする");
    }

    // ============================================================
    // Managers
    // ============================================================
    private static void CreateManagers()
    {
        var managers = new GameObject("--- MANAGERS ---");

        // GameManager
        var gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        gm.transform.SetParent(managers.transform);

        // AudioManager
        var am = new GameObject("AudioManager");
        am.AddComponent<AudioManager>();
        am.transform.SetParent(managers.transform);

        // SaveSystem
        var ss = new GameObject("SaveSystem");
        ss.AddComponent<SaveSystem>();
        ss.transform.SetParent(managers.transform);

        Debug.Log("[DragonPath] ✅ Managers 生成完了");
    }

    // ============================================================
    // Terrain / Ground
    // ============================================================
    private static void CreateTerrain()
    {
        // 地面
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        ground.transform.position   = Vector3.zero;
        ground.tag = "Ground";
        ground.layer = LayerMask.NameToLayer("Default");

        // NavMesh Surface コンポーネント追加（AI Navigation パッケージ必要）
        // ground.AddComponent<NavMeshSurface>();

        // 壁（境界）
        CreateWall(new Vector3(  0f, 2f, 100f), new Vector3(200f, 4f,   1f), "Wall_N");
        CreateWall(new Vector3(  0f, 2f,-100f), new Vector3(200f, 4f,   1f), "Wall_S");
        CreateWall(new Vector3( 100f, 2f,  0f), new Vector3(  1f, 4f, 200f), "Wall_E");
        CreateWall(new Vector3(-100f, 2f,  0f), new Vector3(  1f, 4f, 200f), "Wall_W");

        // 岩・障害物
        for (int i = 0; i < 20; i++)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{i}";
            rock.transform.position   = new Vector3(
                Random.Range(-80f, 80f), 1f, Random.Range(-80f, 80f));
            rock.transform.localScale = Vector3.one * Random.Range(1.5f, 3.5f);
            rock.transform.rotation   = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var r = rock.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = CreateColorMaterial(new Color(0.45f, 0.4f, 0.35f));
        }

        // 木
        for (int i = 0; i < 30; i++)
        {
            float x = Random.Range(-90f, 90f);
            float z = Random.Range(-90f, 90f);
            if (Mathf.Abs(x) < 10f && Mathf.Abs(z) < 10f) continue; // スポーン周囲は除く

            var trunk  = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = $"Tree_{i}_Trunk";
            trunk.transform.position   = new Vector3(x, 2f, z);
            trunk.transform.localScale  = new Vector3(0.5f, 2f, 0.5f);
            var tr = trunk.GetComponent<Renderer>();
            if (tr != null) tr.sharedMaterial = CreateColorMaterial(new Color(0.4f, 0.25f, 0.1f));

            var leaves  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = $"Tree_{i}_Leaves";
            leaves.transform.position   = new Vector3(x, 5.5f, z);
            leaves.transform.localScale  = Vector3.one * 3f;
            leaves.GetComponent<Collider>().enabled = false;
            var lr = leaves.GetComponent<Renderer>();
            if (lr != null) lr.sharedMaterial = CreateColorMaterial(new Color(0.15f, 0.5f, 0.15f));
        }

        Debug.Log("[DragonPath] ✅ Terrain 生成完了");
    }

    private static void CreateWall(Vector3 pos, Vector3 scale, string name)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position   = pos;
        wall.transform.localScale = scale;
        var r = wall.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = CreateColorMaterial(new Color(0.3f, 0.3f, 0.35f));
    }

    // ============================================================
    // Player
    // ============================================================
    private static void CreatePlayer()
    {
        var player = new GameObject("Player");
        player.tag    = "Player";
        player.layer  = 0;
        player.transform.position = new Vector3(0f, 1f, 0f);

        // 仮ビジュアル（カプセル）
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale    = new Vector3(0.8f, 1f, 0.8f);
        Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
        var vr = visual.GetComponent<Renderer>();
        if (vr != null) vr.sharedMaterial = CreateColorMaterial(new Color(0.2f, 0.5f, 0.9f));

        // 武器（剣）
        var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sword.name = "Sword";
        sword.transform.SetParent(player.transform);
        sword.transform.localPosition = new Vector3(0.6f, 0.1f, 0.4f);
        sword.transform.localScale    = new Vector3(0.1f, 0.8f, 0.1f);
        sword.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
        Object.DestroyImmediate(sword.GetComponent<Collider>());
        var sr = sword.GetComponent<Renderer>();
        if (sr != null) sr.sharedMaterial = CreateColorMaterial(new Color(0.8f, 0.8f, 0.9f));

        // Components
        var cc   = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        var stats  = player.AddComponent<PlayerStats>();
        var ctrl   = player.AddComponent<PlayerController>();
        var combat = player.AddComponent<PlayerCombat>();
        var inv    = player.AddComponent<Inventory>();

        // Animator（後で手動割り当て）
        player.AddComponent<Animator>();

        // Enemy layer mask（後で設定）
        combat.enemyLayer = LayerMask.GetMask("Enemy");
        ctrl.enemyLayer   = LayerMask.GetMask("Enemy");

        Debug.Log("[DragonPath] ✅ Player 生成完了");
    }

    // ============================================================
    // Camera
    // ============================================================
    private static void CreateCamera()
    {
        var cam = Camera.main?.gameObject ?? new GameObject("Main Camera");
        cam.tag = "MainCamera";
        cam.transform.position = new Vector3(0f, 5f, -10f);
        cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        if (cam.GetComponent<Camera>() == null) cam.AddComponent<Camera>();

        var tpc = cam.GetComponent<ThirdPersonCamera>() ?? cam.AddComponent<ThirdPersonCamera>();

        // Player の CameraTarget を自動設定
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var pivot = new GameObject("CameraTarget");
            pivot.transform.SetParent(player.transform);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            tpc.target = pivot.transform;

            var ctrl = player.GetComponent<PlayerController>();
            if (ctrl != null) ctrl.camController = tpc;
        }

        tpc.collisionMask = LayerMask.GetMask("Default");

        // Directional Light
        var light = new GameObject("Directional Light");
        var dl    = light.AddComponent<Light>();
        dl.type      = LightType.Directional;
        dl.intensity = 1.2f;
        dl.color     = new Color(1f, 0.95f, 0.85f);
        dl.shadows   = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(45f, 60f, 0f);

        Debug.Log("[DragonPath] ✅ Camera 生成完了");
    }

    // ============================================================
    // Enemies
    // ============================================================
    private static void CreateEnemies()
    {
        var enemyParent = new GameObject("--- ENEMIES ---");

        // 通常敵 × 6
        Vector3[] positions = {
            new Vector3( 20f, 1f,  15f),
            new Vector3(-18f, 1f,  22f),
            new Vector3( 30f, 1f, -20f),
            new Vector3(-25f, 1f, -30f),
            new Vector3( 45f, 1f,  10f),
            new Vector3(-40f, 1f,  40f),
        };

        foreach (var pos in positions)
        {
            var enemy = CreateEnemyObject($"Enemy_{pos.x}", pos);
            enemy.transform.SetParent(enemyParent.transform);
        }

        // ドラゴンボス（ステージ奥）
        CreateDragonBoss(new Vector3(0f, 1f, 80f), enemyParent.transform);

        Debug.Log("[DragonPath] ✅ Enemies 生成完了");
    }

    private static GameObject CreateEnemyObject(string name, Vector3 position)
    {
        var enemy = new GameObject(name);
        enemy.layer = LayerMask.NameToLayer("Enemy");
        enemy.transform.position = position;

        // 仮ビジュアル
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(enemy.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale    = new Vector3(0.9f, 0.9f, 0.9f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        var vr = visual.GetComponent<Renderer>();
        if (vr != null) vr.sharedMaterial = CreateColorMaterial(new Color(0.8f, 0.2f, 0.2f));

        var cc = enemy.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        enemy.AddComponent<NavMeshAgent>();
        enemy.AddComponent<EnemyAI>();
        enemy.AddComponent<Animator>();

        return enemy;
    }

    private static void CreateDragonBoss(Vector3 position, Transform parent)
    {
        var boss = new GameObject("DragonBoss_Valdras");
        boss.layer = LayerMask.NameToLayer("Enemy");
        boss.transform.position = position;
        boss.transform.localScale = new Vector3(3f, 3f, 3f);

        // 仮ビジュアル（大きな敵）
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(boss.transform);
        body.transform.localPosition = Vector3.zero;
        Object.DestroyImmediate(body.GetComponent<Collider>());
        var br = body.GetComponent<Renderer>();
        if (br != null) br.sharedMaterial = CreateColorMaterial(new Color(0.3f, 0.05f, 0.05f));

        var cc = boss.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.6f;
        cc.center = new Vector3(0f, 1f, 0f);

        var agent = boss.AddComponent<NavMeshAgent>();
        agent.radius = 1.5f;
        agent.height = 3f;

        var bossAI    = boss.AddComponent<DragonBossAI>();
        bossAI.maxHP  = 1500f;
        bossAI.defense = 15f;
        bossAI.xpReward = 500;

        boss.AddComponent<Animator>();
        boss.transform.SetParent(parent);

        // ボスアリーナ（床を変える）
        var arena = GameObject.CreatePrimitive(PrimitiveType.Plane);
        arena.name = "BossArena";
        arena.transform.position   = new Vector3(0f, 0.01f, 80f);
        arena.transform.localScale  = new Vector3(5f, 1f, 5f);
        var ar = arena.GetComponent<Renderer>();
        if (ar != null) ar.sharedMaterial = CreateColorMaterial(new Color(0.2f, 0.1f, 0.1f));
    }

    // ============================================================
    // Environment (Chests / NPC)
    // ============================================================
    private static void CreateEnvironment()
    {
        var envParent = new GameObject("--- ENVIRONMENT ---");

        // 宝箱 × 4
        Vector3[] chestPositions = {
            new Vector3( 15f, 0.5f,   5f),
            new Vector3(-20f, 0.5f,  -5f),
            new Vector3(  5f, 0.5f, -30f),
            new Vector3(-10f, 0.5f,  40f),
        };

        foreach (var pos in chestPositions)
        {
            var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "TreasureChest";
            chest.transform.position   = pos;
            chest.transform.localScale  = new Vector3(1f, 0.7f, 0.6f);
            chest.AddComponent<TreasureChest>();
            chest.transform.SetParent(envParent.transform);
            var cr = chest.GetComponent<Renderer>();
            if (cr != null) cr.sharedMaterial = CreateColorMaterial(new Color(0.6f, 0.45f, 0.15f));
        }

        // NPC（村人）
        var npc = new GameObject("NPC_Villager");
        npc.transform.position = new Vector3(-8f, 1f, -8f);
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.SetParent(npc.transform);
        capsule.transform.localPosition = Vector3.zero;
        capsule.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(new Color(0.9f, 0.8f, 0.6f));

        var npcCtrl = npc.AddComponent<NPCController>();
        npcCtrl.npcName = "老賢者ガルド";
        npcCtrl.dialogueLines = new string[]
        {
            "やあ、旅人よ。この地には古の龍が眠るという。",
            "北の荒野を越えた先、「竜ヶ峰」にドラゴンが棲んでいる。",
            "くれぐれも気を付けるがよい… 奴は一筋縄ではいかない強敵じゃ。",
            "道中の宝箱も見逃すな。きっと役に立つものが眠っておる。",
            "剣を持って、勇気を持って。武運を祈る！"
        };
        npc.transform.SetParent(envParent.transform);

        Debug.Log("[DragonPath] ✅ Environment 生成完了");
    }

    // ============================================================
    // UI Canvas
    // ============================================================
    private static void CreateUI()
    {
        var uiGo = new GameObject("--- UI ---");

        // ===== Main Canvas =====
        var canvas = CreateCanvas("GameCanvas", uiGo.transform);

        // UIManager
        var uiMgr = new GameObject("UIManager");
        uiMgr.transform.SetParent(uiGo.transform);
        uiMgr.AddComponent<UIManager>();

        // HUD Panel
        CreateHUDPanel(canvas);

        // Pause Panel
        CreatePausePanel(canvas);

        // GameOver Panel
        CreateSimpleOverlayPanel(canvas, "GameOverPanel", "GAME OVER",
            "リスタート", "終了", new Color(0f, 0f, 0f, 0.85f), Color.red);

        // Victory Panel
        CreateSimpleOverlayPanel(canvas, "VictoryPanel", "ドラゴン討伐！\n勝利！",
            "リスタート", "終了", new Color(0f, 0.05f, 0.1f, 0.85f), Color.yellow);

        // Damage Vignette
        var vignette = new GameObject("DamageVignette");
        vignette.transform.SetParent(canvas.transform);
        var vigImg = vignette.AddComponent<Image>();
        vigImg.color = new Color(0.8f, 0f, 0f, 0f);
        var vigRect = vignette.GetComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.offsetMin = Vector2.zero;
        vigRect.offsetMax = Vector2.zero;

        // Fade Panel
        var fade = new GameObject("FadePanel");
        fade.transform.SetParent(canvas.transform);
        var fadeImg = fade.AddComponent<Image>();
        fadeImg.color = Color.black;
        var fadeRect = fade.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fade.AddComponent<CanvasGroup>();

        // Notification
        CreateNotification(canvas);

        // Dialogue UI
        CreateDialoguePanel(canvas, uiGo.transform);

        // Inventory UI
        CreateInventoryPanel(canvas, uiGo.transform);

        Debug.Log("[DragonPath] ✅ UI 生成完了");
    }

    private static Canvas CreateCanvas(string name, Transform parent)
    {
        var go     = new GameObject(name);
        go.transform.SetParent(parent);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode     = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder   = 0;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateHUDPanel(Canvas parent)
    {
        var hud = CreatePanel(parent.transform, "HUDPanel");
        hud.color = new Color(0, 0, 0, 0);
        hud.gameObject.AddComponent<HUDController>();

        // HP Bar
        CreateBar(hud.transform, "HPBar",   new Vector2(-300f, -200f), Color.red,    "HP");
        // MP Bar
        CreateBar(hud.transform, "MPBar",   new Vector2(-300f, -230f), Color.blue,   "MP");
        // Stamina Bar
        CreateBar(hud.transform, "StaminaBar", new Vector2(-300f, -260f), Color.yellow, "STM");

        // Level Text
        var lvGo  = new GameObject("LevelText");
        lvGo.transform.SetParent(hud.transform);
        var lvText = lvGo.AddComponent<TextMeshProUGUI>();
        lvText.text      = "Lv. 1";
        lvText.fontSize  = 20;
        lvText.color     = Color.yellow;
        var lvRect = lvGo.GetComponent<RectTransform>();
        lvRect.anchorMin = new Vector2(0f, 1f);
        lvRect.anchorMax = new Vector2(0f, 1f);
        lvRect.pivot     = new Vector2(0f, 1f);
        lvRect.anchoredPosition = new Vector2(20f, -20f);
        lvRect.sizeDelta = new Vector2(200f, 30f);
    }

    private static void CreateBar(Transform parent, string name, Vector2 pos, Color color, string label)
    {
        var bg = new GameObject($"{name}_BG");
        bg.transform.SetParent(parent);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 1f);
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot     = new Vector2(0f, 1f);
        bgRect.anchoredPosition = new Vector2(20f, pos.y);
        bgRect.sizeDelta        = new Vector2(200f, 18f);

        var fill = new GameObject($"{name}_Fill");
        fill.transform.SetParent(parent);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = color;
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin      = bgRect.anchorMin;
        fillRect.anchorMax      = bgRect.anchorMax;
        fillRect.pivot          = bgRect.pivot;
        fillRect.anchoredPosition = bgRect.anchoredPosition;
        fillRect.sizeDelta      = bgRect.sizeDelta;
    }

    private static void CreatePausePanel(Canvas parent)
    {
        var panel = CreatePanel(parent.transform, "PausePanel");
        panel.color = new Color(0f, 0f, 0f, 0.8f);
        var cg    = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha  = 0f;

        AddCenteredText(panel.transform, "PauseTitle", "PAUSE", 48, Color.white, new Vector2(0f, 150f));
        AddButton(panel.transform, "ResumeBtn",  "ゲームに戻る", new Vector2(0f,   50f));
        AddButton(panel.transform, "RestartBtn", "リスタート",   new Vector2(0f,  -20f));
        AddButton(panel.transform, "QuitBtn",    "終了",         new Vector2(0f,  -90f));
    }

    private static void CreateSimpleOverlayPanel(Canvas parent, string name,
        string title, string btn1, string btn2, Color bgColor, Color titleColor)
    {
        var panel = CreatePanel(parent.transform, name);
        panel.color = bgColor;
        var cg   = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        AddCenteredText(panel.transform, "Title", title, 64, titleColor, new Vector2(0f, 120f));
        AddCenteredText(panel.transform, "Stats", "",    24, Color.white, new Vector2(0f,  20f));
        AddButton(panel.transform, "Btn1", btn1, new Vector2(-80f, -100f));
        AddButton(panel.transform, "Btn2", btn2, new Vector2( 80f, -100f));
    }

    private static void CreateNotification(Canvas parent)
    {
        var go   = new GameObject("Notification");
        go.transform.SetParent(parent.transform);

        // Image を先に追加することで RectTransform が自動生成される
        var bg   = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);

        var cg   = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -60f);
        rect.sizeDelta        = new Vector2(400f, 50f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform);
        var tmp    = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text       = "";
        tmp.fontSize   = 18;
        tmp.alignment  = TextAlignmentOptions.Center;
        tmp.color      = Color.white;
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(10f, 5f);
        tr.offsetMax = new Vector2(-10f, -5f);
    }

    private static void CreateDialoguePanel(Canvas parent, Transform uiParent)
    {
        var panel = CreatePanel(parent.transform, "DialoguePanel");
        panel.color = new Color(0f, 0f, 0f, 0.85f);
        var cg   = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.3f);

        AddCenteredText(panel.transform, "SpeakerName", "???", 24, Color.yellow, new Vector2(0f, 80f));
        AddCenteredText(panel.transform, "DialogueText", "...", 18, Color.white,  new Vector2(0f,  0f));

        var dialogueUI  = panel.gameObject.AddComponent<DialogueUI>();
        dialogueUI.dialoguePanel = cg;
    }

    private static void CreateInventoryPanel(Canvas parent, Transform uiParent)
    {
        var panel = CreatePanel(parent.transform, "InventoryPanel");
        panel.color = new Color(0f, 0f, 0f, 0.9f);
        var cg   = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        AddCenteredText(panel.transform, "Title", "インベントリ (TAB: 閉じる)", 28, Color.white, new Vector2(0f, 240f));
        panel.gameObject.AddComponent<InventoryUI>();
    }

    // ============================================================
    // Helpers
    // ============================================================
    private static Image CreatePanel(Transform parent, string name)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent);
        var img  = go.AddComponent<Image>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return img;
    }

    private static void AddCenteredText(Transform parent, string name, string text, int size, Color color, Vector2 pos)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent);
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin      = new Vector2(0.5f, 0.5f);
        rect.anchorMax      = new Vector2(0.5f, 0.5f);
        rect.pivot          = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta      = new Vector2(600f, 80f);
    }

    private static void AddButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent);
        var img  = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        go.AddComponent<Button>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta        = new Vector2(200f, 50f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform);
        var tmp    = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 20;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
    }

    private static Material CreateColorMaterial(Color color)
    {
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }

    // ============================================================
    [MenuItem("Tools/DragonPath/📋 Setup Instructions", priority = 2)]
    public static void ShowInstructions()
    {
        EditorUtility.DisplayDialog("DragonPath セットアップ手順",
            "【自動生成後の手順】\n\n" +
            "1. NavMesh のベイク\n" +
            "   Window > AI > Navigation\n" +
            "   Agents/Areas タブで設定 → Bake\n\n" +
            "2. 「Enemy」レイヤーの作成\n" +
            "   Edit > Project Settings > Tags and Layers\n\n" +
            "3. AudioManager にクリップをアサイン\n" +
            "   （BGM・SE 各フィールドへ）\n\n" +
            "4. アニメーターコントローラーの設定\n" +
            "   Player / Enemy に Animator Controller をアサイン\n\n" +
            "5. UI 参照の確認\n" +
            "   UIManager / HUDController の参照を手動接続\n\n" +
            "6. TextMeshPro のインポート確認\n" +
            "   Window > TextMeshPro > Import TMP Essential Resources",
            "OK");
    }
}
#endif
