using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 게임 흐름 확인 (7단계 기준): CLEAR 없이 떨어질 때까지 계속, GAME OVER, 입력 막기,
/// 떨어질 때 카메라, 점수, 최고 점수 저장.
/// </summary>
public class GameFlowTests : PlayModeTestBase
{
    GameObject resultPanel;
    TMP_Text resultText;

    static TMP_Text ScoreText => GameObject.Find("ScoreText").GetComponent<TMP_Text>();
    static TMP_Text BestText => GameObject.Find("BestText").GetComponent<TMP_Text>();

    [UnitySetUp]
    public IEnumerator FindResultUI()
    {
        FindResultPanel();
        yield break;
    }

    void FindResultPanel()
    {
        // 처음에는 꺼져 있으므로 GameObject.Find 대신 부모에서 찾는다
        resultPanel = GameObject.Find("UI").transform.Find("ResultPanel").gameObject;
        resultText = resultPanel.transform.Find("ResultText").GetComponent<TMP_Text>();
    }

    IEnumerator WaitUntilResult(float timeout)
    {
        for (var t = 0f; t < timeout && !resultPanel.activeSelf; t += Time.deltaTime) yield return null;
    }

    IEnumerator FallOffEdge()
    {
        Press(Key.A); // 시작 위치 (0,0)은 왼쪽 끝 → 맵 밖
        yield return WaitUntilResult(3f);
        ReleaseAll();
    }

    /// <summary>캐릭터 오른쪽 칸에 코인을 만들고 걸어가서 먹은 뒤 제자리로 돌아온다</summary>
    IEnumerator CollectOneCoin()
    {
        coinSpawner.SpawnAt(TileAt(1, 0));
        yield return Hold(0.4f, Key.D);
        Teleport(CellPosition(0, 0));
        yield return new WaitForSeconds(0.2f);
    }

    [UnityTest]
    public IEnumerator Start_NoResult_ScoreAndBestZero()
    {
        Assert.IsFalse(resultPanel.activeSelf, "시작할 때 결과 화면은 숨겨져 있어야 합니다.");
        Assert.AreEqual("SCORE 0", ScoreText.text);
        Assert.AreEqual("BEST 0", BestText.text);
        yield return Hold(0.3f, Key.Space); // 점프해도 게임 오버가 되지 않아야 함
        yield return new WaitForSeconds(1f);
        Assert.IsFalse(resultPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator FallingOffEdge_GameOver_CameraStaysAtMapHeight()
    {
        Press(Key.A);
        var cameraMinY = float.MaxValue;
        for (var t = 0f; t < 2.5f && !resultPanel.activeSelf; t += Time.deltaTime)
        {
            if (player.transform.position.y < -1f) cameraMinY = Mathf.Min(cameraMinY, Camera.main.transform.position.y);
            yield return null;
        }
        ReleaseAll();

        Assert.IsTrue(resultPanel.activeSelf, "y < -5로 떨어지면 결과 화면이 나와야 합니다.");
        Assert.AreEqual("GAME OVER", resultText.text);
        Assert.Less(player.transform.position.y, -4.9f, "GAME OVER는 y < -5에서 판정해야 합니다.");
        Assert.Greater(cameraMinY, 3.6f, "떨어질 때 카메라는 맵 높이(y 0 + 3.75)에서 멈춰야 합니다.");
    }

    [UnityTest]
    public IEnumerator FallingIntoHole_GameOver_AndInputBlocked()
    {
        yield return MakeHole(1, 0);
        yield return Hold(0.35f, Key.D); // 오른쪽 구멍으로 걸어 들어감
        yield return WaitUntilResult(2f);
        Assert.IsTrue(resultPanel.activeSelf);
        Assert.AreEqual("GAME OVER", resultText.text);

        var before = player.transform.position;
        yield return Hold(0.5f, Key.D, Key.W);
        var after = player.transform.position;
        Assert.Less(new Vector2(after.x - before.x, after.z - before.z).magnitude, 0.05f, "게임이 끝나면 입력이 막혀야 합니다.");
    }

    [UnityTest]
    public IEnumerator CollectingManyCoins_NoClear_KeepsPlaying()
    {
        for (var i = 0; i < 4; i++) yield return CollectOneCoin();
        Assert.AreEqual("SCORE 400", ScoreText.text, "코인을 먹을 때마다 100점씩 올라야 합니다.");
        Assert.IsFalse(resultPanel.activeSelf, "CLEAR 없이 계속 플레이해야 합니다.");
    }

    [UnityTest]
    public IEnumerator BestScore_SavedOnGameOver()
    {
        yield return CollectOneCoin();
        yield return FallOffEdge();
        Assert.AreEqual(100, PlayerPrefs.GetInt(BestScoreKey, 0), "GAME OVER 때 최고 점수가 저장되어야 합니다.");
        Assert.AreEqual("BEST 100", BestText.text);
    }

    [UnityTest]
    public IEnumerator BestScore_ShownFromSave_AndNotLowered()
    {
        // 이전 실행에서 500점을 저장해 둔 상태로 다시 시작
        PlayerPrefs.SetInt(BestScoreKey, 500);
        SceneManager.LoadScene("Main");
        yield return null;
        FindSceneObjects();
        gameManager.StartGame();
        StopSystems();
        FindResultPanel();
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual("BEST 500", BestText.text, "저장된 최고 점수를 보여줘야 합니다.");

        yield return CollectOneCoin();
        yield return FallOffEdge();
        Assert.AreEqual(500, PlayerPrefs.GetInt(BestScoreKey, 0), "더 낮은 점수로 최고 점수가 바뀌면 안 됩니다.");
        Assert.AreEqual("BEST 500", BestText.text);
        Assert.IsFalse(resultPanel.transform.Find("NewBestBadge").gameObject.activeSelf, "기록을 넘지 못하면 NEW BEST!가 없어야 합니다.");
        Assert.AreEqual("100", resultPanel.transform.Find("ResultScore").GetComponent<TMP_Text>().text);
        Assert.AreEqual("500", resultPanel.transform.Find("ResultBest").GetComponent<TMP_Text>().text);
    }

    [UnityTest]
    public IEnumerator GameOver_ShowsScoreBest_AndNewBest()
    {
        yield return CollectOneCoin();
        yield return FallOffEdge();
        Assert.AreEqual("100", resultPanel.transform.Find("ResultScore").GetComponent<TMP_Text>().text, "게임 오버 화면에 이번 점수가 보여야 합니다.");
        Assert.AreEqual("100", resultPanel.transform.Find("ResultBest").GetComponent<TMP_Text>().text, "게임 오버 화면에 최고 점수가 보여야 합니다.");
        Assert.IsTrue(resultPanel.transform.Find("NewBestBadge").gameObject.activeSelf, "기록을 넘으면 NEW BEST!가 보여야 합니다.");
        foreach (var name in new[] { "RetryButton", "MainMenuButton", "QuitButton" })
            Assert.IsNotNull(resultPanel.transform.Find(name), $"{name} 버튼이 있어야 합니다.");
    }
}
