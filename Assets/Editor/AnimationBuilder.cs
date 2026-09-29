using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 캐릭터의 Idle / Walk / Jump 애니메이션 클립과 Animator Controller를 만든다.
/// Animator는 캐릭터의 Model 오브젝트에 붙으며, 경로 ""는 Model 자신이다.
/// 메뉴: Tools > Voxel > Build Animations
/// </summary>
public static class AnimationBuilder
{
    const string Dir = "Assets/Animations";
    public const string ControllerPath = Dir + "/Player.controller";

    public const string SpeedParam = "Speed";
    public const string GroundedParam = "Grounded";

    // 걷기: 다리는 치마를 뚫고 나오지 않도록 작게, 팔은 크게
    const float WalkCycle = 0.6f;
    const float WalkArmAngle = 35f;
    const float WalkLegAngle = 15f;
    const float WalkBob = 0.03f;

    // Idle: 숨쉬기 + 머리 살짝 기울이기
    const float IdleCycle = 3f;
    const float IdleBreath = 0.02f;   // 세로 스케일 변화
    const float IdleHeadTilt = 4f;

    // Jump: 팔 올리기 + 몸 늘이기
    const float JumpArmAngle = 140f;
    const float JumpRaiseTime = 0.15f;
    const float JumpLength = 0.35f;
    const float JumpStretch = 0.10f;  // 세로로 늘어나는 정도

    [MenuItem("Tools/Voxel/Build Animations")]
    public static void BuildMenu()
    {
        Build();
        AssetDatabase.SaveAssets();
    }

    /// <summary>컨트롤러가 없으면 만든다.</summary>
    public static AnimatorController EnsureController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        return controller != null ? controller : Build();
    }

    public static AnimatorController Build()
    {
        Directory.CreateDirectory(Dir);
        var idle = SaveClip(CreateIdle(), "Idle");
        var walk = SaveClip(CreateWalk(), "Walk");
        var jump = SaveClip(CreateJump(), "Jump");
        var controller = CreateController(idle, walk, jump);
        Debug.Log($"[AnimationBuilder] Saved {ControllerPath}");
        return controller;
    }

    // ===== 클립 =====

    static AnimationClip CreateIdle()
    {
        var clip = new AnimationClip { name = "Idle" };
        var t = IdleCycle;
        SetCurve(clip, "", "m_LocalScale.y", (0f, 1f), (t / 2f, 1f + IdleBreath), (t, 1f));
        SetCurve(clip, "Head", "localEulerAnglesRaw.z", (0f, 0f), (t / 2f, IdleHeadTilt), (t, 0f));
        SetLoop(clip, true);
        return clip;
    }

    static AnimationClip CreateWalk()
    {
        var clip = new AnimationClip { name = "Walk" };
        var t = WalkCycle;
        var h = t / 2f;
        // Unity x축 회전: 음수 = 앞으로, 양수 = 뒤로. 왼팔과 오른다리가 함께 앞으로 나간다
        SetCurve(clip, "Arm_L", "localEulerAnglesRaw.x", (0f, -WalkArmAngle), (h, WalkArmAngle), (t, -WalkArmAngle));
        SetCurve(clip, "Arm_R", "localEulerAnglesRaw.x", (0f, WalkArmAngle), (h, -WalkArmAngle), (t, WalkArmAngle));
        SetCurve(clip, "Leg_L", "localEulerAnglesRaw.x", (0f, WalkLegAngle), (h, -WalkLegAngle), (t, WalkLegAngle));
        SetCurve(clip, "Leg_R", "localEulerAnglesRaw.x", (0f, -WalkLegAngle), (h, WalkLegAngle), (t, -WalkLegAngle));
        // 한 걸음마다 한 번씩 위로 튐
        SetCurve(clip, "", "m_LocalPosition.y", (0f, 0f), (t / 4f, WalkBob), (h, 0f), (t * 3f / 4f, WalkBob), (t, 0f));
        SetLoop(clip, true);
        return clip;
    }

    static AnimationClip CreateJump()
    {
        var clip = new AnimationClip { name = "Jump" };
        var r = JumpRaiseTime;
        var e = JumpLength;
        // 팔을 옆으로 들어 올림 (Unity z축 회전: 왼팔은 음수, 오른팔은 양수가 바깥쪽)
        SetCurve(clip, "Arm_L", "localEulerAnglesRaw.z", (0f, 0f), (r, -JumpArmAngle), (e, -JumpArmAngle));
        SetCurve(clip, "Arm_R", "localEulerAnglesRaw.z", (0f, 0f), (r, JumpArmAngle), (e, JumpArmAngle));
        // 몸 늘이기: 세로로 늘고 가로로 살짝 줄었다가 돌아옴 (발 위치 기준)
        var squeeze = 1f - JumpStretch * 0.5f;
        SetCurve(clip, "", "m_LocalScale.y", (0f, 1f), (r * 0.6f, 1f + JumpStretch), (e, 1f));
        SetCurve(clip, "", "m_LocalScale.x", (0f, 1f), (r * 0.6f, squeeze), (e, 1f));
        SetCurve(clip, "", "m_LocalScale.z", (0f, 1f), (r * 0.6f, squeeze), (e, 1f));
        SetLoop(clip, false); // 끝나면 마지막 자세(팔 올림)를 유지
        return clip;
    }

    static void SetCurve(AnimationClip clip, string path, string property, params (float time, float value)[] keys)
    {
        var frames = new Keyframe[keys.Length];
        for (var i = 0; i < keys.Length; i++) frames[i] = new Keyframe(keys[i].time, keys[i].value);
        var curve = new AnimationCurve(frames);
        for (var i = 0; i < frames.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }

    static void SetLoop(AnimationClip clip, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    static AnimationClip SaveClip(AnimationClip clip, string name)
    {
        var path = $"{Dir}/{name}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        // 기존 에셋을 덮어써서 GUID(컨트롤러 연결)를 유지한다
        EditorUtility.CopySerialized(clip, existing);
        existing.name = name;
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // ===== Animator Controller =====

    static AnimatorController CreateController(AnimationClip idle, AnimationClip walk, AnimationClip jump)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath); // 매번 같은 구성으로 새로 만든다 (클립은 유지)

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);
        controller.AddParameter(GroundedParam, AnimatorControllerParameterType.Bool);
        // 시작할 때 바로 Jump로 넘어가지 않도록 Grounded 기본값을 true로
        var parameters = controller.parameters;
        parameters[1].defaultBool = true;
        controller.parameters = parameters;

        var sm = controller.layers[0].stateMachine;
        var idleState = sm.AddState("Idle", new Vector3(300f, 0f));
        var walkState = sm.AddState("Walk", new Vector3(300f, 120f));
        var jumpState = sm.AddState("Jump", new Vector3(600f, 60f));
        idleState.motion = idle;
        walkState.motion = walk;
        jumpState.motion = jump;
        sm.defaultState = idleState;

        const float moveThreshold = 0.1f;

        // Idle ↔ Walk
        AddTransition(idleState, walkState, 0.1f, t => t.AddCondition(AnimatorConditionMode.Greater, moveThreshold, SpeedParam));
        AddTransition(walkState, idleState, 0.15f, t => t.AddCondition(AnimatorConditionMode.Less, moveThreshold, SpeedParam));

        // Any State → Jump: 공중에 있으면 (점프해서 뜨거나, 맵 밖·구멍으로 떨어질 때)
        var toJump = sm.AddAnyStateTransition(jumpState);
        toJump.hasExitTime = false;
        toJump.duration = 0.05f;
        toJump.canTransitionToSelf = false;
        toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParam);

        // 착지하면 Idle 또는 Walk로
        AddTransition(jumpState, idleState, 0.1f, t =>
        {
            t.AddCondition(AnimatorConditionMode.If, 0f, GroundedParam);
            t.AddCondition(AnimatorConditionMode.Less, moveThreshold, SpeedParam);
        });
        AddTransition(jumpState, walkState, 0.1f, t =>
        {
            t.AddCondition(AnimatorConditionMode.If, 0f, GroundedParam);
            t.AddCondition(AnimatorConditionMode.Greater, moveThreshold, SpeedParam);
        });

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void AddTransition(AnimatorState from, AnimatorState to, float duration, Action<AnimatorStateTransition> conditions)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        conditions(t);
    }

    // ===== 확인용 자세 캡처 =====

    /// <summary>각 클립의 주요 시점을 캐릭터에 적용해 이미지로 저장한다.</summary>
    public static void CapturePoses(string dir)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VoxelCharacterBuilder.PrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(0f, 180f, 0f));
        var model = go.transform.Find("Model").gameObject;
        var p = go.transform.position;
        var camPos = p + new Vector3(1.3f, 0.9f, -2.2f);
        var camRot = Quaternion.LookRotation(p + Vector3.up * 0.6f - camPos);

        void Pose(string file, string clipName, float time)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Dir}/{clipName}.anim");
            clip.SampleAnimation(model, time);
            PreviewCapture.Capture(Path.Combine(dir, file), camPos, camRot, 35f, 480, 480);
        }

        Pose("pose-idle-0.png", "Idle", 0f);
        Pose("pose-idle-mid.png", "Idle", IdleCycle / 2f);
        Pose("pose-walk-0.png", "Walk", 0f);
        Pose("pose-walk-half.png", "Walk", WalkCycle / 2f);
        Pose("pose-walk-bob.png", "Walk", WalkCycle / 4f);
        Pose("pose-jump-stretch.png", "Jump", JumpRaiseTime * 0.6f);
        Pose("pose-jump-end.png", "Jump", JumpLength);

        UnityEngine.Object.DestroyImmediate(go);
    }
}
