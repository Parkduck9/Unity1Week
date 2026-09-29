using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코인 생성 (7단계).
/// - interval(1.5초)마다 보통 상태인 빈 칸 위에 코인을 하나 만든다. 여러 개가 동시에 있을 수 있다.
/// - 캐릭터가 서 있는 칸, 이미 코인이 있는 칸은 제외.
/// - 코인이 있는 칸이 떨어지면 코인도 함께 떨어진다.
/// </summary>
public class CoinSpawner : MonoBehaviour
{
    [SerializeField] Item coinPrefab;
    [SerializeField] HoleManager holeManager;
    [SerializeField] GameManager gameManager;
    [SerializeField] Transform coinsRoot;
    [SerializeField] float interval = 1.5f;
    [SerializeField] float height = 0.5f;

    readonly Dictionary<Tile, Item> coins = new Dictionary<Tile, Item>();
    float timer;

    public int ActiveCoinCount
    {
        get { CleanUp(); return coins.Count; }
    }

    void OnEnable() => Tile.Fell += OnTileFell;
    void OnDisable() => Tile.Fell -= OnTileFell;

    void Update()
    {
        // 게임이 진행 중일 때만 (메인 메뉴·게임 오버에서는 멈춤)
        if (gameManager != null && gameManager.State != GameState.Playing) return;
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            TrySpawn();
        }
    }

    /// <summary>규칙에 맞는 칸 중 무작위로 하나에 코인을 만든다. 칸이 없으면 null.</summary>
    public Item TrySpawn()
    {
        CleanUp();
        holeManager.TryGetPlayerCell(out var playerCell);
        var candidates = new List<Tile>();
        foreach (var tile in holeManager.Tiles)
        {
            if (tile.State != TileState.Normal || coins.ContainsKey(tile) || tile.Cell == playerCell) continue;
            candidates.Add(tile);
        }
        return candidates.Count == 0 ? null : SpawnAt(candidates[Random.Range(0, candidates.Count)]);
    }

    /// <summary>정해진 칸 위에 코인을 만든다.</summary>
    public Item SpawnAt(Tile tile)
    {
        var coin = Instantiate(coinPrefab, tile.transform.position + Vector3.up * height, Quaternion.identity, coinsRoot);
        coins[tile] = coin;
        return coin;
    }

    void OnTileFell(Tile tile)
    {
        if (coins.TryGetValue(tile, out var coin))
        {
            if (coin != null) coin.Drop();
            coins.Remove(tile);
        }
    }

    // 먹었거나 수명이 끝나 사라진 코인 정리
    void CleanUp()
    {
        var gone = new List<Tile>();
        foreach (var pair in coins)
            if (pair.Value == null || pair.Value.IsCollected) gone.Add(pair.Key);
        foreach (var tile in gone) coins.Remove(tile);
    }
}
