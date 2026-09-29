using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 큐브를 조합해 복셀 캐릭터(플레이어) 프리팹을 만든다.
/// 한복풍 옷을 입은 여자 캐릭터 (레퍼런스: 사용자 제공 이미지).
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

    // 복셀 1칸 = 0.1 unit
    const float Hip = 0.35f;      // 엉덩이(다리 피벗) 높이
    const float Shoulder = 0.70f; // 어깨(팔 피벗) / 목(머리 피벗) 높이
    const float ArmX = 0.26f;     // 어깨 피벗 좌우 위치
    const float LegX = 0.09f;     // 엉덩이 피벗 좌우 위치

    static readonly Color SkinColor = new Color(1.00f, 0.89f, 0.82f);
    static readonly Color HairColor = new Color(0.13f, 0.11f, 0.12f);
    static readonly Color EyeColor = new Color(0.16f, 0.14f, 0.15f);
    static readonly Color TopColor = new Color(0.66f, 0.20f, 0.25f);    // 저고리 (붉은색)
    static readonly Color CollarColor = new Color(0.55f, 0.36f, 0.22f); // 깃 (갈색)
    static readonly Color SashColor = new Color(0.26f, 0.30f, 0.50f);   // 허리띠 (남색)
    static readonly Color SkirtColor = new Color(0.94f, 0.56f, 0.76f);  // 치마 (분홍)
    static readonly Color FrillColor = new Color(0.97f, 0.95f, 0.95f);  // 소매 프릴 (흰색)
    static readonly Color ShoeColor = new Color(0.30f, 0.18f, 0.15f);

    [MenuItem("Tools/Voxel/Build Character")]
    public static void BuildMenu()
    {
        Build();
        AssetDatabase.SaveAssets();
    }

    public static GameObject Build()
    {
        var skin = CreateMaterial("Skin", SkinColor);
        var hair = CreateMaterial("Hair", HairColor);
        var eye = CreateMaterial("Eye", EyeColor);
        var top = CreateMaterial("Top", TopColor);
        var collar = CreateMaterial("Collar", CollarColor);
        var sash = CreateMaterial("Sash", SashColor);
        var skirt = CreateMaterial("Skirt", SkirtColor);
        var frill = CreateMaterial("Frill", FrillColor);
        var shoe = CreateMaterial("Shoe", ShoeColor);

        var root = new GameObject(CharacterName);
        var model = CreatePivot("Model", root.transform, Vector3.zero);

        // 몸통 (피벗 = 엉덩이 높이). 치마는 다리가 아니라 몸통에 붙여서 걸을 때 흔들리지 않게 한다
        var body = CreatePivot("Body", model, new Vector3(0f, Hip, 0f));
        CreateCube("Mesh", body, new Vector3(0f, 0.225f, 0f), new Vector3(0.36f, 0.25f, 0.20f), top);
        CreateCube("Collar_L", body, new Vector3(-0.035f, 0.26f, 0.106f), new Vector3(0.05f, 0.19f, 0.012f), collar, new Vector3(0f, 0f, 21f));
        CreateCube("Collar_R", body, new Vector3(0.035f, 0.26f, 0.106f), new Vector3(0.05f, 0.19f, 0.012f), collar, new Vector3(0f, 0f, -21f));
        CreateCube("Sash", body, new Vector3(0f, 0.12f, 0f), new Vector3(0.38f, 0.08f, 0.22f), sash);
        // 치마: 아래로 갈수록 넓어지는 3단, 밑단은 발목(y 0.09)
        CreateCube("Skirt_Top", body, new Vector3(0f, 0.04f, 0f), new Vector3(0.38f, 0.12f, 0.24f), skirt);
        CreateCube("Skirt_Mid", body, new Vector3(0f, -0.085f, 0f), new Vector3(0.44f, 0.13f, 0.30f), skirt);
        CreateCube("Skirt_Bottom", body, new Vector3(0f, -0.205f, 0f), new Vector3(0.50f, 0.11f, 0.36f), skirt);

        // 머리 (피벗 = 목)
        var head = CreatePivot("Head", model, new Vector3(0f, Shoulder, 0f));
        CreateCube("Mesh", head, new Vector3(0f, 0.175f, 0f), new Vector3(0.35f, 0.35f, 0.35f), skin);
        CreateCube("Hair_Top", head, new Vector3(0f, 0.34f, 0f), new Vector3(0.37f, 0.08f, 0.37f), hair);
        // 뒷머리: 등 가운데까지 내려오는 긴 머리
        CreateCube("Hair_Back", head, new Vector3(0f, 0.07f, -0.18f), new Vector3(0.37f, 0.62f, 0.07f), hair);
        // 옆머리: 얼굴 양옆, 어깨 위까지
        CreateCube("Hair_Side_L", head, new Vector3(-0.19f, 0.18f, -0.03f), new Vector3(0.06f, 0.32f, 0.30f), hair);
        CreateCube("Hair_Side_R", head, new Vector3(0.19f, 0.18f, -0.03f), new Vector3(0.06f, 0.32f, 0.30f), hair);
        // 앞머리: 캐릭터의 왼쪽에서 가르마를 타고 오른쪽(+X)으로 넘긴 모양
        CreateCube("Bangs", head, new Vector3(0f, 0.31f, 0.185f), new Vector3(0.37f, 0.06f, 0.04f), hair);
        CreateCube("Bangs_Side", head, new Vector3(0.115f, 0.25f, 0.185f), new Vector3(0.14f, 0.06f, 0.04f), hair);
        CreateCube("Eye_L", head, new Vector3(-0.08f, 0.17f, 0.18f), new Vector3(0.06f, 0.08f, 0.02f), eye);
        CreateCube("Eye_R", head, new Vector3(0.08f, 0.17f, 0.18f), new Vector3(0.06f, 0.08f, 0.02f), eye);

        // 팔 (피벗 = 어깨, 캐릭터의 왼쪽 = -X): 짧은 퍼프 소매 + 흰 프릴
        foreach (var (name, side) in new[] { ("Arm_L", -1f), ("Arm_R", 1f) })
        {
            var arm = CreatePivot(name, model, new Vector3(ArmX * side, Shoulder, 0f));
            CreateCube("Mesh", arm, new Vector3(0f, -0.165f, 0f), new Vector3(0.13f, 0.33f, 0.13f), skin);
            CreateCube("Sleeve", arm, new Vector3(0f, -0.08f, 0f), new Vector3(0.17f, 0.16f, 0.17f), top);
            CreateCube("Frill", arm, new Vector3(0f, -0.175f, 0f), new Vector3(0.18f, 0.03f, 0.18f), frill);
        }

        // 다리 (피벗 = 엉덩이): 치마 속에 있고 신발만 밑단 아래로 보인다
        foreach (var (name, side) in new[] { ("Leg_L", -1f), ("Leg_R", 1f) })
        {
            var leg = CreatePivot(name, model, new Vector3(LegX * side, Hip, 0f));
            CreateCube("Mesh", leg, new Vector3(0f, -0.175f, 0f), new Vector3(0.14f, 0.35f, 0.14f), skin);
            CreateCube("Shoe", leg, new Vector3(0f, -0.31f, 0.02f), new Vector3(0.15f, 0.08f, 0.20f), shoe);
        }

        AddPhysics(root);
        root.AddComponent<PlayerController>();

        // 애니메이션: Model에 Animator (몸 전체의 튐·늘이기는 Model 자신을 움직임)
        var animator = model.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = AnimationBuilder.EnsureController();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log($"[VoxelCharacterBuilder] Saved {PrefabPath}");
        return prefab;
    }

    static void AddPhysics(GameObject root)
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

    static void CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 size, Material mat, Vector3 localEuler = default)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localEulerAngles = localEuler;
        cube.transform.localScale = size;
        cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
