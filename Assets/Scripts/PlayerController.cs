using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rigidbody 기반 이동 / 점프.
/// 입력은 프로젝트 기본 Input Actions(Player/Move, Player/Jump)를 사용하고,
/// 이동 방향은 카메라 기준(W = 화면 위쪽)으로 계산한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 3f;      // 초당 칸 수 (1칸 = 1 unit)
    [SerializeField] float turnSpeed = 720f;    // 초당 회전 각도

    [Header("점프")]
    [SerializeField] float jumpHeight = 1.2f;

    [Header("바닥 체크")]
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] float groundCheckDistance = 0.15f;
    [SerializeField] LayerMask groundMask = ~0;

    [Tooltip("비워두면 Main Camera 기준으로 이동")]
    [SerializeField] Transform cameraTransform;

    Rigidbody rb;
    InputAction moveAction;
    InputAction jumpAction;
    Vector3 moveDirection;
    bool jumpRequested;

    public bool IsGrounded { get; private set; }
    public float HorizontalSpeed { get; private set; }

    /// <summary>false면 입력을 무시한다. (게임 종료 시 GameManager가 사용)</summary>
    public bool InputEnabled { get; set; } = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        moveAction = InputSystem.actions?.FindAction("Player/Move");
        jumpAction = InputSystem.actions?.FindAction("Player/Jump");
        if (moveAction == null || jumpAction == null)
            Debug.LogError("[PlayerController] Player/Move 또는 Player/Jump 액션을 찾을 수 없습니다.");
    }

    void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
    }

    void Update()
    {
        if (!InputEnabled || moveAction == null)
        {
            moveDirection = Vector3.zero;
            jumpRequested = false;
            return;
        }

        var input = moveAction.ReadValue<Vector2>();
        var cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
        var forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        var right = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
        moveDirection = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);

        if (jumpAction.WasPressedThisFrame()) jumpRequested = true;
    }

    void FixedUpdate()
    {
        IsGrounded = CheckGround();

        var velocity = rb.linearVelocity;
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;

        if (jumpRequested && IsGrounded)
        {
            velocity.y = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
            IsGrounded = false;
        }
        jumpRequested = false;

        rb.linearVelocity = velocity;
        HorizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            var target = Quaternion.LookRotation(moveDirection, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, target, turnSpeed * Time.fixedDeltaTime));
        }
    }

    bool CheckGround()
    {
        // 위로 올라가는 중에는 바닥으로 보지 않는다 (점프 직후 재점프 방지)
        if (rb.linearVelocity.y > 0.1f) return false;

        var origin = rb.position + Vector3.up * (groundCheckRadius + 0.05f);
        return Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out _,
            groundCheckDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * (groundCheckRadius + 0.05f - groundCheckDistance - 0.05f), groundCheckRadius);
    }
}
