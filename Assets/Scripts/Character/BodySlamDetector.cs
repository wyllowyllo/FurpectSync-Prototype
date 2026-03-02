using System.Collections.Generic;
using UnityEngine;

public class BodySlamDetector : MonoBehaviour
{
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float detectionRadius = 0.6f;
    [SerializeField] private float detectionInterval = 0.1f;

    private bool isTeamA;
    private bool isOwner;
    private float detectionTimer;

    private ConvergenceController cachedConvergence;
    private DivideController cachedDivide;

    private readonly List<Transform> enemies = new();
    private float findTimer;
    private const float FIND_INTERVAL = 1f;

    public void Initialize(bool teamA, bool owner)
    {
        isTeamA = teamA;
        isOwner = owner;
        cachedConvergence = GetComponent<ConvergenceController>();
        cachedDivide = GetComponent<DivideController>();
    }

    void FixedUpdate()
    {
        if (!isOwner) return;

        RefreshEnemies();

        detectionTimer += Time.fixedDeltaTime;
        if (detectionTimer < detectionInterval) return;
        detectionTimer = 0f;

        float sqrRadius = detectionRadius * detectionRadius;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            Vector3 diff = transform.position - enemies[i].position;
            diff.y = 0f;
            if (diff.sqrMagnitude > sqrRadius) continue;

            ApplyKnockbackToSelf(diff.normalized * knockbackForce);
        }
    }

    private void RefreshEnemies()
    {
        if (enemies.Count > 0)
        {
            findTimer += Time.fixedDeltaTime;
            if (findTimer < FIND_INTERVAL) return;
        }
        findTimer = 0f;

        enemies.Clear();

        var convergences = FindObjectsByType<ConvergenceController>(FindObjectsSortMode.None);
        foreach (var cc in convergences)
        {
            if (cc.IsTeamA != isTeamA)
                enemies.Add(cc.transform);
        }

        var divides = FindObjectsByType<DivideController>(FindObjectsSortMode.None);
        foreach (var dc in divides)
        {
            if (dc.IsTeamA != isTeamA)
                enemies.Add(dc.transform);
        }
    }

    private void ApplyKnockbackToSelf(Vector3 force)
    {
        if (cachedConvergence != null)
        {
            cachedConvergence.ApplyKnockback(force);
            return;
        }

        if (cachedDivide != null)
            cachedDivide.ApplyKnockback(force);
    }
}
