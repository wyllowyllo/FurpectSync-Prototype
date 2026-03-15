using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 독립 씬 테스트용 드라이버.
/// T키: Light Hit 임펄스, R키: Ragdoll 임펄스, K키: 사망 처리.
/// </summary>
public class RagdollTestDriver : MonoBehaviour
{
    [FormerlySerializedAs("ragdoll")] [SerializeField] private RagdollController ragdollController;
    [SerializeField] private float testImpulseForce = 15f;
    [FormerlySerializedAs("testStumbleForce")] [SerializeField] private float testLightHitForce = 5f;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3 randomDir = Random.insideUnitSphere.normalized;
            randomDir.y = Mathf.Abs(randomDir.y);
            Vector3 impulse = randomDir * testLightHitForce;
            Vector3 hitPoint = ragdollController.transform.position + Vector3.up * 0.5f;
            ragdollController.OnHitImpact(impulse, hitPoint);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Vector3 randomDir = Random.insideUnitSphere.normalized;
            randomDir.y = Mathf.Abs(randomDir.y);
            Vector3 impulse = randomDir * testImpulseForce;
            Vector3 hitPoint = ragdollController.transform.position + Vector3.up * 0.5f;
            ragdollController.OnHitImpact(impulse, hitPoint);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            ragdollController.OnDeath();
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
            $"State: {ragdollController.CurrentState}", style);
        GUI.Label(new Rect(10, 50, 400, 30),
            "[T] Light Hit  [R] Ragdoll  [K] Death", GUI.skin.label);
    }
}
