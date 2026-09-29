using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 움직이는 구멍 관리 (7단계).
/// - 주기마다 보통 상태인 칸 하나를 골라 흔든다 (캐릭터 발밑 포함). 흔들림이 끝나면 떨어진다.
/// - 떨어진 순간 구멍 수가 그때의 최대치를 넘으면 가장 오래된 구멍 1개를 다시 생성한다.
/// - 난이도: rampTime(60초)에 걸쳐 주기 3초 → 1초, 최대 구멍 수 1개 → 14개.
/// - 살아남기 규칙: 캐릭터 주변 8칸(맵 밖 제외) 중 최소 1칸은 항상 보통 상태로 남긴다.
/// </summary>
public class HoleManager : MonoBehaviour
{
    [SerializeField] Transform tilesRoot;
    [SerializeField] Transform player;
    [SerializeField] GameManager gameManager;

    [Header("난이도 (rampTime에 걸쳐 일정하게 변함)")]
    [SerializeField] float startInterval = 3f;
    [SerializeField] float endInterval = 1f;
    [SerializeField] int startMaxHoles = 1;
    [SerializeField] int endMaxHoles = 14;
    [SerializeField] float rampTime = 60f;

    [Header("땅")]
    [SerializeField] float shakeTime = 1f;

    readonly Dictionary<Vector2Int, Tile> grid = new Dictionary<Vector2Int, Tile>();
    readonly List<Tile> holes = new List<Tile>(); // 떨어진 순서 (앞쪽이 가장 오래됨)
    Tile[] tiles;
    Vector3 origin; // 칸 (0,0)의 월드 위치
    float timer;

    /// <summary>게임 시작 후 지난 시간. (테스트에서 난이도를 바꿀 때도 사용)</summary>
    public float Elapsed { get; set; }
    public float CurrentInterval => IntervalAt(Elapsed);
    public int CurrentMaxHoles => MaxHolesAt(Elapsed);
    public IReadOnlyList<Tile> Tiles => tiles;
    public int HoleCount => holes.Count;

    public float IntervalAt(float time) => Mathf.Lerp(startInterval, endInterval, time / rampTime);
    public int MaxHolesAt(float time) => Mathf.RoundToInt(Mathf.Lerp(startMaxHoles, endMaxHoles, time / rampTime));

    void Awake()
    {
        tiles = tilesRoot.GetComponentsInChildren<Tile>();
        foreach (var tile in tiles)
        {
            grid[tile.Cell] = tile;
            if (tile.Cell == Vector2Int.zero) origin = tile.transform.position;
        }
    }

    void OnEnable() => Tile.Fell += OnTileFell;
    void OnDisable() => Tile.Fell -= OnTileFell;

    void Update()
    {
        // 게임이 진행 중일 때만 (메인 메뉴·게임 오버에서는 멈춤)
        if (gameManager != null && gameManager.State != GameState.Playing) return;
        Elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        if (timer >= CurrentInterval)
        {
            timer = 0f;
            TryDropTile();
        }
    }


    public Tile GetTile(Vector2Int cell) => grid.TryGetValue(cell, out var tile) ? tile : null;

    /// <summary>캐릭터가 서 있는 칸. 맵 밖이면 false.</summary>
    public bool TryGetPlayerCell(out Vector2Int cell)
    {
        var p = player.position - origin;
        cell = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
        return grid.ContainsKey(cell);
    }

    /// <summary>규칙을 지키는 보통 칸 중 하나를 골라 흔든다. 고를 칸이 없으면 false.</summary>
    public bool TryDropTile()
    {
        var candidates = new List<Tile>();
        foreach (var tile in tiles)
            if (tile.State == TileState.Normal && KeepsEscape(tile)) candidates.Add(tile);
        if (candidates.Count == 0) return false;

        candidates[Random.Range(0, candidates.Count)].StartShake(shakeTime);
        return true;
    }

    /// <summary>이 칸이 떨어져도 캐릭터 주변 8칸 중 보통 칸이 1칸 이상 남는지</summary>
    bool KeepsEscape(Tile candidate)
    {
        if (!TryGetPlayerCell(out var center)) return true; // 캐릭터가 맵 밖이면 규칙 없음

        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
        {
            if (dx == 0 && dz == 0) continue;
            var neighbor = GetTile(center + new Vector2Int(dx, dz));
            if (neighbor != null && neighbor != candidate && neighbor.State == TileState.Normal) return true;
        }
        return false;
    }

    void OnTileFell(Tile tile)
    {
        if (GetTile(tile.Cell) != tile) return; // 다른 맵의 칸
        holes.Add(tile);
        if (holes.Count > CurrentMaxHoles)
        {
            var oldest = holes[0];
            holes.RemoveAt(0);
            oldest.Regenerate();
        }
    }
}
