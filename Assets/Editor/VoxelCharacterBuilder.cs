using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 큐브를 조합해 복셀 캐릭터 프리팹을 만들고 Main 씬에 배치한다.
/// 메뉴: Tools > Voxel > Build Character
/// </summary>
public static class VoxelCharacterBuilder
{
    const string MaterialDir = "Assets/Materials/Character";
    const string PrefabPath = "Assets/Prefabs/VoxelCharacter.prefab";
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string CharacterName = "VoxelCharacter";

    // 복셀 1칸 = 0.1 unit
    const float Hip = 0.35f;      // 엉덩이(다리 피벗) 높이
    const float Shoulder = 0.70f; // 어깨(팔 피벗) / 목(머리 피벗) 높이

    static readonly Color SkinColor = new Color(1.00f, 0.80f, 0.62f);
    static readonly Color ShirtColor = new Color(0.25f, 0.50f, 0.90f);
    static readonly Color PantsColor = new Color(0.20f, 0.22f, 0.35f);
    static readonly Color ShoeColor = new Color(0.15f, 0.15f, 0.17f);
    static readonly Color HairColor = new Color(0.35f, 0.22f, 0.12f);
    static readonly Color EyeColor = new Color(0.08f, 0.08f, 0.10f);

    [MenuItem("Tools/Voxel/Build Character")]
    public static void Build()
    {
        var skin = CreateMaterial("Skin", SkinColor);
        var shirt = CreateMaterial("Shirt", ShirtColor);
        var pants = CreateMaterial("Pants", PantsColor);
        var shoe = CreateMaterial("Shoe", ShoeColor);
        var hair = CreateMaterial("Hair", HairColor);
        var eye = CreateMaterial("Eye", EyeColor);

        var root = new GameObject(CharacterName);
        var model = CreatePivot("Model", root.transform, Vector3.zero);

        // 몸통
        var body = CreatePivot("Body", model, new Vector3(0f, Hip, 0f));
        CreateCube("Mesh", body, new Vector3(0f, 0.175f, 0f), new Vector3(0.40f, 0.35f, 0.20f), shirt);

        // 머리 (피벗 = 목)
        var head = CreatePivot("Head", model, new Vector3(0f, Shoulder, 0f));
        CreateCube("Mesh", head, new Vector3(0f, 0.175f, 0f), new Vector3(0.35f, 0.35f, 0.35f), skin);
        CreateCube("Hair_Top", head, new Vector3(0f, 0.34f, 0f), new Vector3(0.37f, 0.08f, 0.37f), hair);
        CreateCube("Hair_Back", head, new Vector3(0f, 0.25f, -0.16f), new Vector3(0.37f, 0.20f, 0.06f), hair);
        CreateCube("Eye_L", head, new Vector3(-0.08f, 0.19f, 0.18f), new Vector3(0.06f, 0.08f, 0.02f), eye);
        CreateCube("Eye_R", head, new Vector3(0.08f, 0.19f, 0.18f), new Vector3(0.06f, 0.08f, 0.02f), eye);

        // 팔 (피벗 = 어깨, 캐릭터의 왼쪽 = -X)
        foreach (var (name, side) in new[] { ("Arm_L", -1f), ("Arm_R", 1f) })
        {
            var arm = CreatePivot(name, model, new Vector3(0.275f * side, Shoulder, 0f));
            CreateCube("Mesh", arm, new Vector3(0f, -0.175f, 0f), new Vector3(0.15f, 0.35f, 0.15f), skin);
            CreateCube("Sleeve", arm, new Vector3(0f, -0.06f, 0f), new Vector3(0.16f, 0.12f, 0.16f), shirt);
        }

        // 다리 (피벗 = 엉덩이)
        foreach (var (name, side) in new[] { ("Leg_L", -1f), ("Leg_R", 1f) })
        {
            var leg = CreatePivot(name, model, new Vector3(0.10f * side, Hip, 0f));
            CreateCube("Mesh", leg, new Vector3(0f, -0.175f, 0f), new Vector3(0.20f, 0.35f, 0.20f), pants);
            CreateCube("Shoe", leg, new Vector3(0f, -0.31f, 0.01f), new Vector3(0.21f, 0.08f, 0.22f), shoe);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        PlaceInMainScene(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[VoxelCharacterBuilder] Saved {PrefabPath}");
    }

    static void PlaceInMainScene(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == CharacterName) UnityEngine.Object.DestroyImmediate(go);
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.position = Vector3.zero;
        EditorSceneManager.SaveScene(scene);
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

    static Transform CreatePivot(string name, Transform parent, Vector3 localPosition)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    static void CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 size, Material mat)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = size;
        cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // batchmode 실행용: Unity.exe -batchmode -quit -executeMethod VoxelCharacterBuilder.BuildFromCommandLine [-previewDir <폴더>]
    public static void BuildFromCommandLine()
    {
        try
        {
            Build();
            var args = Environment.GetCommandLineArgs();
            var idx = Array.IndexOf(args, "-previewDir");
            if (idx >= 0 && idx + 1 < args.Length) CapturePreviews(args[idx + 1]);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void CapturePreviews(string dir)
    {
        Directory.CreateDirectory(dir);
        var target = new Vector3(0f, 0.5f, 0f);
        Capture(Path.Combine(dir, "front.png"), new Vector3(1.1f, 1.0f, 2.0f), target);
        Capture(Path.Combine(dir, "back.png"), new Vector3(-1.3f, 1.0f, -1.8f), target);
        Capture(Path.Combine(dir, "side.png"), new Vector3(2.3f, 0.6f, 0f), target);
    }

    static void Capture(string file, Vector3 position, Vector3 lookAt)
    {
        const int w = 640, h = 640;
        var camGo = new GameObject("PreviewCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = position;
        cam.transform.LookAt(lookAt);
        cam.fieldOfView = 35f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.82f, 0.86f, 0.92f);

        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());

        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(camGo);
    }
}
