using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 4x4 타일맵을 만들고 플레이어, 카메라, 아이템, UI를 Main 씬에 배치한다.
/// 메뉴: Tools > Voxel > Build Level
/// </summary>
public static class LevelBuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string TilePrefabPath = "Assets/Prefabs/Tile.prefab";
    const string MaterialDir = "Assets/Materials/Level";
    const string LevelName = "Level";
    const string UIName = "UI";
    const string GameManagerName = "GameManager";

    const float TileSize = 1f;
    const float TileThickness = 0.5f;

    // 맵 배치: # = 타일, . = 구멍, S = 시작 위치, * = 아이템(타일 있음). 첫 줄이 맵 안쪽(+Z)
    static readonly string[] Layout =
    {
        "###*",
        "#.*#",
        "#*.#",
        "S###",
    };

    static readonly Color TileLightColor = new Color(0.55f, 0.80f, 0.35f);
    static readonly Color TileDarkColor = new Color(0.42f, 0.66f, 0.27f);

    [MenuItem("Tools/Voxel/Build Level")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        var light = CreateMaterial("Tile_Light", TileLightColor);
        var dark = CreateMaterial("Tile_Dark", TileDarkColor);
        var tilePrefab = CreateTilePrefab(light);
        var itemPrefab = ItemBuilder.EnsurePrefab();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 타일맵
        var old = GameObject.Find(LevelName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var level = new GameObject(LevelName);
        var tiles = new GameObject("Tiles").transform;
        tiles.SetParent(level.transform, false);
        var items = new GameObject("Items").transform;
        items.SetParent(level.transform, false);

        var startPosition = Vector3.zero;
        var itemCount = 0;
        var rows = Layout.Length;
        for (var row = 0; row < rows; row++)
        {
            var z = rows - 1 - row;
            for (var x = 0; x < Layout[row].Length; x++)
            {
                var cell = Layout[row][x];
                if (cell == '.') continue;

                var position = CellToWorld(x, z, Layout[row].Length, rows);
                if (cell == 'S') startPosition = position;

                var tile = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, scene);
                tile.name = $"Tile_{x}_{z}";
                tile.transform.SetParent(tiles, false);
                tile.transform.localPosition = position;
                if ((x + z) % 2 == 1)
                    tile.GetComponentInChildren<MeshRenderer>().sharedMaterial = dark;

                if (cell == '*')
                {
                    var item = (GameObject)PrefabUtility.InstantiatePrefab(itemPrefab, scene);
                    item.name = $"Item_{++itemCount}";
                    item.transform.SetParent(items, false);
                    item.transform.localPosition = position + Vector3.up * ItemBuilder.FloatHeight;
                }
            }
        }

        var uiManager = BuildUI();

        // 플레이어: 시작 위치, 카메라 쪽(-Z)을 바라봄
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VoxelCharacterBuilder.PrefabPath);
        var player = GameObject.Find(VoxelCharacterBuilder.CharacterName);
        if (player == null) player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.transform.SetPositionAndRotation(startPosition, Quaternion.Euler(0f, 180f, 0f));

        // 카메라
        var cam = Camera.main;
        var follow = cam.GetComponent<FollowCamera>();
        if (follow == null) follow = cam.gameObject.AddComponent<FollowCamera>();
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        follow.Snap();

        // 게임 상태 관리
        var oldManager = GameObject.Find(GameManagerName);
        if (oldManager != null) UnityEngine.Object.DestroyImmediate(oldManager);
        var gameManager = new GameObject(GameManagerName).AddComponent<GameManager>();
        var gm = new SerializedObject(gameManager);
        gm.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        gm.FindProperty("ui").objectReferenceValue = uiManager;
        gm.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelBuilder] Built level in {ScenePath}");
    }

    /// <summary>
    /// 화면 UI
    /// - 왼쪽 위 ITEM 0 / 3 (4단계 사용자 결정: 영어 표시, 왼쪽 위)
    /// - 결과 화면: 화면 전체를 반투명하게 어둡게 + 가운데 큰 글자 (5단계 사용자 결정)
    /// </summary>
    static UIManager BuildUI()
    {
        var old = GameObject.Find(UIName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var ui = new GameObject(UIName, typeof(RectTransform));
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 글자가 배경색과 상관없이 잘 보이도록 반투명 어두운 판 위에 표시
        var panel = new GameObject("ItemPanel", typeof(RectTransform), typeof(Image));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(ui.transform, false);
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(32f, -28f);
        panelRect.sizeDelta = new Vector2(330f, 84f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        var textGo = new GameObject("ItemText", typeof(RectTransform));
        var textRect = (RectTransform)textGo.transform;
        textRect.SetParent(panelRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 0f);
        textRect.offsetMax = new Vector2(-20f, 0f);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "ITEM 0 / 3";
        text.fontSize = 52f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        // 결과 화면 (처음에는 숨김)
        var result = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
        var resultRect = (RectTransform)result.transform;
        resultRect.SetParent(ui.transform, false);
        resultRect.anchorMin = Vector2.zero;
        resultRect.anchorMax = Vector2.one;
        resultRect.offsetMin = resultRect.offsetMax = Vector2.zero;
        result.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var resultTextGo = new GameObject("ResultText", typeof(RectTransform));
        var resultTextRect = (RectTransform)resultTextGo.transform;
        resultTextRect.SetParent(resultRect, false);
        resultTextRect.anchorMin = new Vector2(0f, 0.5f);
        resultTextRect.anchorMax = new Vector2(1f, 0.5f);
        resultTextRect.sizeDelta = new Vector2(0f, 240f);
        resultTextRect.anchoredPosition = new Vector2(0f, 40f);
        var resultText = resultTextGo.AddComponent<TextMeshProUGUI>();
        resultText.text = "GAME OVER";
        resultText.fontSize = 170f;
        resultText.fontStyle = FontStyles.Bold;
        resultText.alignment = TextAlignmentOptions.Center;
        resultText.textWrappingMode = TextWrappingModes.NoWrap;

        // 재시작 카운트다운: 결과 문구 아래 숫자 (6단계 사용자 결정)
        var countdownGo = new GameObject("CountdownText", typeof(RectTransform));
        var countdownRect = (RectTransform)countdownGo.transform;
        countdownRect.SetParent(resultRect, false);
        countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
        countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
        countdownRect.sizeDelta = new Vector2(300f, 200f);
        countdownRect.anchoredPosition = new Vector2(0f, -150f);
        var countdownText = countdownGo.AddComponent<TextMeshProUGUI>();
        countdownText.text = "3";
        countdownText.fontSize = 140f;
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.color = Color.white;
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.textWrappingMode = TextWrappingModes.NoWrap;
        countdownGo.SetActive(false);
        result.SetActive(false);

        var manager = ui.AddComponent<UIManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("itemText").objectReferenceValue = text;
        so.FindProperty("resultPanel").objectReferenceValue = result;
        so.FindProperty("resultText").objectReferenceValue = resultText;
        so.FindProperty("countdownText").objectReferenceValue = countdownText;
        so.ApplyModifiedPropertiesWithoutUndo();
        return manager;
    }

    /// <summary>게임 카메라 시점에 UI를 함께 찍는다. 확인용이며 씬은 저장하지 않는다.</summary>
    static void CaptureWithUI(string file, GameState? result)
    {
        var canvas = GameObject.Find(UIName).GetComponent<Canvas>();
        var ui = canvas.GetComponent<UIManager>();
        if (result.HasValue)
        {
            ui.ShowResult(result.Value);
            ui.ShowCountdown(3); // 결과가 나온 직후 모습
        }
        else ui.HideResult();

        var main = Camera.main.transform;
        PreviewCapture.Capture(file, main.position, main.rotation, 60f, 960, 540, cam =>
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
        });

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        ui.HideResult();
    }

    static Vector3 CellToWorld(int x, int z, int columns, int rows)
    {
        // 맵 중심이 원점이 되도록 배치, 타일 윗면 = y 0
        return new Vector3((x - (columns - 1) / 2f) * TileSize, 0f, (z - (rows - 1) / 2f) * TileSize);
    }

    static GameObject CreateTilePrefab(Material material)
    {
        var root = new GameObject("Tile");
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, -TileThickness / 2f, 0f);
        collider.size = new Vector3(TileSize, TileThickness, TileSize);

        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.name = "Mesh";
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<BoxCollider>());
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = collider.center;
        mesh.transform.localScale = collider.size;
        mesh.GetComponent<MeshRenderer>().sharedMaterial = material;

        Directory.CreateDirectory(Path.GetDirectoryName(TilePrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, TilePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static Material CreateMaterial(string name, Color color)
    {
        Directory.CreateDirectory(MaterialDir);
        var path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // UI 글자에는 TextMeshPro 필수 리소스(Assets/TextMesh Pro)가 필요하다. 없으면 메뉴
    // Window > TextMeshPro > Import TMP Essential Resources 또는 명령줄 -importPackage 로 가져온다.
    // (AssetDatabase.ImportPackage는 비동기라 batchmode -quit에서는 끝나기 전에 종료됨)

    // batchmode 실행용:
    // Unity.exe -batchmode -quit -executeMethod LevelBuilder.BuildAllFromCommandLine [-previewDir <폴더>]
    public static void BuildAllFromCommandLine()
    {
        try
        {
            AnimationBuilder.Build();
            ItemBuilder.Build();
            VoxelCharacterBuilder.Build();
            AssetDatabase.SaveAssets();
            Build();

            var args = Environment.GetCommandLineArgs();
            var idx = Array.IndexOf(args, "-previewDir");
            if (idx >= 0 && idx + 1 < args.Length)
            {
                var dir = args[idx + 1];
                var cam = Camera.main.transform;
                PreviewCapture.Capture(Path.Combine(dir, "game-view.png"), cam.position, cam.rotation);
                // 맵 전체를 보는 확인용 시점
                PreviewCapture.Capture(Path.Combine(dir, "overview.png"), new Vector3(0f, 6.5f, -5.5f),
                    Quaternion.LookRotation(new Vector3(0f, -6.5f, 5.5f)));

                // 캐릭터 확대 (시작 위치에서 카메라 쪽 -Z를 바라보는 상태)
                var p = GameObject.Find(VoxelCharacterBuilder.CharacterName).transform.position;
                var look = p + Vector3.up * 0.55f;
                void Close(string file, Vector3 offset) =>
                    PreviewCapture.Capture(Path.Combine(dir, file), p + offset,
                        Quaternion.LookRotation(look - (p + offset)), 35f, 640, 640);
                Close("char-front.png", new Vector3(0.7f, 0.9f, -1.9f));
                Close("char-back.png", new Vector3(-0.8f, 0.9f, 1.9f));
                Close("char-side.png", new Vector3(2.1f, 0.6f, 0f));

                // 금화 확대
                var coin = GameObject.Find("Item_1").transform.position;
                var coinCam = coin + new Vector3(0.25f, 0.2f, -0.9f);
                PreviewCapture.Capture(Path.Combine(dir, "item-close.png"), coinCam,
                    Quaternion.LookRotation(coin - coinCam), 35f, 480, 480);

                // UI 포함 화면 (Overlay UI는 카메라에 찍히지 않으므로 잠시 카메라 모드로 바꿔 찍음)
                CaptureWithUI(Path.Combine(dir, "ui-playing.png"), null);
                CaptureWithUI(Path.Combine(dir, "ui-gameover.png"), GameState.GameOver);
                CaptureWithUI(Path.Combine(dir, "ui-clear.png"), GameState.Clear);

                // 애니메이션 자세
                AnimationBuilder.CapturePoses(dir);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
