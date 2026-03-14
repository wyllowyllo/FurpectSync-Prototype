using UnityEngine;

/// <summary>
/// 독립 씬 테스트용 드라이버.
/// R키: 랜덤 임펄스, K키: 사망 처리.
/// </summary>
public class RagdollTestDriver : MonoBehaviour
{
    [SerializeField] private FallGuysRagdoll ragdoll;
    [SerializeField] private float testImpulseForce = 15f;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Vector3 randomDir = Random.insideUnitSphere.normalized;
            randomDir.y = Mathf.Abs(randomDir.y);
            Vector3 impulse = randomDir * testImpulseForce;
            Vector3 hitPoint = ragdoll.transform.position + Vector3.up * 0.5f;
            ragdoll.OnHitImpact(impulse, hitPoint);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            ragdoll.OnDeath();
        }
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 400, 40),
            $"State: {ragdoll.CurrentState}", style);
        GUI.Label(new Rect(10, 50, 400, 30),
            "[R] Hit Impulse  [K] Death", GUI.skin.label);
    }
}
