using System;
using UnityEngine;

public enum TileState
{
    Normal,   // 밟을 수 있음
    Shaking,  // 곧 떨어짐 (아직 밟을 수 있음)
    Falling,  // 떨어지는 중 (밟을 수 없음)
    Gone,     // 구멍
    Rising,   // 아래에서 올라오는 중 (아직 밟을 수 없음)
}

/// <summary>
/// 땅 한 칸. 흔들림(1초) → 떨어짐 → 없음 → 아래에서 올라와 다시 생김.
/// 발판 판정(콜라이더)은 떨어지기 시작할 때 꺼지고, 다 올라왔을 때 켜진다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class Tile : MonoBehaviour
{
    /// <summary>땅이 떨어지기 시작할 때 (HoleManager, CoinSpawner가 받음)</summary>
    public static event Action<Tile> Fell;

    [Tooltip("흔들림·떨어짐·올라옴 연출을 적용할 모델")]
    [SerializeField] Transform visual;
    [Tooltip("맵에서의 칸 좌표 (x = 왼쪽→오른쪽, y = 아래→위)")]
    [SerializeField] Vector2Int cell;

    [Header("흔들림")]
    [SerializeField] float shakeAmplitude = 0.04f;
    [SerializeField] float shakeFrequency = 35f;

    [Header("떨어짐")]
    [SerializeField] float fallGravity = 20f;
    [SerializeField] float fallTime = 0.8f;

    [Header("다시 생김")]
    [SerializeField] float riseTime = 0.3f;
    [SerializeField] float riseDepth = 1.5f;

    public TileState State { get; private set; } = TileState.Normal;
    public Vector2Int Cell => cell;

    BoxCollider solid;
    Renderer[] renderers;
    Vector3 visualBase;
    float timer;
    float shakeDuration;
    float fallSpeed;

    void Awake()
    {
        solid = GetComponent<BoxCollider>();
        if (visual == null) visual = transform;
        renderers = visual.GetComponentsInChildren<Renderer>();
        visualBase = visual.localPosition;
    }

    /// <summary>흔들기 시작한다. duration초 뒤에 떨어진다.</summary>
    public void StartShake(float duration)
    {
        if (State != TileState.Normal) return;
        State = TileState.Shaking;
        shakeDuration = duration;
        timer = 0f;
    }

    /// <summary>바로 떨어뜨린다.</summary>
    public void Fall()
    {
        if (State != TileState.Normal && State != TileState.Shaking) return;
        State = TileState.Falling;
        visual.localPosition = visualBase;
        solid.enabled = false;
        fallSpeed = 0f;
        timer = 0f;
        Fell?.Invoke(this);
    }

    /// <summary>아래에서 올라오며 다시 생긴다.</summary>
    public void Regenerate()
    {
        if (State != TileState.Gone && State != TileState.Falling) return;
        State = TileState.Rising;
        SetVisible(true);
        visual.localPosition = visualBase + Vector3.down * riseDepth;
        timer = 0f;
    }

    void Update()
    {
        switch (State)
        {
            case TileState.Shaking:
                timer += Time.deltaTime;
                var t = timer * shakeFrequency;
                visual.localPosition = visualBase + new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t * 1.3f)) * shakeAmplitude;
                if (timer >= shakeDuration) Fall();
                break;

            case TileState.Falling:
                timer += Time.deltaTime;
                fallSpeed += fallGravity * Time.deltaTime;
                visual.localPosition += Vector3.down * (fallSpeed * Time.deltaTime);
                if (timer >= fallTime)
                {
                    State = TileState.Gone;
                    SetVisible(false);
                    visual.localPosition = visualBase;
                }
                break;

            case TileState.Rising:
                timer += Time.deltaTime;
                var k = Mathf.Clamp01(timer / riseTime);
                var ease = 1f - (1f - k) * (1f - k);
                visual.localPosition = visualBase + Vector3.down * (riseDepth * (1f - ease));
                if (k >= 1f)
                {
                    State = TileState.Normal;
                    visual.localPosition = visualBase;
                    solid.enabled = true;
                }
                break;
        }
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
    }
}
