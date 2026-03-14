using System;
using UnityEngine;

public enum RagdollState { Animated, Ragdoll, BlendToAnim, Dead }

/// <summary>
/// 래그돌 시스템 외부 제어 인터페이스.
/// CharacterController, RopeSystem, PUN2PlayerInput에서 사용.
/// </summary>
public interface IRagdollInput
{
    RagdollState CurrentState { get; }
    bool IsRagdollActive => CurrentState is RagdollState.Ragdoll
                                            or RagdollState.BlendToAnim;

    /// <summary>충돌 임펄스 인가. 임계값 미만은 자동 무시.</summary>
    void OnHitImpact(Vector3 impulse, Vector3 hitPoint);

    /// <summary>낙사 처리. Dead 상태로 영구 전환.</summary>
    void OnDeath();

    event Action<Vector3, Vector3> OnImpactTriggered;
    event Action OnDeathTriggered;
}
