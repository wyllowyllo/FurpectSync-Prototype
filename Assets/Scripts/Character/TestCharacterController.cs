using UnityEngine;

public class TestCharacterController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float accelerationTime = 0.15f;
    [SerializeField] private float decelerationTime = 0.1f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody capsuleRb;
    [SerializeField] private RagdollController ragdollController;

    private Transform cameraTransform;

    private Vector3 currentVelocity;
    private ERagdollState previousRagdollState;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Start()
    {
        previousRagdollState = ERagdollState.Animated;
        AssignCamera();
    }

    private void Update()
    {
        ERagdollState currentRagdollState = ragdollController.CurrentState;

        // 래그돌 복귀 감지 → 속도 초기화 (Fix 5)
        if (previousRagdollState != ERagdollState.Animated
            && currentRagdollState == ERagdollState.Animated)
        {
            currentVelocity = Vector3.zero;
            UpdateAnimator();
        }

        previousRagdollState = currentRagdollState;

        if (currentRagdollState != ERagdollState.Animated)
            return;

        Vector3 targetVelocity = GetCameraRelativeInput() * moveSpeed;
        Accelerate(targetVelocity);
        RotateToVelocity();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (ragdollController.CurrentState != ERagdollState.Animated)
            return;

        Vector3 velocity = capsuleRb.linearVelocity;
        velocity.x = currentVelocity.x;
        velocity.z = currentVelocity.z;
        capsuleRb.linearVelocity = velocity;
    }

    private Vector3 GetCameraRelativeInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (cameraTransform == null)
        {
            Vector3 worldDir = new Vector3(h, 0f, v);
            return worldDir.sqrMagnitude > 1f ? worldDir.normalized : worldDir;
        }

        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = cameraTransform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 direction = camRight * h + camForward * v;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private void Accelerate(Vector3 targetVelocity)
    {
        float smoothTime = targetVelocity.sqrMagnitude > currentVelocity.sqrMagnitude
            ? accelerationTime
            : decelerationTime;

        if (smoothTime > 0f)
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.deltaTime / smoothTime);
        else
            currentVelocity = targetVelocity;
    }

    private void RotateToVelocity()
    {
        if (currentVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(currentVelocity),
                Time.deltaTime * rotationSpeed);
        }
    }

    private void UpdateAnimator()
    {
        if (animator != null)
            animator.SetFloat(SpeedHash, currentVelocity.magnitude);
    }

    private void AssignCamera()
    {
        var tpsCam = FindAnyObjectByType<TpsCameraController>();
        if (tpsCam != null)
        {
            tpsCam.SetTarget(transform);
            cameraTransform = tpsCam.transform;
            return;
        }

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }
}
