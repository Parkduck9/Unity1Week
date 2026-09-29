using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode 테스트 공통 준비: Main 씬을 불러오고 가상 키보드로 입력을 넣는다.
/// </summary>
public abstract class PlayModeTestBase
{
    protected Keyboard keyboard;
    protected GameObject player;
    protected Rigidbody rb;

    InputSettings.BackgroundBehavior oldBackground;
    InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // batchmode에서는 창 포커스가 없으므로 포커스와 무관하게 입력을 받게 한다
        oldBackground = InputSystem.settings.backgroundBehavior;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        keyboard = InputSystem.AddDevice<Keyboard>();
        SceneManager.LoadScene("Main");
        yield return null;
        yield return null;

        player = GameObject.Find("VoxelCharacter");
        Assert.IsNotNull(player, "VoxelCharacter가 씬에 없습니다.");
        rb = player.GetComponent<Rigidbody>();
        yield return new WaitForSeconds(0.3f); // 착지 대기
    }

    [TearDown]
    public void TearDown()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = oldBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
    }

    protected void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    protected void ReleaseAll() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());

    protected IEnumerator Hold(float seconds, params Key[] keys)
    {
        Press(keys);
        yield return new WaitForSeconds(seconds);
        ReleaseAll();
        yield return new WaitForSeconds(0.2f);
    }

    protected void Teleport(Vector3 position)
    {
        rb.linearVelocity = Vector3.zero;
        rb.position = position;
        player.transform.position = position;
    }
}
