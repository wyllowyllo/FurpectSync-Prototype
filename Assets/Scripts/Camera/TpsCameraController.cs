using UnityEngine;

public class TpsCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Orbit")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private Vector2 pitchRange = new Vector2(-20f, 60f);

    [Header("Mouse Sensitivity")]
    [SerializeField] private float horizontalSensitivity = 3f;
    [SerializeField] private float verticalSensitivity = 2f;
    [SerializeField] private bool invertY;

    private float yaw;
    private float pitch = 20f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target == null)
            return;

        Vector3 dir = transform.position - (target.position + targetOffset);
        yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float mouseX = Input.GetAxis("Mouse X") * horizontalSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * verticalSensitivity;

        yaw += mouseX;
        pitch += invertY ? mouseY : -mouseY;
        pitch = Mathf.Clamp(pitch, pitchRange.x, pitchRange.y);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + targetOffset;
        transform.position = pivot - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
