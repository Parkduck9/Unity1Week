using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 디테일한 복셀 캐릭터 (ChatGPT 그림 참고). 복셀 한 칸 0.025, 머리가 큰 비율.
/// 2차: 상자를 하나씩 쌓지 않고, 구·타원체·원뿔·캡슐 같은 둥근 모양을 정해 두고 그 안을 작은 블록으로 자동으로 채운다.
/// 부위마다 밖으로 보이는 면만 모아 메쉬 하나로 만든다.
/// 관절 피벗(목·어깨·엉덩이) 구조는 기존 캐릭터와 같아서 애니메이션을 그대로 쓸 수 있다.
/// 지금은 미리보기용 프리팹으로만 만든다 (게임 적용은 사용자 확인 후).
/// 메뉴: Tools > Voxel > Build Detailed Character (Preview)
/// </summary>
public static class DetailedCharacterBuilder
{
    public const string PrefabPath = "Assets/Prefabs/VoxelCharacterDetailed.prefab";
    const string MeshDir = "Assets/Meshes/CharacterDetailed";
    const string PreviewPage = "캐릭터 미리보기 (디테일).html"; // 3D 미리보기 페이지 (프로젝트 폴더), 이 안의 데이터 부분을 갈아 끼움
    const string DataStart = "/*CHARACTER-DATA-START";
    const string DataEnd = "/*CHARACTER-DATA-END*/";

    const float V = 0.025f;         // 복셀 한 칸 (2차: 0.05 → 0.025)
    const float Hip = 0.30f;        // 다리·몸통 피벗 높이
    const float Neck = 0.60f;       // 머리 피벗 높이
    const float Shoulder = 0.58f;   // 팔 피벗 높이
    const float ArmX = 0.20f;
    const float LegX = 0.07f;

    // 색 (기존 머티리얼 이름과 같으면 같은 머티리얼을 함께 씀)
    static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        { "Skin", new Color(1.00f, 0.89f, 0.82f) },
        { "Hair", new Color(0.13f, 0.11f, 0.12f) },
        { "HairShade", new Color(0.21f, 0.18f, 0.20f) },  // 머리카락 결 (조금 밝은 검정)
        { "Eye", new Color(0.16f, 0.14f, 0.15f) },
        { "Top", new Color(0.66f, 0.20f, 0.25f) },
        { "Collar", new Color(0.55f, 0.36f, 0.22f) },
        { "Sash", new Color(0.26f, 0.30f, 0.50f) },
        { "Skirt", new Color(0.94f, 0.56f, 0.76f) },
        { "Frill", new Color(0.97f, 0.95f, 0.95f) },      // 소매 프릴, 동정(흰 깃), 눈 하이라이트, 버선
        { "Shoe", new Color(0.30f, 0.18f, 0.15f) },
        { "Blush", new Color(1.00f, 0.62f, 0.64f) },      // 볼터치, 혀
        { "Mouth", new Color(0.78f, 0.28f, 0.32f) },
        { "SkirtShade", new Color(0.86f, 0.47f, 0.67f) }, // 치마 주름
    };

    // ===== 조각 도구: 모양 안쪽이면 채우고(Add), 이미 채운 칸의 색을 바꾸고(Paint), 지운다(Cut) =====

    enum OpKind { Add, Paint, Cut }

    class Op
    {
        public OpKind Kind;
        public Func<Vector3, bool> Inside;
        public string Color;
        public string OnlyOver; // Paint: 이 색 위에만 칠함 (null이면 아무 색 위에나)
    }

    class Part
    {
        public string Name;
        public string Joint;
        public Vector3 Pivot;
        public Vector3 Offset = new Vector3(V / 2f, V / 2f, V / 2f); // 칸 i의 중심 = i·V + V/2 (0을 중심으로 좌우 대칭)
        public Vector3 BoundsMin, BoundsMax;                      // 조각할 범위 (피벗 기준, m)
        public readonly List<Op> Ops = new List<Op>();
        public readonly Dictionary<Vector3Int, string> Cells = new Dictionary<Vector3Int, string>();

        public void Add(string color, Func<Vector3, bool> inside) => Ops.Add(new Op { Kind = OpKind.Add, Inside = inside, Color = color });
        public void Paint(string color, Func<Vector3, bool> inside, string onlyOver = null) =>
            Ops.Add(new Op { Kind = OpKind.Paint, Inside = inside, Color = color, OnlyOver = onlyOver });
        public void Cut(Func<Vector3, bool> inside) => Ops.Add(new Op { Kind = OpKind.Cut, Inside = inside });

        /// <summary>범위 안의 모든 칸 중심에서 모양들을 차례로 적용해 칸을 채운다</summary>
        public void Voxelize()
        {
            var min = Vector3Int.FloorToInt(BoundsMin / V);
            var max = Vector3Int.CeilToInt(BoundsMax / V);
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var p = new Vector3(x, y, z) * V + Offset;
                string color = null;
                foreach (var op in Ops)
                {
                    if (!op.Inside(p)) continue;
                    switch (op.Kind)
                    {
                        case OpKind.Add: color = op.Color; break;
                        case OpKind.Cut: color = null; break;
                        case OpKind.Paint:
                            if (color != null && (op.OnlyOver == null || op.OnlyOver == color)) color = op.Color;
                            break;
                    }
                }
                if (color != null) Cells[new Vector3Int(x, y, z)] = color;
            }
        }
    }

    // 모양 판정 함수들
    static bool Ellipsoid(Vector3 p, Vector3 c, Vector3 r)
    {
        var d = p - c;
        return (d.x * d.x) / (r.x * r.x) + (d.y * d.y) / (r.y * r.y) + (d.z * d.z) / (r.z * r.z) <= 1f;
    }

    static bool Sphere(Vector3 p, Vector3 c, float r) => (p - c).sqrMagnitude <= r * r;

    static bool Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        var ab = b - a;
        var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).sqrMagnitude <= r * r;
    }

    static bool RoundBox(Vector3 p, Vector3 c, Vector3 half, float radius)
    {
        var q = new Vector3(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y), Mathf.Abs(p.z - c.z)) - half + Vector3.one * radius;
        var outside = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - radius <= 0f;
    }

    /// <summary>위아래 높이가 y0~y1이고 반지름이 (rTop → rBottom)으로 바뀌는 타원 원뿔대</summary>
    static bool EllipticFrustum(Vector3 p, float yTop, float yBottom, Vector2 rTop, Vector2 rBottom)
    {
        if (p.y > yTop || p.y < yBottom) return false;
        var t = (yTop - p.y) / (yTop - yBottom);
        var r = Vector2.Lerp(rTop, rBottom, t);
        return (p.x * p.x) / (r.x * r.x) + (p.z * p.z) / (r.y * r.y) <= 1f;
    }

    // 치마: 3단. 3차: 비탈을 없애고 단마다 곧은 벽(위아래 반지름 같음), 아래 단일수록 한 단계씩 넓어짐
    // (비탈이면 작은 블록이 들쭉날쭉한 계단을 만들어 지저분해 보였음)
    static readonly (float top, float bottom, Vector2 rTop, Vector2 rBottom)[] SkirtTiers =
    {
        (0.12f, -0.005f, new Vector2(0.190f, 0.160f), new Vector2(0.190f, 0.160f)),
        (-0.005f, -0.13f, new Vector2(0.235f, 0.200f), new Vector2(0.235f, 0.200f)),
        (-0.13f, -0.25f, new Vector2(0.280f, 0.240f), new Vector2(0.280f, 0.240f)),
    };

    static bool InSkirt(Vector3 p)
    {
        foreach (var tier in SkirtTiers)
            if (EllipticFrustum(p, tier.top, tier.bottom, tier.rTop, tier.rBottom)) return true;
        return false;
    }

    /// <summary>높이 y에서 치마 앞면(+z)의 위치</summary>
    static float SkirtFrontZ(float y)
    {
        foreach (var tier in SkirtTiers)
        {
            if (y > tier.top || y < tier.bottom) continue;
            var t = (tier.top - y) / (tier.top - tier.bottom);
            return Mathf.Lerp(tier.rTop.y, tier.rBottom.y, t);
        }
        return 0.14f;
    }

    [MenuItem("Tools/Voxel/Build Detailed Character (Preview)")]
    public static void BuildMenu()
    {
        Build();
        AssetDatabase.SaveAssets();
    }

    // ===== 부위 설계 (피벗 기준 m 단위, 앞 = +z, 캐릭터의 왼쪽 = -x) =====
    // 3차 원칙: 평평한 면 + 둥근 모서리(둥근 상자), 곧은 벽(세로 기둥). 비탈진 곡면은 쓰지 않음

    const float FaceZ = 0.18f; // 얼굴 앞면 위치 (머리 피벗 기준)

    static List<Part> DesignParts()
    {
        var parts = new List<Part>();

        // --- 몸통 (피벗 = 엉덩이 0.30): 저고리 + 허리띠·리본 + 3단 치마 ---
        var body = new Part
        {
            Name = "Body", Pivot = new Vector3(0f, Hip, 0f),
            BoundsMin = new Vector3(-0.31f, -0.27f, -0.27f), BoundsMax = new Vector3(0.31f, 0.34f, 0.27f),
        };
        // 저고리: 둥근 상자 (월드 높이 0.41 ~ 0.61), 앞면은 평평
        body.Add("Top", p => RoundBox(p, new Vector3(0f, 0.21f, 0f), new Vector3(0.165f, 0.10f, 0.105f), 0.04f));
        // 여밈: V자 안쪽은 흰 속옷, 맨 위는 목, 한쪽으로 비스듬한 갈색 깃
        body.Paint("Frill", p =>
        {
            if (p.z < 0.05f) return false;
            var v = p.y - 0.20f;
            return v >= 0f && Mathf.Abs(p.x) < v * 0.55f + 0.028f;
        }, "Top");
        body.Paint("Skin", p => p.z > 0.05f && p.y > 0.28f && Mathf.Abs(p.x) < (p.y - 0.20f) * 0.55f - 0.005f, "Frill");
        body.Paint("Collar", p =>
        {
            if (p.z < 0.05f || p.y < 0.13f) return false;
            // 캐릭터의 오른쪽 어깨(+x)에서 왼쪽 허리(-x) 쪽으로 내려오는 띠
            var a = new Vector2(0.10f, 0.31f);
            var b = new Vector2(-0.07f, 0.13f);
            var q = new Vector2(p.x, p.y);
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
            return (q - (a + ab * t)).magnitude < 0.018f;
        });
        // 치마 (곧은 벽 3단) + 세로 주름
        body.Add("Skirt", InSkirt);
        body.Paint("SkirtShade", p =>
        {
            var a = Mathf.Atan2(p.z, p.x);
            return Mathf.FloorToInt((a + Mathf.PI) / (2f * Mathf.PI) * 28f) % 2 == 0;
        }, "Skirt");
        // 단 아래쪽 한 줄은 밝게 두어 단 구분이 보이게
        body.Paint("Skirt", p =>
        {
            foreach (var tier in SkirtTiers)
                if (p.y < tier.bottom + V && p.y >= tier.bottom) return true;
            return false;
        }, "SkirtShade");
        // 허리띠 (곧은 벽)
        body.Add("Sash", p => p.y > 0.09f && p.y < 0.155f && (p.x * p.x) / (0.192f * 0.192f) + (p.z * p.z) / (0.162f * 0.162f) <= 1f);
        // 리본: 매듭 + 양쪽 고리 (둥근 상자)
        body.Add("Sash", p => RoundBox(p, new Vector3(0f, 0.122f, 0.172f), new Vector3(0.026f, 0.026f, 0.02f), 0.01f));
        body.Add("Sash", p => RoundBox(p, new Vector3(-0.06f, 0.128f, 0.166f), new Vector3(0.04f, 0.024f, 0.016f), 0.012f));
        body.Add("Sash", p => RoundBox(p, new Vector3(0.06f, 0.128f, 0.166f), new Vector3(0.04f, 0.024f, 0.016f), 0.012f));
        // 리본 끈 2개: 치마 앞면을 따라 곧게 늘어짐 (길이를 조금 다르게)
        foreach (var (x, bottom) in new[] { (-0.022f, -0.12f), (0.022f, -0.17f) })
        {
            body.Add("Sash", p =>
            {
                if (p.y > 0.11f || p.y < bottom) return false;
                // 단이 넓어지는 곳에서 끈이 끊겨 보이지 않도록 바로 위 칸의 위치까지 이어 붙임
                var z0 = SkirtFrontZ(p.y) + 0.0125f;
                var z1 = SkirtFrontZ(Mathf.Min(p.y + V, 0.11f)) + 0.0125f;
                return Mathf.Abs(p.x - x) < 0.0175f && p.z > Mathf.Min(z0, z1) - 0.0125f && p.z < Mathf.Max(z0, z1) + 0.0125f;
            });
        }
        parts.Add(body);

        // --- 머리 (피벗 = 목 0.60): 앞면이 평평한 둥근 상자 얼굴 + 가닥진 앞머리 + 곧게 떨어지는 긴 뒷머리 ---
        var head = new Part
        {
            Name = "Head", Joint = "목", Pivot = new Vector3(0f, Neck, 0f),
            BoundsMin = new Vector3(-0.28f, -0.34f, -0.26f), BoundsMax = new Vector3(0.28f, 0.47f, 0.24f),
        };
        head.Add("Skin", p => RoundBox(p, new Vector3(0f, 0.20f, 0f), new Vector3(0.205f, 0.195f, FaceZ), 0.05f)); // 아래 모서리를 덜 둥글게 해 입이 평평한 면에 들어가게
        head.Add("Skin", p => RoundBox(p, new Vector3(-0.212f, 0.14f, 0.01f), new Vector3(0.02f, 0.035f, 0.025f), 0.01f)); // 귀
        head.Add("Skin", p => RoundBox(p, new Vector3(0.212f, 0.14f, 0.01f), new Vector3(0.02f, 0.035f, 0.025f), 0.01f));

        // 앞머리 가닥: 폭 0.05씩, 가닥마다 끝 높이가 다름. 캐릭터의 왼쪽(-x)에서 가르마, 오른쪽(+x)으로 갈수록 길어짐
        var bangEnds = new[] { 0.275f, 0.26f, 0.27f, 0.25f, 0.24f, 0.25f, 0.23f, 0.22f, 0.23f };
        float BangBottom(float x)
        {
            var i = Mathf.Clamp(Mathf.FloorToInt((x + 0.225f) / 0.05f), 0, bangEnds.Length - 1);
            return bangEnds[i];
        }
        bool FaceWindow(Vector3 p) => p.z > 0.03f && Mathf.Abs(p.x) < 0.182f && p.y > -0.06f && p.y < BangBottom(p.x);

        // 머리카락 전체 덩어리 (얼굴 창은 비움)
        head.Add("Hair", p => !FaceWindow(p) && RoundBox(p, new Vector3(0f, 0.225f, -0.012f), new Vector3(0.235f, 0.225f, 0.207f), 0.10f));
        // 정수리 볼륨: 납작한 둥근 상자 2개를 살짝 어긋나게
        head.Add("Hair", p => RoundBox(p, new Vector3(-0.05f, 0.425f, -0.03f), new Vector3(0.14f, 0.03f, 0.13f), 0.03f));
        head.Add("Hair", p => RoundBox(p, new Vector3(0.07f, 0.41f, -0.06f), new Vector3(0.12f, 0.035f, 0.11f), 0.03f));
        // 얼굴 양옆으로 내려오는 머리 (볼 옆, 앞쪽까지)
        head.Add("Hair", p => RoundBox(p, new Vector3(-0.205f, 0.10f, 0.08f), new Vector3(0.035f, 0.17f, 0.07f), 0.025f));
        head.Add("Hair", p => RoundBox(p, new Vector3(0.205f, 0.08f, 0.08f), new Vector3(0.035f, 0.19f, 0.07f), 0.025f));
        // 등으로 곧게 떨어지는 긴 머리 (몸 뒤, 월드 약 0.33까지) + 아래로 갈수록 좁아지는 끝
        head.Add("Hair", p => RoundBox(p, new Vector3(0f, -0.02f, -0.165f), new Vector3(0.225f, 0.20f, 0.065f), 0.05f));
        head.Add("Hair", p => RoundBox(p, new Vector3(0f, -0.21f, -0.17f), new Vector3(0.17f, 0.06f, 0.055f), 0.035f));
        // 어깨 뒤로 흘러내리는 옆 가닥 (팔 뒤쪽)
        head.Add("Hair", p => RoundBox(p, new Vector3(-0.19f, -0.06f, -0.10f), new Vector3(0.04f, 0.16f, 0.045f), 0.02f));
        head.Add("Hair", p => RoundBox(p, new Vector3(0.19f, -0.09f, -0.10f), new Vector3(0.04f, 0.19f, 0.045f), 0.02f));
        // (물결 덩어리를 옆·뒤에 붙여 봤지만 가로 지느러미처럼 보여서 뺐음)
        // 뒷머리 끝을 가닥지게: 두 칸마다 한 칸씩 끝을 잘라 들쭉날쭉하게
        head.Cut(p => p.y < -0.235f && Mathf.FloorToInt((p.x + 1f) / (V * 2f)) % 2 == 0 && p.z < -0.11f);
        // 머리카락 결: 뒤쪽·옆 아래에 가는 세로 줄
        head.Paint("HairShade", p => (p.z < -0.10f || p.y < 0.10f) && Mathf.FloorToInt((p.x + 1f) / (V * 2f)) % 3 == 0, "Hair");

        // 얼굴 (평평한 앞면의 피부 위에만 칠함)
        bool OnFace(Vector3 p) => p.z > FaceZ - V * 1.5f;
        bool RoundRect(Vector3 p, float cx, float cy, float hx, float hy, float r) =>
            RoundBox(new Vector3(p.x, p.y, 0f), new Vector3(cx, cy, 0f), new Vector3(hx, hy, 1f), r);
        foreach (var side in new[] { -1f, 1f })
        {
            var ex = 0.085f * side;
            head.Paint("Eye", p => OnFace(p) && RoundRect(p, ex, 0.160f, 0.036f, 0.052f, 0.022f), "Skin");       // 눈동자
            head.Paint("Eye", p => OnFace(p) && RoundRect(p, ex + 0.004f * side, 0.215f, 0.046f, 0.012f, 0.006f), "Skin"); // 윗속눈썹
            head.Paint("Frill", p => OnFace(p) && RoundRect(p, ex + 0.012f * side, 0.182f, 0.0125f, 0.0125f, 0.004f), "Eye"); // 큰 하이라이트
            head.Paint("Frill", p => OnFace(p) && RoundRect(p, ex - 0.0125f * side, 0.130f, 0.006f, 0.006f, 0f), "Eye");    // 작은 하이라이트
            head.Paint("Hair", p => OnFace(p) && RoundRect(p, ex, 0.262f, 0.03f, 0.0065f, 0f), "Skin");                    // 눈썹 (앞머리에 가려질 수 있음)
            head.Paint("Blush", p => OnFace(p) && RoundRect(p, 0.135f * side, 0.098f, 0.03f, 0.0125f, 0.006f), "Skin");    // 볼터치
        }
        // 벌린 채 웃는 입 (위는 평평, 아래는 둥글게) + 혀
        head.Paint("Mouth", p => OnFace(p) && p.y <= 0.112f && p.y >= 0.078f && Mathf.Abs(p.x) <= 0.036f - Mathf.Max(0f, 0.092f - p.y) * 1.2f, "Skin");
        head.Paint("Blush", p => OnFace(p) && p.y < 0.086f && Mathf.Abs(p.x) < 0.0125f, "Mouth");
        parts.Add(head);

        // --- 팔 (피벗 = 어깨 0.58): 둥근 상자 퍼프 소매 + 흰 프릴 + 손 ---
        foreach (var (name, side) in new[] { ("Arm_L", -1f), ("Arm_R", 1f) })
        {
            var arm = new Part
            {
                Name = name, Joint = "어깨", Pivot = new Vector3(ArmX * side, Shoulder, 0f),
                BoundsMin = new Vector3(-0.10f, -0.30f, -0.10f), BoundsMax = new Vector3(0.10f, 0.03f, 0.10f),
            };
            arm.Add("Skin", p => RoundBox(p, new Vector3(0f, -0.18f, 0.005f), new Vector3(0.03f, 0.07f, 0.03f), 0.012f));      // 아래팔
            arm.Add("Skin", p => RoundBox(p, new Vector3(0f, -0.255f, 0.01f), new Vector3(0.036f, 0.032f, 0.036f), 0.016f));    // 손
            arm.Add("Top", p => RoundBox(p, new Vector3(0.01f * side, -0.055f, 0f), new Vector3(0.075f, 0.065f, 0.075f), 0.035f)); // 퍼프 소매
            arm.Add("Frill", p => RoundBox(p, new Vector3(0.006f * side, -0.125f, 0f), new Vector3(0.072f, 0.0125f, 0.072f), 0.02f)); // 프릴
            // 프릴 물결: 둘레를 따라 한 칸씩 아래로 늘어진 부분
            arm.Add("Frill", p =>
            {
                if (p.y > -0.137f || p.y < -0.15f) return false;
                var onRing = RoundBox(p, new Vector3(0.006f * side, -0.14f, 0f), new Vector3(0.072f, 0.02f, 0.072f), 0.02f)
                             && !RoundBox(p, new Vector3(0.006f * side, -0.14f, 0f), new Vector3(0.047f, 0.03f, 0.047f), 0.015f);
                var a = Mathf.Atan2(p.z, p.x - 0.006f * side);
                return onRing && Mathf.FloorToInt((a + Mathf.PI) / (2f * Mathf.PI) * 12f) % 2 == 0;
            });
            parts.Add(arm);
        }

        // --- 다리 (피벗 = 엉덩이 0.30): 치마 속, 흰 버선과 신발만 밑단 아래로 보임 ---
        foreach (var (name, side) in new[] { ("Leg_L", -1f), ("Leg_R", 1f) })
        {
            var leg = new Part
            {
                Name = name, Joint = "엉덩이", Pivot = new Vector3(LegX * side, Hip, 0f),
                BoundsMin = new Vector3(-0.07f, -0.31f, -0.07f), BoundsMax = new Vector3(0.07f, 0.01f, 0.11f),
            };
            leg.Add("Skin", p => RoundBox(p, new Vector3(0f, -0.11f, 0f), new Vector3(0.032f, 0.10f, 0.032f), 0.012f));
            leg.Add("Frill", p => RoundBox(p, new Vector3(0f, -0.235f, 0.003f), new Vector3(0.035f, 0.022f, 0.035f), 0.012f)); // 버선
            leg.Add("Shoe", p => RoundBox(p, new Vector3(0f, -0.278f, 0.022f), new Vector3(0.04f, 0.022f, 0.064f), 0.018f));
            parts.Add(leg);
        }

        foreach (var part in parts) part.Voxelize();
        return parts;
    }

    // ===== 프리팹 만들기 =====

    /// <summary>미리보기용 프리팹 (VoxelCharacterDetailed)</summary>
    public static GameObject Build() => BuildPrefab(PrefabPath, "VoxelCharacterDetailed");

    /// <summary>디테일 캐릭터 프리팹을 지정한 경로·이름으로 만든다. 게임 캐릭터는 VoxelCharacterBuilder가 이걸 호출한다.</summary>
    public static GameObject BuildPrefab(string prefabPath, string rootName)
    {
        var materials = new Dictionary<string, Material>();
        foreach (var pair in Palette)
        {
            var mat = VoxelCharacterBuilder.CreateMaterial(pair.Key, pair.Value);
            // 작은 복셀끼리 드리우는 그림자(앞머리 → 얼굴, 리본 끈 → 치마)가 얼룩처럼 보여서 받지 않음.
            // URP는 MeshRenderer.receiveShadows가 아니라 머티리얼의 이 옵션을 따른다
            mat.SetFloat("_ReceiveShadows", 0f);
            mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            EditorUtility.SetDirty(mat);
            materials[pair.Key] = mat;
        }

        var parts = DesignParts();
        Directory.CreateDirectory(MeshDir);

        var root = new GameObject(rootName);
        var model = VoxelCharacterBuilder.CreatePivot("Model", root.transform, Vector3.zero);
        var voxelCount = 0;
        var faceCount = 0;
        foreach (var part in parts)
        {
            voxelCount += part.Cells.Count;
            var pivot = VoxelCharacterBuilder.CreatePivot(part.Name, model, part.Pivot);
            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(pivot, false);
            meshGo.transform.localPosition = part.Offset;
            var built = BuildMesh(part, out var keys);
            faceCount += built.vertexCount / 4;
            var mesh = SaveMesh(built, $"{MeshDir}/{part.Name}.asset");
            meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.receiveShadows = false;
            var shared = new Material[keys.Count];
            for (var i = 0; i < keys.Count; i++) shared[i] = materials[keys[i]];
            renderer.sharedMaterials = shared;
        }

        VoxelCharacterBuilder.AddPhysics(root);
        root.AddComponent<PlayerController>();
        var animator = model.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = AnimationBuilder.EnsureController();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        WritePreviewData(parts);
        Debug.Log($"[DetailedCharacterBuilder] Saved {prefabPath} (복셀 {voxelCount}개, 보이는 면 {faceCount}개)");
        return prefab;
    }

    static readonly Vector3Int[] Directions =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
    };

    /// <summary>밖으로 드러난 면만 모아 메쉬를 만든다. 색마다 서브메쉬 하나.</summary>
    static Mesh BuildMesh(Part part, out List<string> keys)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new Dictionary<string, List<int>>();

        foreach (var cell in part.Cells)
        {
            if (!triangles.TryGetValue(cell.Value, out var tris)) triangles[cell.Value] = tris = new List<int>();
            var center = (Vector3)cell.Key * V;
            foreach (var dir in Directions)
            {
                if (part.Cells.ContainsKey(cell.Key + dir)) continue; // 붙어 있는 면은 안 보이므로 뺌
                AddFace(vertices, normals, tris, center, dir);
            }
        }

        keys = new List<string>(triangles.Keys);
        var mesh = new Mesh { name = part.Name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.subMeshCount = keys.Count;
        for (var i = 0; i < keys.Count; i++) mesh.SetTriangles(triangles[keys[i]], i);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddFace(List<Vector3> vertices, List<Vector3> normals, List<int> tris, Vector3 center, Vector3Int dir)
    {
        Vector3 n = dir;
        var u = Mathf.Abs(n.x) > 0.5f ? Vector3.up : Vector3.right;
        var w = Vector3.Cross(n, u);
        var h = V / 2f;
        var fc = center + n * h;
        var p0 = fc + (-u - w) * h;
        var p1 = fc + (u - w) * h;
        var p2 = fc + (u + w) * h;
        var p3 = fc + (-u + w) * h;

        var i = vertices.Count;
        vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);
        for (var k = 0; k < 4; k++) normals.Add(n);

        // Unity는 바깥에서 볼 때 시계 방향이 앞면 → Cross(b-a, c-a)가 바깥을 향하도록
        if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0f)
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        else
            tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing); // GUID 유지
        EditorUtility.SetDirty(existing);
        return existing;
    }

    /// <summary>
    /// 3D 미리보기 페이지 안의 데이터 부분(window.CHARACTER_DETAILED)을 새 복셀 데이터로 갈아 끼운다.
    /// 데이터를 페이지 안에 넣어서, 따로 된 파일을 읽지 못하는 환경에서도 보이게 한다.
    /// 밖에서 보이지 않는 안쪽 칸은 빼서 페이지를 가볍게 한다.
    /// </summary>
    static void WritePreviewData(List<Part> parts)
    {
        string F(float f) => f.ToString("0.####", CultureInfo.InvariantCulture);
        string Vec(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("window.CHARACTER_DETAILED = {");
        sb.AppendLine($"  voxel: {F(V)},");
        sb.Append("  palette: {");
        foreach (var pair in Palette) sb.Append($" {pair.Key}: [{F(pair.Value.r)},{F(pair.Value.g)},{F(pair.Value.b)}],");
        sb.AppendLine(" },");
        sb.AppendLine("  parts: [");
        foreach (var part in parts)
        {
            sb.Append($"    {{ name: \"{part.Name}\", joint: {(part.Joint == null ? "null" : $"\"{part.Joint}\"")}, pivot: {Vec(part.Pivot)}, offset: {Vec(part.Offset)}, total: {part.Cells.Count}, cells: [");
            foreach (var cell in part.Cells)
            {
                var hidden = true;
                foreach (var dir in Directions)
                    if (!part.Cells.ContainsKey(cell.Key + dir)) { hidden = false; break; }
                if (hidden) continue;
                sb.Append($"[{cell.Key.x},{cell.Key.y},{cell.Key.z},\"{cell.Value}\"],");
            }
            sb.AppendLine("] },");
        }
        sb.AppendLine("  ],");
        sb.AppendLine("};");

        if (!File.Exists(PreviewPage))
        {
            Debug.LogWarning($"[DetailedCharacterBuilder] {PreviewPage}이 없어 미리보기 데이터를 쓰지 않았습니다.");
            return;
        }
        var html = File.ReadAllText(PreviewPage, Encoding.UTF8);
        var start = html.IndexOf(DataStart, StringComparison.Ordinal);
        var end = html.IndexOf(DataEnd, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            Debug.LogWarning($"[DetailedCharacterBuilder] {PreviewPage}에서 데이터 표시(CHARACTER-DATA)를 찾지 못했습니다.");
            return;
        }
        var headerEnd = html.IndexOf("*/", start, StringComparison.Ordinal) + 2; // 시작 표시 주석 끝
        html = html.Substring(0, headerEnd) + sb + html.Substring(end);
        File.WriteAllText(PreviewPage, html, new UTF8Encoding(false));
    }

    // ===== 확인용 캡처 (batchmode) =====
    // Unity.exe -batchmode -quit -executeMethod DetailedCharacterBuilder.PreviewFromCommandLine -previewDir <폴더>
    public static void PreviewFromCommandLine()
    {
        try
        {
            AnimationBuilder.EnsureController();
            var prefab = Build();
            AssetDatabase.SaveAssets();

            var args = Environment.GetCommandLineArgs();
            var idx = Array.IndexOf(args, "-previewDir");
            if (idx < 0 || idx + 1 >= args.Length) return;
            var dir = args[idx + 1];

            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);

            // 캐릭터 확대: 맵에서 떨어진 곳에 잠시 놓고 찍음 (카메라 쪽 -Z를 바라봄)
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(0f, 180f, 0f));
            var p = go.transform.position;
            var look = p + Vector3.up * 0.55f;
            void Close(string file, Vector3 offset) =>
                PreviewCapture.Capture(Path.Combine(dir, file), p + offset, Quaternion.LookRotation(look - (p + offset)), 35f, 640, 640);
            Close("detail-front.png", new Vector3(0.7f, 0.9f, -1.9f));
            Close("detail-front-straight.png", new Vector3(0f, 0.6f, -2.1f));
            Close("detail-back.png", new Vector3(-0.8f, 0.9f, 1.9f));
            Close("detail-side.png", new Vector3(2.1f, 0.6f, 0f));
            // 얼굴 확대
            var face = p + Vector3.up * 0.8f;
            PreviewCapture.Capture(Path.Combine(dir, "detail-face.png"), face + new Vector3(0.15f, 0.05f, -0.9f),
                Quaternion.LookRotation(-new Vector3(0.15f, 0.05f, -0.9f)), 35f, 640, 640);

            // 애니메이션 자세 (걷기·점프에서 치마·팔이 어색하지 않은지)
            var model = go.transform.Find("Model").gameObject;
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Walk.anim").SampleAnimation(model, 0f);
            Close("detail-walk.png", new Vector3(0.7f, 0.9f, -1.9f));
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Jump.anim").SampleAnimation(model, 0.35f);
            Close("detail-jump.png", new Vector3(0.7f, 0.9f, -1.9f));
            // 자세 되돌리기 (Idle 클립에는 팔 회전이 없어서 직접 초기화)
            foreach (Transform pivot in model.transform) pivot.localRotation = Quaternion.identity;
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;

            // 게임 화면: 시작 위치에 놓고, 카메라를 25% 가까이 (사용자 결정) — 기존 캐릭터는 잠시 숨김 (씬은 저장하지 않음)
            var oldPlayer = GameObject.Find(VoxelCharacterBuilder.CharacterName);
            var start = oldPlayer.transform.position;
            oldPlayer.SetActive(false);
            go.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 180f, 0f));
            var offsetNear = new Vector3(0f, 5f, -4.5f) * 0.75f;
            PreviewCapture.Capture(Path.Combine(dir, "detail-game-near.png"), start + offsetNear, Quaternion.LookRotation(Vector3.up * 0.5f - offsetNear));

            UnityEngine.Object.DestroyImmediate(go);
            oldPlayer.SetActive(true);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
