using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 재시작 확인: GAME OVER 뒤 3 → 2 → 1 카운트다운 후 씬을 다시 불러오고,
/// 점수·땅·코인은 처음으로 돌아오며 최고 점수는 남는다.
/// </summary>
public class RestartTests : PlayModeTestBase
{
    static readonly Vector3 StartPosition = new Vector3(-1.5f, 0f, -1.5f);

    int loadCount;
    float lastLoadTime;

    [UnitySetUp]
    public IEnumerator WatchSceneLoads()
    {
        loadCount = 0;
        SceneManager.sceneLoaded += OnSceneLoaded;
        yield break;
    }

    [TearDown]
    public void StopWatching() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loadCount++;
        lastLoadTime = Time.realtimeSinceStartup;
    }

    static Transform ResultPanel => GameObject.Find("UI").transform.Find("ResultPanel");
    static TMP_Text Countdown => ResultPanel.Find("CountdownText").GetComponent<TMP_Text>();
    static TMP_Text ScoreText => GameObject.Find("ScoreText").GetComponent<TMP_Text>();
    static TMP_Text BestText => GameObject.Find("BestText").GetComponent<TMP_Text>();

    IEnumerator FallOffEdge()
    {
        Press(Key.A);
        for (var t = 0f; t < 3f && !ResultPanel.gameObject.activeSelf; t += Time.deltaTime) yield return null;
        ReleaseAll();
    }

    /// <summary>결과가 나온 뒤 카운트다운 숫자와 재시작까지 걸린 시간을 기록한다.</summary>
    IEnumerator RecordCountdown(List<string> numbers, System.Action<float> onRestart)
    {
        Assert.IsTrue(ResultPanel.gameObject.activeSelf, "결과 화면이 나와 있어야 합니다.");
        var start = Time.realtimeSinceStartup;
        var loadsBefore = loadCount;
        while (loadCount == loadsBefore && Time.realtimeSinceStartup - start < 5f)
        {
            var text = Countdown.gameObject.activeInHierarchy ? Countdown.text : null;
            if (text != null && (numbers.Count == 0 || numbers[numbers.Count - 1] != text)) numbers.Add(text);
            yield return null;
        }
        onRestart(lastLoadTime - start);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator GameOver_CountsDown_ThenRestartsFresh()
    {
        TileAt(3, 3).Fall(); // 재시작 뒤 땅이 모두 돌아오는지 보기 위해 구멍 하나를 만들어 둠
        coinSpawner.SpawnAt(TileAt(2, 2));
        yield return FallOffEdge();

        var numbers = new List<string>();
        var restartAfter = 0f;
        yield return RecordCountdown(numbers, t => restartAfter = t);

        CollectionAssert.AreEqual(new[] { "3", "2", "1" }, numbers, "카운트다운은 3 → 2 → 1이어야 합니다.");
        Assert.AreEqual(3f, restartAfter, 0.3f, "게임이 끝나고 약 3초 뒤에 재시작해야 합니다.");

        yield return new WaitForSeconds(0.3f);
        FindSceneObjects();
        var p = player.transform.position;
        Assert.AreEqual(StartPosition.x, p.x, 0.05f, "캐릭터가 시작 위치로 돌아와야 합니다.");
        Assert.AreEqual(StartPosition.z, p.z, 0.05f);
        Assert.AreEqual(0f, p.y, 0.05f);
        Assert.IsFalse(ResultPanel.gameObject.activeSelf, "결과 화면이 사라져야 합니다.");
        Assert.AreEqual("SCORE 0", ScoreText.text, "점수가 0으로 돌아와야 합니다.");
        foreach (var tile in holeManager.Tiles) Assert.AreEqual(TileState.Normal, tile.State, "땅이 모두 돌아와야 합니다.");
        Assert.AreEqual(0, GameObject.Find("Level/Coins").transform.childCount, "코인은 다시 처음부터 생겨야 합니다.");

        StopSystems();
        var before = player.transform.position;
        yield return Hold(0.3f, Key.D);
        Assert.Greater(player.transform.position.x - before.x, 0.5f, "재시작 후 다시 움직일 수 있어야 합니다.");
    }

    [UnityTest]
    public IEnumerator BestScore_KeptAfterRestart()
    {
        coinSpawner.SpawnAt(TileAt(1, 0));
        yield return Hold(0.4f, Key.D);
        Teleport(CellPosition(0, 0));
        yield return new WaitForSeconds(0.2f);
        Assert.AreEqual("SCORE 100", ScoreText.text);

        yield return FallOffEdge();
        var numbers = new List<string>();
        yield return RecordCountdown(numbers, _ => { });
        yield return new WaitForSeconds(0.2f);

        Assert.AreEqual("SCORE 0", ScoreText.text);
        Assert.AreEqual("BEST 100", BestText.text, "재시작해도 최고 점수는 남아 있어야 합니다.");
    }

    [UnityTest]
    public IEnumerator Countdown_PopsWhenNumberChanges()
    {
        yield return FallOffEdge();
        var countdown = Countdown;
        yield return null;
        Assert.Greater(countdown.transform.localScale.x, 1.1f, "숫자가 나올 때 커져 있어야 합니다.");
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(1f, countdown.transform.localScale.x, 0.02f, "잠시 뒤 원래 크기로 돌아와야 합니다.");
    }
}
