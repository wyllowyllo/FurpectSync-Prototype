using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public class RagdollController : MonoBehaviour, IRagdollInput
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider capsuleCollider;
    [SerializeField] private Rigidbody capsuleRb;
    [SerializeField] private Transform hipsRoot;
    [SerializeField] private UpperBodyPhysics upperBodyPhysics;

    [Header("Ragdoll Settings")]
    [SerializeField] private float minRagdollDuration = 0.5f;
    [SerializeField] private float maxRagdollDuration = 2.0f;
    [SerializeField] private float durationPerImpulse = 0.1f;
    [SerializeField] private float blendDuration = 0.3f;
    [SerializeField] private float groundCheckDistance = 10f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private bool useGetUpAnimation = true;

    [Header("Impact Thresholds")]
    [SerializeField] private float ragdollThreshold = 8f;

    private ERagdollState currentState = ERagdollState.Animated;
    private Rigidbody[] ragdollRbs;
    private Collider[] ragdollCols;
    private Transform[] ragdollBones;

    private Vector3[] bonePositionSnapshot;
    private Quaternion[] boneRotationSnapshot;
    private float blendTimer;
    private Coroutine activeCoroutine;
    private bool savedFaceUp;

    public ERagdollState CurrentState => currentState;

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

        capsuleRb.constraints = RigidbodyConstraints.FreezeRotation;
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

        if (upperBodyPhysics != null)
            upperBodyPhysics.SetActive(!active);
    }

    public void OnHitImpact(Vector3 impulse, Vector3 hitPoint)
    {
        if (currentState == ERagdollState.Dead) return;

        float magnitude = impulse.magnitude;

        if (magnitude >= ragdollThreshold)
        {
            EnterRagdoll(impulse, hitPoint);
            return;
        }

        if (upperBodyPhysics != null)
            upperBodyPhysics.AddImpulse(impulse);
    }

    private void EnterRagdoll(Vector3 impulse, Vector3 hitPoint)
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        currentState = ERagdollState.Ragdoll;

        Vector3 inheritedVelocity = capsuleRb.linearVelocity;
        SetRagdollActive(true);

        foreach (var rb in ragdollRbs)
            rb.linearVelocity = inheritedVelocity;

        Rigidbody closestRb = GetClosestBoneRb(hitPoint);
        closestRb.AddForce(impulse, ForceMode.Impulse);
        closestRb.AddTorque(
            UnityEngine.Random.insideUnitSphere * impulse.magnitude * 0.15f,
            ForceMode.Impulse);

        OnImpactTriggered?.Invoke(impulse, hitPoint);

        float duration = Mathf.Clamp(
            impulse.magnitude * durationPerImpulse,
            minRagdollDuration,
            maxRagdollDuration);
        activeCoroutine = StartCoroutine(RagdollToBlendCoroutine(duration));
    }

    public void OnDeath()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        currentState = ERagdollState.Dead;
        SetRagdollActive(true);

        OnDeathTriggered?.Invoke();
    }

    private Rigidbody GetClosestBoneRb(Vector3 point)
    {
        Rigidbody closest = ragdollRbs[0];
        float closestSqr = (closest.position - point).sqrMagnitude;

        for (int i = 1; i < ragdollRbs.Length; i++)
        {
            float sqr = (ragdollRbs[i].position - point).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closest = ragdollRbs[i];
                closestSqr = sqr;
            }
        }

        return closest;
    }

    private IEnumerator RagdollToBlendCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (currentState == ERagdollState.Ragdoll)
            StartBlendToAnimation();

        activeCoroutine = null;
    }

    private void StartBlendToAnimation()
    {
        currentState = ERagdollState.BlendToAnim;

        savedFaceUp = Vector3.Dot(ragdollBones[0].up, Vector3.up) > 0;

        for (int i = 0; i < ragdollBones.Length; i++)
        {
            bonePositionSnapshot[i] = ragdollBones[i].position;
            boneRotationSnapshot[i] = ragdollBones[i].rotation;
        }

        Vector3 hipsPos = ragdollBones[0].position;
        float groundY = GetGroundY(hipsPos);

        capsuleRb.position = new Vector3(hipsPos.x, groundY, hipsPos.z);

        Vector3 hipsForward = ragdollBones[0].rotation * Vector3.forward;
        hipsForward.y = 0f;
        if (hipsForward.sqrMagnitude > 0.001f)
            capsuleRb.rotation = Quaternion.LookRotation(hipsForward);

        // 이제 물리 모드 전환
        SetRagdollActive(false);

        // 잔여 속도 제거
        capsuleRb.linearVelocity = Vector3.zero;
        capsuleRb.angularVelocity = Vector3.zero;

        // BlendToAnim 동안 UpperBodyPhysics 비활성화
        if (upperBodyPhysics != null)
            upperBodyPhysics.SetActive(false);

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
        if (currentState != ERagdollState.BlendToAnim) return;

        blendTimer += Time.deltaTime;
        float t = Mathf.Clamp01(blendTimer / blendDuration);

        // Phase 1: 본 블렌드
        if (t < 1f)
        {
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
            return;
        }

        currentState = ERagdollState.Animated;
        if (useGetUpAnimation)
        {
            string getUpClip = savedFaceUp ? "GetUp_Back" : "GetUp_Front";
            animator.CrossFade(getUpClip, 0.2f);
        }
        else
        {
            animator.CrossFade("Locomotion", 0.2f);
        }

        if (upperBodyPhysics != null)
            upperBodyPhysics.SetActive(true);
    }
}
