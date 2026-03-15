using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// CinemachineCamera에 부착하면 TPS 마우스 카메라로 자동 전환됩니다.
/// CinemachineFollow가 있으면 제거하고 CinemachineOrbitalFollow를 추가합니다.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineTpsInput : MonoBehaviour
{
    [Header("Mouse Sensitivity")]
    [SerializeField] private float horizontalSensitivity = 3f;
    [SerializeField] private float verticalSensitivity = 2f;
    [SerializeField] private bool invertY;

    [Header("Orbit")]
    [SerializeField] private float orbitRadius = 4f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector2 pitchRange = new Vector2(-20f, 60f);

    private CinemachineOrbitalFollow orbitalFollow;

    private void Awake()
    {
        SetupOrbitalFollow();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetupOrbitalFollow()
    {
        orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        if (orbitalFollow != null)
            return;

        var oldFollow = GetComponent<CinemachineFollow>();
        if (oldFollow != null)
            Destroy(oldFollow);

        orbitalFollow = gameObject.AddComponent<CinemachineOrbitalFollow>();
        orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbitalFollow.Radius = orbitRadius;
        orbitalFollow.TargetOffset = targetOffset;
        orbitalFollow.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.AxisCenter;

        orbitalFollow.HorizontalAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false };
        orbitalFollow.VerticalAxis = new InputAxis
        {
            Value = 20f,
            Range = pitchRange,
            Wrap = false,
            Center = 20f,
            Recentering = new InputAxis.RecenteringSettings { Enabled = false }
        };
    }

    private void Update()
    {
        if (orbitalFollow == null) return;

        float mouseX = Input.GetAxis("Mouse X") * horizontalSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * verticalSensitivity;

        if (invertY) mouseY = -mouseY;

        orbitalFollow.HorizontalAxis.Value += mouseX;
        orbitalFollow.VerticalAxis.Value -= mouseY;
    }
}
