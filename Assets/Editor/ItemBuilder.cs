using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 금화(엽전) 아이템 프리팹을 만든다.
/// 복셀(0.05)을 원 모양으로 배치하고 가운데에 네모 구멍을 낸 뒤 하나의 메쉬로 합친다.
/// 메뉴: Tools > Voxel > Build Item
/// </summary>
public static class ItemBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Item.prefab";
    const string MeshPath = "Assets/Meshes/Coin.asset";
    const string MaterialPath = "Assets/Materials/Item/Gold.mat";
    const string ExpiredMaterialPath = "Assets/Materials/Item/GoldExpired.mat"; // 2초가 지나 점수가 없는 코인 (회색)

    public const float Diameter = 0.4f;      // 사용자 결정: 중간 크기
    public const float FloatHeight = 0.5f;   // 바닥에서 떠 있는 높이 (캐릭터 허리쯤)
    const float Thickness = 0.06f;
    const float TriggerRadius = 0.3f;

    // 8 × 8 복셀 (X = 채움, . = 비움). 가운데 2 × 2가 엽전의 네모 구멍
    static readonly string[] Pattern =
    {
        "..XXXX..",
        ".XXXXXX.",
        "XXXXXXXX",
        "XXX..XXX",
        "XXX..XXX",
        "XXXXXXXX",
        ".XXXXXX.",
        "..XXXX..",
    };

    static readonly Color GoldColor = new Color(1.00f, 0.78f, 0.25f);
    static readonly Color GoldEmission = new Color(0.30f, 0.20f, 0.03f);
    static readonly Color ExpiredColor = new Color(0.55f, 0.55f, 0.58f);

    [MenuItem("Tools/Voxel/Build Item")]
    public static void BuildMenu()
    {
        Build();
        AssetDatabase.SaveAssets();
    }

    public static GameObject EnsurePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return prefab != null ? prefab : Build();
    }

    public static GameObject Build()
    {
        var mesh = BuildCoinMesh();
        var material = CreateGoldMaterial();
        var expired = CreateExpiredMaterial();

        var root = new GameObject("Item");
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = TriggerRadius;

        var coin = new GameObject("Coin");
        coin.transform.SetParent(root.transform, false);
        coin.AddComponent<MeshFilter>().sharedMesh = mesh;
        coin.AddComponent<MeshRenderer>().sharedMaterial = material;

        var item = root.AddComponent<Item>();
        var so = new SerializedObject(item);
        so.FindProperty("visual").objectReferenceValue = coin.transform;
        so.FindProperty("expiredMaterial").objectReferenceValue = expired;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[ItemBuilder] Saved {PrefabPath}");
        return prefab;
    }

    static Mesh BuildCoinMesh()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);

        var n = Pattern.Length;
        var voxel = Diameter / n;
        var combine = new List<CombineInstance>();
        for (var row = 0; row < n; row++)
        {
            for (var col = 0; col < n; col++)
            {
                if (Pattern[row][col] != 'X') continue;
                // 동전은 세워서(XY 평면) 만든다. 중심이 원점
                var pos = new Vector3((col - (n - 1) / 2f) * voxel, ((n - 1) / 2f - row) * voxel, 0f);
                combine.Add(new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(voxel, voxel, Thickness)),
                });
            }
        }

        var mesh = new Mesh { name = "Coin" };
        mesh.CombineMeshes(combine.ToArray(), true, true);
        mesh.RecalculateBounds();

        Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing); // GUID 유지
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Material CreateGoldMaterial()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetColor("_BaseColor", GoldColor);
        mat.SetFloat("_Metallic", 0.5f);
        mat.SetFloat("_Smoothness", 0.55f);
        // 초록 타일 위에서 잘 보이도록 살짝 스스로 빛나게
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", GoldEmission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateExpiredMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ExpiredMaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, ExpiredMaterialPath);
        }
        mat.SetColor("_BaseColor", ExpiredColor);
        mat.SetFloat("_Metallic", 0.2f);
        mat.SetFloat("_Smoothness", 0.3f);
        mat.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
