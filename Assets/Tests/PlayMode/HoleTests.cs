using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// 7단계 땅·구멍 확인: 16칸으로 시작, 흔들림 1초 → 떨어짐, 가장 오래된 구멍 다시 생김,
/// 난이도(주기 3→1초, 최대 구멍 1→14개), 캐릭터 주변 8칸 중 1칸은 남기는 규칙.
/// </summary>
public class HoleTests : PlayModeTestBase
{
    [UnityTest]
    public IEnumerator StartsWithAllSixteenTiles()
    {
        Assert.AreEqual(16, holeManager.Tiles.Count, "처음에는 16칸이 모두 있어야 합니다.");
        foreach (var tile in holeManager.Tiles)
        {
            Assert.AreEqual(TileState.Normal, tile.State);
            Assert.IsTrue(tile.GetComponent<Collider>().enabled);
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator ShakesForOneSecond_ThenFalls()
    {
        var tile = TileAt(2, 2);
        var mesh = tile.transform.Find("Mesh");
        var basePos = mesh.localPosition;

        tile.StartShake(1f);
        var maxOffset = 0f;
        for (var t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            maxOffset = Mathf.Max(maxOffset, Vector3.Distance(mesh.localPosition, basePos));
            yield return null;
        }
        Assert.AreEqual(TileState.Shaking, tile.State);
        Assert.IsTrue(tile.GetComponent<Collider>().enabled, "흔들리는 동안에는 아직 밟을 수 있어야 합니다.");
        Assert.Greater(maxOffset, 0.01f, "살짝 흔들려야 합니다.");
        Assert.Less(maxOffset, 0.1f, "크게 흔들리면 안 됩니다.");

        yield return new WaitForSeconds(0.3f);
        Assert.AreNotEqual(TileState.Shaking, tile.State, "1초 뒤에는 떨어져야 합니다.");
        Assert.IsFalse(tile.GetComponent<Collider>().enabled, "떨어지면 밟을 수 없어야 합니다.");

        yield return new WaitForSeconds(0.9f);
        Assert.AreEqual(TileState.Gone, tile.State);
    }

    [UnityTest]
    public IEnumerator StandingOnFallingTile_PlayerFalls()
    {
        TileAt(0, 0).StartShake(1f); // 캐릭터 발밑 (시작 칸)
        yield return new WaitForSeconds(1.8f);
        Assert.Less(player.transform.position.y, -1f, "발밑 칸이 떨어지면 캐릭터도 떨어져야 합니다.");
    }

    [UnityTest]
    public IEnumerator JumpingOffShakingTile_Survives()
    {
        TileAt(0, 0).StartShake(1f);
        yield return new WaitForSeconds(0.3f);
        Press(Key.D, Key.Space); // 옆 칸 (1,0)으로 점프
        yield return new WaitForSeconds(0.1f);
        Press(Key.D);
        yield return new WaitForSeconds(0.25f);
        ReleaseAll();
        yield return new WaitForSeconds(1.5f);

        Assert.AreEqual(0f, player.transform.position.y, 0.05f, "옆 칸으로 점프하면 살아남아야 합니다.");
        Assert.AreEqual(TileState.Gone, TileAt(0, 0).State);
    }

    [UnityTest]
    public IEnumerator OldestHoleRegenerates_WhenOverMax()
    {
        holeManager.enabled = true; // 떨어지는 신호를 받아 구멍 수를 관리
        holeManager.Elapsed = 0f;   // 최대 구멍 수 1개

        TileAt(3, 3).Fall();
        yield return new WaitForSeconds(0.9f);
        Assert.AreEqual(TileState.Gone, TileAt(3, 3).State);
        Assert.AreEqual(1, holeManager.HoleCount);

        TileAt(3, 2).Fall(); // 구멍이 2개가 되어 최대치(1)를 넘음 → 가장 오래된 (3,3)이 다시 생김
        yield return null;
        Assert.AreEqual(TileState.Rising, TileAt(3, 3).State, "가장 오래된 구멍이 아래에서 올라와야 합니다.");
        Assert.AreEqual(1, holeManager.HoleCount);

        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual(TileState.Normal, TileAt(3, 3).State);
        Assert.IsTrue(TileAt(3, 3).GetComponent<Collider>().enabled, "다시 생기면 밟을 수 있어야 합니다.");
    }

    [UnityTest]
    public IEnumerator Difficulty_IntervalAndMaxHoles_OverOneMinute()
    {
        Assert.AreEqual(3f, holeManager.IntervalAt(0f), 0.001f);
        Assert.AreEqual(2f, holeManager.IntervalAt(30f), 0.001f);
        Assert.AreEqual(1f, holeManager.IntervalAt(60f), 0.001f);
        Assert.AreEqual(1f, holeManager.IntervalAt(120f), 0.001f, "1분 뒤에는 1초로 유지");

        Assert.AreEqual(1, holeManager.MaxHolesAt(0f));
        Assert.AreEqual(14, holeManager.MaxHolesAt(60f));
        Assert.AreEqual(14, holeManager.MaxHolesAt(120f), "최대 구멍 수는 14개까지");
        Assert.Greater(holeManager.MaxHolesAt(30f), 1);
        Assert.Less(holeManager.MaxHolesAt(30f), 14);
        yield break;
    }

    [UnityTest]
    public IEnumerator SurvivalRule_KeepsOneNeighborAroundPlayer()
    {
        // 캐릭터는 모서리 (0,0). 주변 칸은 (1,0), (0,1), (1,1) 세 칸 → 두 칸을 구멍으로 만든다
        TileAt(1, 0).Fall();
        TileAt(0, 1).Fall();
        yield return new WaitForSeconds(0.9f);

        // 떨어뜨릴 칸을 계속 고르게 해도 마지막 이웃 (1,1)은 절대 고르지 않아야 한다
        var picks = 0;
        while (holeManager.TryDropTile()) picks++;

        Assert.AreEqual(TileState.Normal, TileAt(1, 1).State, "주변 8칸 중 1칸은 항상 남아야 합니다.");
        Assert.AreEqual(13, picks, "남은 13칸(발밑 포함)은 고를 수 있어야 합니다.");
    }

    [UnityTest]
    public IEnumerator AutoMode_DropsTilesOverTime()
    {
        holeManager.enabled = true;
        yield return new WaitForSeconds(3.3f);
        var changed = 0;
        foreach (var tile in holeManager.Tiles)
            if (tile.State != TileState.Normal) changed++;
        Assert.GreaterOrEqual(changed, 1, "3초 주기로 땅이 흔들리기 시작해야 합니다.");
    }
}
