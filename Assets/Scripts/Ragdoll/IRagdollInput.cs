using System;
using UnityEngine;

public enum ERagdollState { Animated, Ragdoll, BlendToAnim, Dead }

/// <summary>
/// 래그돌 시스템 외부 제어 인터페이스.
/// CharacterController, RopeSystem, PUN2PlayerInput에서 사용.
/// </summary>
public interface IRagdollInput
{
    ERagdollState CurrentState { get; }
    bool IsRagdollActive => ((CurrentState == ERagdollState.Ragdoll) || (CurrentState == ERagdollState.BlendToAnim));

    /// <summary>충돌 임펄스 인가. ragdollThreshold 미만은 상체 물리로 처리.</summary>
    void OnHitImpact(Vector3 impulse, Vector3 hitPoint);

    /// <summary>낙사 처리. Dead 상태로 영구 전환.</summary>
    void OnDeath();

    event Action<Vector3, Vector3> OnImpactTriggered;
    event Action OnDeathTriggered;
}
