using UnityEngine;

/// <summary>
/// Spine 본에 수동 스프링 시뮬레이션으로 상체 물리 반응을 적용.
/// Animator와 충돌 없이 LateUpdate에서 회전 오프셋만 덧씌움.
/// </summary>
public class UpperBodyPhysics : MonoBehaviour
{
    [SerializeField] private Transform spineTransform;
    [SerializeField] private float spring = 80f;
    [SerializeField] private float damper = 8f;
    [SerializeField] private float impulseMultiplier = 3f;

    private Rigidbody spineRb;
    private Vector3 angularVelocity;
    private Quaternion rotationOffset = Quaternion.identity;
    private bool isActive = true;

    private void Awake()
    {
        spineRb = spineTransform.GetComponent<Rigidbody>();
    }

    private void LateUpdate()
    {
        if (!isActive) return;

        rotationOffset.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        if (axis.sqrMagnitude < 0.001f)
        {
            axis = Vector3.up;
            angle = 0f;
        }

        Vector3 springTorque = -(axis.normalized * (angle * Mathf.Deg2Rad)) * spring;
        Vector3 dampTorque = -angularVelocity * damper;
        angularVelocity += (springTorque + dampTorque) * Time.deltaTime;

        float speed = angularVelocity.magnitude;
        if (speed > 0.001f)
        {
            Quaternion delta = Quaternion.AngleAxis(
                speed * Mathf.Rad2Deg * Time.deltaTime,
                angularVelocity / speed);
            rotationOffset = delta * rotationOffset;
        }

        spineTransform.localRotation *= rotationOffset;
    }

    public void AddImpulse(Vector3 worldImpulse)
    {
        if (!isActive) return;

        Vector3 localDir = spineTransform.InverseTransformDirection(worldImpulse);
        angularVelocity += new Vector3(-localDir.z, 0f, localDir.x) * impulseMultiplier;
    }

    public void SetActive(bool active)
    {
        isActive = active;

        if (!active)
        {
            if (spineRb != null)
                spineRb.isKinematic = true;

            angularVelocity = Vector3.zero;
            rotationOffset = Quaternion.identity;
        }
    }
}
