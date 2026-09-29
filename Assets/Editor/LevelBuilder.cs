using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 4x4 타일맵을 만들고 플레이어와 카메라를 Main 씬에 배치한다.
/// 메뉴: Tools > Voxel > Build Level
/// </summary>
public static class LevelBuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string TilePrefabPath = "Assets/Prefabs/Tile.prefab";
    const string MaterialDir = "Assets/Materials/Level";
    const string LevelName = "Level";

    const float TileSize = 1f;
    const float TileThickness = 0.5f;

    // 맵 배치: # = 타일, . = 구멍, S = 시작 위치(타일 있음). 첫 줄이 맵 안쪽(+Z)
    static readonly string[] Layout =
    {
        "####",
        "#.##",
        "##.#",
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

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 타일맵
        var old = GameObject.Find(LevelName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var level = new GameObject(LevelName);
        var tiles = new GameObject("Tiles").transform;
        tiles.SetParent(level.transform, false);

        var startPosition = Vector3.zero;
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
            }
        }

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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelBuilder] Built level in {ScenePath}");
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

    // batchmode 실행용:
    // Unity.exe -batchmode -quit -executeMethod LevelBuilder.BuildAllFromCommandLine [-previewDir <폴더>]
    public static void BuildAllFromCommandLine()
    {
        try
        {
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
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
