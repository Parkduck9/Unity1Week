using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 게임 캐릭터(플레이어) 프리팹을 만든다.
/// 모양은 DetailedCharacterBuilder의 디테일 복셀 캐릭터(3차, 복셀 0.025)를 쓴다.
/// (이전의 큐브 26개 캐릭터는 Git 기록에 남아 있음)
/// 씬 배치는 LevelBuilder가 담당한다.
/// 메뉴: Tools > Voxel > Build Character
/// </summary>
public static class VoxelCharacterBuilder
{
    const string MaterialDir = "Assets/Materials/Character";
    const string PhysicsMaterialPath = "Assets/Materials/Physics/NoFriction.physicMaterial";
    public const string PrefabPath = "Assets/Prefabs/VoxelCharacter.prefab";
    public const string CharacterName = "VoxelCharacter";
    public const float Height = 1.08f;

    [MenuItem("Tools/Voxel/Build Character")]
    public static void BuildMenu()
    {
        Build();
        AssetDatabase.SaveAssets();
    }

    public static GameObject Build()
    {
        var prefab = DetailedCharacterBuilder.BuildPrefab(PrefabPath, CharacterName);
        Debug.Log($"[VoxelCharacterBuilder] Saved {PrefabPath}");
        return prefab;
    }

    internal static void AddPhysics(GameObject root)
    {
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.freezeRotation = true; // 회전은 PlayerController가 직접 처리
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, Height / 2f, 0f);
        capsule.height = Height;
        capsule.radius = 0.25f;
        capsule.sharedMaterial = GetNoFrictionMaterial(); // 타일 옆면에 달라붙지 않도록
    }

    static PhysicsMaterial GetNoFrictionMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
        if (mat != null) return mat;

        Directory.CreateDirectory(Path.GetDirectoryName(PhysicsMaterialPath));
        mat = new PhysicsMaterial("NoFriction")
        {
            staticFriction = 0f,
            dynamicFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        AssetDatabase.CreateAsset(mat, PhysicsMaterialPath);
        return mat;
    }

    internal static Material CreateMaterial(string name, Color color)
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

    internal static Transform CreatePivot(string name, Transform parent, Vector3 localPosition)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }
}
