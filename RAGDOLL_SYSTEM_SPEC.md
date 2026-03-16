# Animation + Passive Ragdoll System 명세서

## 1. 시스템 개요

Animator 기반 애니메이션과 물리 기반 래그돌을 **상호 배타적으로 전환**하는 Passive Ragdoll 시스템이다.
"Passive"란 래그돌 상태에서 캐릭터가 자체 구동력 없이 **외부 힘에만 반응**한다는 의미이다.

추가로, 래그돌 임계값 미만의 약한 충격에는 **상체 스프링 물리**(UpperBodyPhysics)로 흔들림만 연출하여, 작은 충돌에도 자연스러운 물리 반응을 보여준다.

```
[약한 충격] → UpperBodyPhysics (상체 흔들림)
[강한 충격] → Full Ragdoll → Blend → Animation 복귀
[사망]      → Full Ragdoll (영구)
```

---

## 2. 상태 머신

```
ERagdollState { Animated, Ragdoll, BlendToAnim, Dead }
```

```
                 impulse ≥ threshold
  ┌──────────┐ ─────────────────────► ┌──────────┐
  │ Animated │                        │ Ragdoll  │
  └──────────┘ ◄──── blend 완료 ───── └──────────┘
       │              ▲                     │
       │              │      duration 경과  │
       │              │                     ▼
       │              │              ┌──────────────┐
       │              └───────────── │ BlendToAnim  │
       │                             └──────────────┘
       │
       │  OnDeath()
       ▼
  ┌──────────┐
  │   Dead   │  (영구 래그돌, 복귀 없음)
  └──────────┘
```

| 상태 | Animator | Ragdoll Rigidbody | Capsule Collider | UpperBodyPhysics |
|------|----------|-------------------|------------------|------------------|
| **Animated** | enabled | isKinematic = true | enabled | active |
| **Ragdoll** | disabled | isKinematic = false | disabled | inactive |
| **BlendToAnim** | enabled | isKinematic = true | enabled | inactive |
| **Dead** | disabled | isKinematic = false | disabled | inactive |

---

## 3. 듀얼 콜라이더 구조

캐릭터에는 **두 세트의 물리 오브젝트**가 공존한다.

### 3.1 Capsule (이동용)
- 단일 `Rigidbody` + 단일 `Collider`
- Animated 상태에서 `TestCharacterController`가 `linearVelocity`를 직접 설정하여 이동
- 회전은 FreezeRotation 제약으로 물리 회전 방지, 스크립트에서 `Quaternion.Slerp`로 제어

### 3.2 Ragdoll Bones (래그돌용)
- `hipsRoot` 하위의 모든 본에 부착된 다수의 `Rigidbody` + `Collider`
- Animated 상태에서는 `isKinematic = true`, 콜라이더 `disabled` → 물리 연산 없음
- Ragdoll 상태에서 활성화되어 물리 시뮬레이션 수행

**핵심 원칙:** 두 세트는 항상 반대 상태로 토글된다. 동시에 활성화되지 않는다.

---

## 4. 충격 처리 흐름 (OnHitImpact)

```csharp
void OnHitImpact(Vector3 impulse, Vector3 hitPoint)
```

### 4.1 약한 충격 (magnitude < ragdollThreshold)

```
OnHitImpact → UpperBodyPhysics.AddImpulse(impulse)
```

- Animator는 계속 재생, 캐릭터는 넘어지지 않음
- Spine 본에 회전 오프셋만 적용하여 **상체 흔들림** 연출
- 자세한 원리는 [Section 7](#7-upperbodyphysics-상체-스프링-물리) 참조

### 4.2 강한 충격 (magnitude ≥ ragdollThreshold)

```
OnHitImpact → EnterRagdoll(impulse, hitPoint)
```

1. **상태 전환:** `SetRagdollActive(true)` → Animator 비활성화, 본 물리 활성화
2. **관성 전달:** Capsule의 `linearVelocity`를 모든 래그돌 본에 복사 → 이동 중 넘어져도 관성 유지
3. **충격 적용:** `hitPoint`에 가장 가까운 본을 찾아 해당 Rigidbody에 `AddForce(impulse, Impulse)` + 랜덤 토크
4. **복귀 타이머:** `impulse.magnitude * durationPerImpulse`로 래그돌 지속 시간 계산 (min~max 클램프)
5. **코루틴 시작:** 지속 시간 후 `StartBlendToAnimation()` 호출

---

## 5. 래그돌 → 애니메이션 복귀 (Blend)

래그돌에서 바로 애니메이션으로 전환하면 **순간 이동(팝핑)**이 발생한다.
이를 방지하기 위해 **스냅샷 블렌딩** 기법을 사용한다.

### 5.1 StartBlendToAnimation

```
상태: Ragdoll → BlendToAnim
```

1. **스냅샷 저장:** 모든 래그돌 본의 현재 position/rotation을 배열에 기록
2. **Capsule 위치 복원:**
   - Hips 본의 XZ 좌표를 가져옴 (래그돌이 쓰러진 위치)
   - Raycast로 지면 Y좌표를 구함
   - Capsule을 `(hips.x, groundY, hips.z)`로 이동 → 넘어진 자리에서 일어남
3. **Capsule 회전 복원:** Hips의 forward 벡터에서 수평 방향 추출 → `LookRotation`
4. **물리 모드 전환:** `SetRagdollActive(false)` → Animator 활성화, 본 Kinematic 전환
5. **잔여 속도 제거:** Capsule의 linear/angular velocity를 0으로 초기화

### 5.2 LateUpdate 블렌딩

```
blendTimer: 0 → blendDuration (기본 0.3초)
t = blendTimer / blendDuration (0→1)
```

매 프레임, 모든 본에 대해:
```
position = Lerp(스냅샷 position, Animator position, t)
rotation = Slerp(스냅샷 rotation, Animator rotation, t)
```

- `t = 0`: 래그돌 자세 그대로
- `t = 1`: Animator가 결정한 자세 완전 적용
- 중간값: 래그돌 자세에서 애니메이션 자세로 부드럽게 전환

**LateUpdate를 사용하는 이유:** Animator가 Update에서 본 트랜스폼을 갱신한 뒤, LateUpdate에서 그 결과 위에 스냅샷 블렌딩을 덧씌워야 하기 때문이다.

블렌드 완료 시:
- `currentState = Animated`
- `animator.CrossFade("Locomotion", 0.2f)` → 자연스러운 애니메이션 전환
- `UpperBodyPhysics` 재활성화

---

## 6. TestCharacterController 연동

`TestCharacterController`는 래그돌 상태에 따라 입력을 차단한다.

### 6.1 Update 흐름
```
if (state != Animated) return;  // 래그돌 중에는 입력 무시
→ 카메라 상대 입력 계산
→ 가감속 보간 (Lerp)
→ 속도 방향으로 회전
→ Animator Speed 파라미터 갱신
```

### 6.2 래그돌 복귀 감지
```
if (이전 상태 != Animated && 현재 상태 == Animated)
    currentVelocity = Vector3.zero;
```
래그돌에서 복귀할 때 이전 이동 속도를 초기화하여, 복귀 직후 캐릭터가 튀어나가는 것을 방지한다.

### 6.3 물리 이동 (FixedUpdate)
```
capsuleRb.linearVelocity = (currentVelocity.x, 기존 y, currentVelocity.z)
```
Y축 속도는 보존하여 중력/점프가 정상 동작하고, XZ만 입력 속도로 덮어쓴다.

---

## 7. UpperBodyPhysics (상체 스프링 물리)

래그돌 임계값 미만의 약한 충격에 대응하는 **소프트 물리 반응** 시스템이다.

### 7.1 원리: 수동 스프링-댐퍼 시뮬레이션

Spine 본의 `localRotation`에 회전 오프셋을 매 프레임 곱하여, Animator 위에 물리적 흔들림을 덧씌운다.

```
[Animator가 결정한 Spine 회전] × [rotationOffset] = 최종 Spine 회전
```

### 7.2 스프링 역학

매 LateUpdate에서:

1. **현재 오프셋을 각도/축으로 분해:**
   `rotationOffset → (angle, axis)`

2. **복원력 (Spring Torque):** 오프셋 각도에 비례하는 토크로 원래 자세로 되돌림
   ```
   springTorque = -(axis × angle_rad) × spring
   ```

3. **감쇠력 (Damp Torque):** 각속도에 비례하는 반대 토크로 진동 억제
   ```
   dampTorque = -angularVelocity × damper
   ```

4. **각속도 갱신:**
   ```
   angularVelocity += (springTorque + dampTorque) × dt
   ```

5. **오프셋 회전 적용:**
   ```
   rotationOffset = Quaternion.AngleAxis(speed × dt) × rotationOffset
   spineTransform.localRotation *= rotationOffset
   ```

### 7.3 충격 입력 (AddImpulse)

월드 방향 임펄스를 Spine 로컬 좌표로 변환하여 각속도에 더한다.
```
localDir = InverseTransformDirection(worldImpulse)
angularVelocity += (-localDir.z, 0, localDir.x) × multiplier
```
- 정면 충격(`+z`) → X축 음의 회전 (뒤로 젖혀짐)
- 측면 충격(`+x`) → Z축 양의 회전 (옆으로 기울어짐)
- Y축 회전은 0 → 좌우 비틀림 방지

---

## 8. 파일 구조

```
Assets/Scripts/
├── Ragdoll/
│   ├── IRagdollInput.cs        # 외부 제어 인터페이스 + ERagdollState 열거형
│   ├── RagdollController.cs    # 상태 머신, 전환 로직, 블렌딩
│   ├── UpperBodyPhysics.cs     # 상체 스프링 물리 (약한 충격 반응)
│   └── RagdollTestDriver.cs    # 독립 씬 테스트 드라이버 (T/R/K 키)
└── Character/
    └── TestCharacterController.cs  # 이동/회전, 래그돌 상태 연동
```

---

## 9. 설계 요약

| 설계 결정 | 이유 |
|-----------|------|
| Passive Ragdoll (자체 구동 없음) | 구현 단순성, 넘어진 캐릭터는 외부 힘에만 반응하면 충분 |
| 듀얼 콜라이더 토글 | 애니메이션/래그돌 충돌 방지, 깔끔한 상태 분리 |
| 스냅샷 블렌딩 (LateUpdate) | 래그돌→애니메이션 전환 시 팝핑 방지 |
| 관성 전달 (velocity 복사) | 이동 중 넘어질 때 자연스러운 물리 연속성 |
| Hips 기반 위치 복원 + Raycast | 넘어진 위치에서 일어나며, 지면 위에 정확히 착지 |
| UpperBodyPhysics 분리 | 약한 충격에도 물리 반응, 래그돌 전환 없이 Animator와 공존 |
| 임펄스 비례 지속 시간 | 강한 충격일수록 오래 넘어져 있어 직관적 |
| IRagdollInput 인터페이스 | 외부 시스템(네트워크, 로프 등)이 구체 클래스에 의존하지 않음 (DIP) |
