using UnityEngine;

[RequireComponent(typeof(DivideController))]
public class RubberBand : MonoBehaviour
{
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private float pullStrength = 3f;

    private DivideController owner;
    private DivideController partner;
    private LineRenderer lineRenderer;
    private float findTimer;
    private const float FIND_INTERVAL = 0.5f; // Throttle FindPartner to 2Hz

    void Awake()
    {
        owner = GetComponent<DivideController>();
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.magenta;
        lineRenderer.endColor = Color.magenta;
        lineRenderer.enabled = false;
    }

    void Update()
    {
        if (partner == null)
        {
            findTimer += Time.deltaTime;
            if (findTimer >= FIND_INTERVAL)
            {
                findTimer = 0f;
                FindPartner();
            }
            return;
        }

        UpdateLineRenderer();
        ApplyPullForce();
    }

    private void FindPartner()
    {
        var controllers = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
        foreach (var dc in controllers)
        {
            if (dc == owner) continue;
            if (dc.IsTeamA != owner.IsTeamA) continue;

            partner = dc;
            lineRenderer.enabled = true;
            return;
        }
    }

    private void UpdateLineRenderer()
    {
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, partner.transform.position);
    }

    private void ApplyPullForce()
    {
        if (!owner.photonView.IsMine) return;

        float distance = Vector3.Distance(transform.position, partner.transform.position);
        if (distance <= maxDistance) return;

        Vector3 pullDirection = (partner.transform.position - transform.position).normalized;
        float pullMagnitude = (distance - maxDistance) * pullStrength;
        owner.ApplyRubberBandForce(pullDirection * pullMagnitude);
    }
}
