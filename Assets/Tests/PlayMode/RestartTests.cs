using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 6단계 확인: 게임이 끝나면 3 → 2 → 1 카운트다운 후 씬을 다시 불러오고, 모든 상태가 처음으로 돌아온다.
/// </summary>
public class RestartTests : PlayModeTestBase
{
    static readonly Vector3 StartPosition = new Vector3(-1.5f, 0f, -1.5f);
    static readonly Vector3[] ItemPositions =
    {
        new Vector3(-0.5f, 0f, -0.5f),
        new Vector3(0.5f, 0f, 0.5f),
        new Vector3(1.5f, 0f, 1.5f),
    };

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
    static TMP_Text ItemText => GameObject.Find("ItemText").GetComponent<TMP_Text>();

    IEnumerator CollectAll()
    {
        foreach (var p in ItemPositions)
        {
            Teleport(p);
            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>결과가 나온 뒤 카운트다운 숫자와 재시작 시점을 기록한다.</summary>
    IEnumerator RecordCountdown(System.Collections.Generic.List<string> numbers, System.Action<float> onRestart)
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
    public IEnumerator GameOver_CountsDown_ThenRestarts()
    {
        Press(Key.A); // 맵 밖으로 떨어짐
        for (var t = 0f; t < 3f && !ResultPanel.gameObject.activeSelf; t += Time.deltaTime) yield return null;
        ReleaseAll();

        var numbers = new System.Collections.Generic.List<string>();
        var restartAfter = 0f;
        yield return RecordCountdown(numbers, t => restartAfter = t);

        CollectionAssert.AreEqual(new[] { "3", "2", "1" }, numbers, "카운트다운은 3 → 2 → 1이어야 합니다.");
        Assert.AreEqual(3f, restartAfter, 0.3f, "게임이 끝나고 약 3초 뒤에 재시작해야 합니다.");
        yield return AssertFreshStart();
    }

    [UnityTest]
    public IEnumerator Clear_CountsDown_ThenRestarts_WithItemsBack()
    {
        yield return CollectAll();
        Assert.AreEqual("CLEAR", ResultPanel.Find("ResultText").GetComponent<TMP_Text>().text);

        var numbers = new System.Collections.Generic.List<string>();
        var restartAfter = 0f;
        yield return RecordCountdown(numbers, t => restartAfter = t);

        CollectionAssert.AreEqual(new[] { "3", "2", "1" }, numbers);
        Assert.AreEqual(3f, restartAfter, 0.3f);
        yield return AssertFreshStart();
    }

    [UnityTest]
    public IEnumerator Countdown_PopsWhenNumberChanges()
    {
        Press(Key.A);
        for (var t = 0f; t < 3f && !ResultPanel.gameObject.activeSelf; t += Time.deltaTime) yield return null;
        ReleaseAll();

        var countdown = Countdown;
        yield return null;
        Assert.Greater(countdown.transform.localScale.x, 1.1f, "숫자가 나올 때 커져 있어야 합니다.");
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(1f, countdown.transform.localScale.x, 0.02f, "잠시 뒤 원래 크기로 돌아와야 합니다.");
    }

    /// <summary>재시작 직후: 새 씬의 캐릭터·아이템·UI가 처음 상태인지 확인</summary>
    IEnumerator AssertFreshStart()
    {
        yield return new WaitForSeconds(0.3f); // 착지 대기
        player = GameObject.Find("VoxelCharacter");
        rb = player.GetComponent<Rigidbody>();

        var p = player.transform.position;
        Assert.AreEqual(StartPosition.x, p.x, 0.05f, "캐릭터가 시작 위치로 돌아와야 합니다.");
        Assert.AreEqual(StartPosition.z, p.z, 0.05f);
        Assert.AreEqual(0f, p.y, 0.05f);
        Assert.IsFalse(ResultPanel.gameObject.activeSelf, "결과 화면이 사라져야 합니다.");
        Assert.AreEqual("ITEM 0 / 3", ItemText.text, "아이템 개수가 0으로 돌아와야 합니다.");
        Assert.AreEqual(3, GameObject.Find("Level/Items").transform.childCount, "금화 3개가 다시 있어야 합니다.");

        // 다시 조작할 수 있어야 함
        var before = player.transform.position;
        yield return Hold(0.3f, Key.D);
        Assert.Greater(player.transform.position.x - before.x, 0.5f, "재시작 후 다시 움직일 수 있어야 합니다.");
    }
}
