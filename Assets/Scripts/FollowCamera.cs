using UnityEngine;

/// <summary>
/// 고정 각도 쿼터뷰 카메라. 대상의 위치만 부드럽게 따라가고 각도는 바뀌지 않는다.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 5f, -4.5f);
    [Tooltip("대상 발 위치에서 이 높이만큼 위를 바라본다")]
    [SerializeField] float lookHeight = 0.5f;
    [SerializeField] float smoothTime = 0.15f;
    [Tooltip("대상이 이 높이보다 아래로 떨어지면 카메라는 이 높이에서 멈춘다 (맵 높이)")]
    [SerializeField] float minTargetHeight = 0f;

    Vector3 velocity;

    public Transform Target
    {
        get => target;
        set { target = value; Snap(); }
    }

    public Vector3 Offset => offset;

    void Start() => Snap();

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, FollowPoint() + offset, ref velocity, smoothTime);
        transform.rotation = FixedRotation();
    }

    /// <summary>대상 위치로 즉시 이동한다.</summary>
    public void Snap()
    {
        if (target == null) return;
        transform.position = FollowPoint() + offset;
        transform.rotation = FixedRotation();
        velocity = Vector3.zero;
    }

    // 떨어질 때는 맵 높이에서 멈춤 (5단계 사용자 결정), 점프처럼 위로 올라갈 때는 따라감
    Vector3 FollowPoint()
    {
        var p = target.position;
        p.y = Mathf.Max(p.y, minTargetHeight);
        return p;
    }

    Quaternion FixedRotation() => Quaternion.LookRotation(Vector3.up * lookHeight - offset, Vector3.up);
}
