using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public class FallGuysRagdoll : MonoBehaviour, IRagdollInput
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider capsuleCollider;
    [SerializeField] private Rigidbody capsuleRb;
    [SerializeField] private Transform hipsRoot;

    [Header("Ragdoll Settings")]
    [SerializeField] private float ragdollDuration = 0.8f;
    [SerializeField] private float blendDuration = 0.3f;
    [SerializeField] private float impactThreshold = 5f;
    [SerializeField] private float groundCheckDistance = 10f;
    [SerializeField] private LayerMask groundLayer;

    private RagdollState currentState = RagdollState.Animated;
    private Rigidbody[] ragdollRbs;
    private Collider[] ragdollCols;
    private Transform[] ragdollBones;

    private Vector3[] bonePositionSnapshot;
    private Quaternion[] boneRotationSnapshot;
    private float blendTimer;
    private Coroutine activeCoroutine;

    public RagdollState CurrentState => currentState;

    public event Action<Vector3, Vector3> OnImpactTriggered;
    public event Action OnDeathTriggered;

    private void Awake()
    {
        ragdollRbs = hipsRoot.GetComponentsInChildren<Rigidbody>();
        ragdollCols = hipsRoot.GetComponentsInChildren<Collider>();
        ragdollBones = ragdollRbs.Select(rb => rb.transform).ToArray();

        bonePositionSnapshot = new Vector3[ragdollBones.Length];
        boneRotationSnapshot = new Quaternion[ragdollBones.Length];

        SetRagdollActive(false);
    }

    private void SetRagdollActive(bool active)
    {
        foreach (var rb in ragdollRbs)
            rb.isKinematic = !active;

        foreach (var col in ragdollCols)
            col.enabled = active;

        capsuleCollider.enabled = !active;
        capsuleRb.isKinematic = active;

        animator.enabled = !active;
    }

    public void OnHitImpact(Vector3 impulse, Vector3 hitPoint)
    {
        if (impulse.magnitude < impactThreshold) return;
        if (currentState == RagdollState.Dead) return;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        currentState = RagdollState.Ragdoll;

        ResetRagdollVelocities();
        SetRagdollActive(true);

        Rigidbody closestRb = GetClosestBoneRb(hitPoint);
        closestRb.AddForce(impulse, ForceMode.Impulse);
        closestRb.AddTorque(
            UnityEngine.Random.insideUnitSphere * impulse.magnitude * 0.5f,
            ForceMode.Impulse);

        OnImpactTriggered?.Invoke(impulse, hitPoint);

        activeCoroutine = StartCoroutine(RagdollToBlendCoroutine());
    }

    public void OnDeath()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        currentState = RagdollState.Dead;
        SetRagdollActive(true);

        OnDeathTriggered?.Invoke();
    }

    private void ResetRagdollVelocities()
    {
        foreach (var rb in ragdollRbs)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private Rigidbody GetClosestBoneRb(Vector3 point)
    {
        return ragdollRbs
            .OrderBy(rb => Vector3.Distance(rb.position, point))
            .First();
    }

    private IEnumerator RagdollToBlendCoroutine()
    {
        yield return new WaitForSeconds(ragdollDuration);

        if (currentState == RagdollState.Ragdoll)
            StartBlendToAnimation();

        activeCoroutine = null;
    }

    private void StartBlendToAnimation()
    {
        currentState = RagdollState.BlendToAnim;

        for (int i = 0; i < ragdollBones.Length; i++)
        {
            bonePositionSnapshot[i] = ragdollBones[i].position;
            boneRotationSnapshot[i] = ragdollBones[i].rotation;
        }

        Vector3 hipsPos = ragdollBones[0].position;

        SetRagdollActive(false);

        float groundY = GetGroundY(hipsPos);
        capsuleRb.MovePosition(new Vector3(hipsPos.x, groundY, hipsPos.z));

        Vector3 hipsForward = ragdollBones[0].rotation * Vector3.forward;
        hipsForward.y = 0f;
        if (hipsForward.sqrMagnitude > 0.001f)
            capsuleRb.MoveRotation(Quaternion.LookRotation(hipsForward));

        animator.enabled = true;
        animator.CrossFade("GetUp", 0.1f);

        blendTimer = 0f;
    }

    private float GetGroundY(Vector3 origin)
    {
        Vector3 rayOrigin = origin + Vector3.up * 0.5f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                groundCheckDistance, groundLayer))
            return hit.point.y;

        return origin.y;
    }

    private void LateUpdate()
    {
        if (currentState != RagdollState.BlendToAnim) return;

        blendTimer += Time.deltaTime;
        float t = Mathf.Clamp01(blendTimer / blendDuration);

        for (int i = 0; i < ragdollBones.Length; i++)
        {
            ragdollBones[i].position = Vector3.Lerp(
                bonePositionSnapshot[i],
                ragdollBones[i].position,
                t);

            ragdollBones[i].rotation = Quaternion.Slerp(
                boneRotationSnapshot[i],
                ragdollBones[i].rotation,
                t);
        }

        if (t >= 1f)
        {
            currentState = RagdollState.Animated;
            animator.CrossFade("Locomotion", 0.1f);
        }
    }
}
