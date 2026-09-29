using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// 7단계 코인 확인: 생성(1.5초마다, 여러 개), 수명 3초, 2초 안에 먹으면 100점, 2초 뒤 회색·깜빡임 0점,
/// 떨어지는 칸 위의 코인은 함께 떨어짐.
/// </summary>
public class ItemTests : PlayModeTestBase
{
    static Transform CoinsRoot => GameObject.Find("Level/Coins").transform;
    static TMP_Text ScoreText => GameObject.Find("ScoreText").GetComponent<TMP_Text>();

    [UnityTest]
    public IEnumerator NoCoinsAtStart_ThenSpawnEveryInterval_Several()
    {
        Assert.AreEqual(0, CoinsRoot.childCount, "처음에는 코인이 없어야 합니다.");
        coinSpawner.enabled = true;

        yield return new WaitForSeconds(1.7f);
        Assert.AreEqual(1, CoinsRoot.childCount, "1.5초 뒤 코인이 하나 생겨야 합니다.");

        yield return new WaitForSeconds(1.5f);
        Assert.GreaterOrEqual(CoinsRoot.childCount, 2, "코인이 여러 개 동시에 있을 수 있어야 합니다.");
    }

    [UnityTest]
    public IEnumerator SpawnedCoin_OnNormalTile_NotPlayerTile()
    {
        for (var i = 0; i < 10; i++)
        {
            var coin = coinSpawner.TrySpawn();
            Assert.IsNotNull(coin);
            var p = coin.transform.position;
            Assert.AreEqual(0.5f, p.y, 0.01f, "코인은 바닥에서 0.5 높이에 생겨야 합니다.");
            Assert.IsFalse(Mathf.Approximately(p.x, -1.5f) && Mathf.Approximately(p.z, -1.5f), "캐릭터가 서 있는 칸에는 생기면 안 됩니다.");
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator CollectWithinTwoSeconds_Gives100()
    {
        Assert.AreEqual("SCORE 0", ScoreText.text);
        coinSpawner.SpawnAt(TileAt(1, 0)); // 시작 칸 (0,0)의 오른쪽
        yield return Hold(0.4f, Key.D);
        Assert.AreEqual("SCORE 100", ScoreText.text, "2초 안에 먹으면 100점이어야 합니다.");
    }

    [UnityTest]
    public IEnumerator AfterTwoSeconds_GrayAndBlinking_GivesZero()
    {
        var coin = coinSpawner.SpawnAt(TileAt(2, 0));
        var renderer = coin.GetComponentInChildren<Renderer>();
        var gold = renderer.sharedMaterial;

        yield return new WaitForSeconds(1.8f);
        Assert.IsFalse(coin.IsExpired);
        Assert.AreEqual(gold, renderer.sharedMaterial, "2초 전에는 금색이어야 합니다.");

        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(coin.IsExpired, "2초가 지나면 점수 시간이 끝나야 합니다.");
        Assert.AreNotEqual(gold, renderer.sharedMaterial, "2초가 지나면 회색으로 바뀌어야 합니다.");

        bool sawOn = false, sawOff = false;
        for (var t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            sawOn |= renderer.enabled;
            sawOff |= !renderer.enabled;
            yield return null;
        }
        Assert.IsTrue(sawOn && sawOff, "2초가 지나면 깜빡여야 합니다.");

        Teleport(CellPosition(2, 0));
        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(coin == null || coin.IsCollected, "늦게라도 먹을 수는 있어야 합니다.");
        Assert.AreEqual("SCORE 0", ScoreText.text, "2초가 지난 뒤 먹으면 0점이어야 합니다.");
    }

    [UnityTest]
    public IEnumerator CoinDisappearsAfterThreeSeconds()
    {
        var coin = coinSpawner.SpawnAt(TileAt(3, 3));
        yield return new WaitForSeconds(2.8f);
        Assert.IsTrue(coin != null, "3초 전에는 남아 있어야 합니다.");
        yield return new WaitForSeconds(0.4f);
        Assert.IsTrue(coin == null, "3초가 지나면 사라져야 합니다.");
    }

    [UnityTest]
    public IEnumerator CoinOnFallingTile_FallsWithIt()
    {
        coinSpawner.enabled = true; // 칸이 떨어지는 신호를 받으려면 켜져 있어야 함
        var coin = coinSpawner.SpawnAt(TileAt(2, 1));
        var y0 = coin.transform.position.y;

        TileAt(2, 1).Fall();
        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(coin.IsDropped, "칸이 떨어지면 코인도 함께 떨어져야 합니다.");
        Assert.Less(coin.transform.position.y, y0 - 0.2f);

        yield return new WaitForSeconds(0.7f);
        Assert.IsTrue(coin == null, "떨어진 코인은 사라져야 합니다.");
        Assert.AreEqual("SCORE 0", ScoreText.text);
    }
}
