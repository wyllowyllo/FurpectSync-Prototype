# 래그돌 부분 도입 구현 계획 (모델 교체 포함)

## Context

**목적:** 캡슐 메시 기반 프리팹을 리깅된 캐릭터로 교체하고, 뼈대 기반 부분 래그돌 구현.

**현재 상태:**
- 캐릭터: Unity 기본 Capsule (뼈대·Animator 없음)
- 물리: CharacterController만 사용, Rigidbody 없음
- 충돌 감지: CharacterController.Move()로 지형 충돌만 처리. 캐릭터 간 충돌 이벤트 없음

**목표 상태 (3가지 래그돌 상황):**

| 상황 | 방식 | 기술 |
|------|------|------|
| 평상시 | Secondary Motion | 팔·머리 뼈대만 `isKinematic=false`, Animator가 몸통·다리 제어 |
| 몸통박치기 피격 | 단기 래그돌 (0.5s) | 전체 뼈대 `isKinematic=false`, Animator off, 외력 인가 후 복귀 |
| 낙사 | 풀 래그돌 | 단기 래그돌과 동일하나 복귀 없이 리스폰 처리 |

---

## Step 1 — 래그돌 모델 제작 가이드

### 최소 뼈대 요구 (Unity Ragdoll Wizard 기준)

Unity Ragdoll Wizard는 아래 13개 슬롯을 요구한다.

| Wizard 슬롯 | Mixamo 본 이름 |
|-------------|----------------|
| Pelvis | Hips |
| Left / Right Hips | LeftUpLeg / RightUpLeg |
| Left / Right Knee | LeftLeg / RightLeg |
| Left / Right Foot | LeftFoot / RightFoot |
| Left / Right Shoulder | LeftArm / RightArm |
| Left / Right Elbow | LeftForeArm / RightForeArm |
| Middle Spine | Spine |
| Head | Head |

### 추천 경로: Mixamo (무료, 가장 빠름)

1. **mixamo.com 접속 → 캐릭터 선택** (X Bot, Y Bot 또는 기타)
   기획서 레퍼런스의 Party Game Character Pack을 쓸 경우 이 단계 생략.

2. **T-Pose FBX 다운로드**
   - 애니메이션: "Without Skin" X → 기본 T-Pose만
   - Format: `FBX for Unity`
   - Skin: `With Skin`
   - Frame rate: 30

3. **Unity Import 설정**
   FBX를 `Assets/Models/Character/` 경로에 배치 후:
   ```
   Inspector → Model 탭
     - Scale Factor: 1
   Inspector → Rig 탭
     - Animation Type: Humanoid
     - Avatar Definition: Create From This Model
     - Apply
   ```

4. **Unity Ragdoll Wizard 실행**
   ```
   Hierarchy에서 FBX 인스턴스 선택
   → 상단 메뉴: GameObject > 3D Object > Ragdoll
   → Wizard에서 각 슬롯에 Hierarchy 뼈대 본 드래그
   → Create 버튼 클릭
   ```
   Wizard 실행 후 각 뼈대에 `Rigidbody`, `CapsuleCollider`(또는 BoxCollider), `CharacterJoint`가 자동 추가됨.

5. **Ragdoll 기본값 조정**
   생성 직후 모든 `Rigidbody`의 `isKinematic = true`로 변경 (Secondary Motion 준비 상태).
   팔·머리 뼈대(LeftArm, RightArm, LeftForeArm, RightForeArm, Head)만 `isKinematic = false`로 유지 → Secondary Motion 기본 동작.

---

## Step 2 — 프리팹 재구성

### 목표 계층 구조 (두 프리팹 공통 패턴)

```
ConvergenceCharacter (Root GameObject)
├── CharacterController          ← 지형 충돌·이동 (위치 유지)
├── PhotonView                   ← 네트워크 동기화
├── ConvergenceController        ← 벡터 합산 이동 로직
├── RagdollController (신규)     ← 래그돌 상태 관리 + 충돌 감지
├── CapsuleCollider (IsTrigger)  ← 캐릭터 간 충돌 감지 전용 (신규)
│     Radius: 0.6 (CC보다 약간 크게)
│     IsTrigger: true
│     Center: (0, 1, 0)
└── Model (자식 GameObject)      ← 리깅된 시각 모델
    └── Armature
        └── Hips                 ← Rigidbody (isKinematic: true, 평상시)
            ├── Spine → Chest
            │   ├── LeftArm → LeftForeArm   ← Rigidbody (isKinematic: false, Secondary Motion)
            │   ├── RightArm → RightForeArm ← Rigidbody (isKinematic: false, Secondary Motion)
            │   └── Neck → Head             ← Rigidbody (isKinematic: false, Secondary Motion)
            ├── LeftUpLeg → LeftLeg → LeftFoot  ← Rigidbody (isKinematic: true)
            └── RightUpLeg → RightLeg → RightFoot ← Rigidbody (isKinematic: true)
```

**DivideCharacter.prefab도 동일 구조** (DivideController, LineRenderer 유지).

### 제거할 기존 컴포넌트
- Root의 `MeshFilter`, `MeshRenderer` (캡슐 비주얼)
- 자식 `Capsule` GameObject 전체 (CapsuleCollider + Mesh)

---

## Step 3 — 생성할 스크립트

### `Assets/Scripts/Character/RagdollController.cs`

**역할:** 래그돌 상태 머신 + 캐릭터 간 충돌 감지.

```
RagdollState enum
  Animated         // 전체 kinematic, Animator on  (현재 미사용, 추후 확장용)
  SecondaryMotion  // 몸통·다리 kinematic, 팔·머리 non-kinematic, Animator on
  HitRagdoll       // 전체 non-kinematic, Animator off, stunTimer 카운트다운
  FullRagdoll      // 전체 non-kinematic, Animator off, 영구 (낙사)

핵심 필드:
  Rigidbody[] allBodies           // GetComponentsInChildren<Rigidbody>()
  Rigidbody[] secondaryBodies     // 팔·머리 본 Rigidbody만 필터링
  Rigidbody hipsBone              // 외력 인가 기준점
  Animator animator
  CharacterController cc
  float stunDuration = 0.5f
  float stunTimer
  float minImpactSpeed = 2f       // 이 이상 상대속도여야 피격으로 인정
  bool IsStunned                  // HitRagdoll || FullRagdoll

SetState(RagdollState):
  SecondaryMotion:
    allBodies → isKinematic = true
    secondaryBodies → isKinematic = false
    animator.enabled = true
    cc.enabled = true

  HitRagdoll:
    allBodies → isKinematic = false
    animator.enabled = false
    cc.enabled = false
    stunTimer = stunDuration

  FullRagdoll:
    allBodies → isKinematic = false
    animator.enabled = false
    cc.enabled = false

Update():
  if HitRagdoll:
    stunTimer -= dt
    if stunTimer <= 0:
      // Root를 Hips 위치로 이동 후 복귀
      transform.position = hipsBone.position (y값 지면 보정)
      SetState(SecondaryMotion)

OnTriggerEnter(Collider other):
  if !photonView.IsMine: return
  if !other.CompareTag("Player"): return
  // 상대 팀 여부 확인 (RagdollController의 IsTeamA vs other)
  // 상대 CC.velocity 기준 충돌 세기 계산
  if impactSpeed < minImpactSpeed: return
  Vector3 hitDir = (transform.position - other.transform.position).normalized
  photonView.RPC("RPCApplyHit", RpcTarget.All, hitDir.x, hitDir.z, hitForce)

[PunRPC] RPCApplyHit(float dx, float dz, float force):
  SetState(HitRagdoll)
  hipsBone.AddForce(new Vector3(dx, 0.3f, dz) * force, ForceMode.Impulse)

[PunRPC] RPCApplyFall():
  SetState(FullRagdoll)
  // 낙사 완료 → NetworkManager 또는 상위에서 리스폰 처리 트리거
```

**Secondary Motion 대상 본 이름 (Inspector에서 설정):**
```
secondaryBoneNames: ["LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "Head"]
```
Awake에서 `allBodies`를 이름 필터링하여 `secondaryBodies` 구성.

---

## Step 4 — 수정할 스크립트

### `Assets/Scripts/Character/ConvergenceController.cs`

**추가:**
```csharp
private RagdollController ragdollController;

void Awake() {
    // 기존 코드 유지...
    ragdollController = GetComponent<RagdollController>();
}
```

**SendInputToMaster() 앞에 가드:**
```csharp
private void SendInputToMaster() {
    if (ragdollController != null && ragdollController.IsStunned) return;
    // 기존 로직...
}
```

**UpdateBuffer() 앞에 가드:**
```csharp
private void UpdateBuffer() {
    if (ragdollController != null && ragdollController.IsStunned) return;
    // 기존 로직...
}
```

### `Assets/Scripts/Character/DivideController.cs`

동일 패턴:
```csharp
private RagdollController ragdollController;
// Awake에서 GetComponent

// ProcessLocalInput() 앞에 가드
if (ragdollController != null && ragdollController.IsStunned) return;
```

---

## Step 5 — 낙사 존 설정 (씬)

TestScene에 `FallZone` 트리거 추가:
```
GameObject: FallZone
  BoxCollider (IsTrigger: true)
  Tag: "FallZone"
  배치: 맵 외곽 낙사 구역
```

`RagdollController.OnTriggerEnter`에서 `FallZone` 태그 감지 → `RPCApplyFall()` 호출.

---

## 네트워크 전략

```
[충돌 감지]
  소유자(photonView.IsMine)만 OnTriggerEnter 처리
       ↓
  photonView.RPC("RPCApplyHit", All, dir, force)
       ↓
  [전 클라이언트] SetState(HitRagdoll) + 외력 인가
       ↓
  [소유자] CharacterController 비활성, Rigidbody로 이동
  [비소유자] 기존 OnPhotonSerializeView가 Root 위치 동기화
```

- 뼈대별 위치 동기화 없음 → 네트워크 비용 추가 없음
- Secondary Motion은 각 클라이언트 로컬 물리 연산 → 동기화 불필요

---

## 구현 순서

1. **모델 준비** — Mixamo에서 FBX 다운로드, Unity Import, Ragdoll Wizard 실행
2. **프리팹 재구성** — 캡슐 제거, Model 자식 배치, TriggerCollider 추가
3. **RagdollController.cs 생성** — SecondaryMotion 상태 진입 확인
4. **ConvergenceController / DivideController 수정** — IsStunned 가드 추가
5. **HitRagdoll 검증** — 에디터 2클라이언트에서 충돌 → 스턴 확인
6. **FallZone 배치** — 씬에 트리거 존 추가, FullRagdoll 확인

---

## 검증 체크리스트

| 항목 | 확인 방법 |
|------|----------|
| Secondary Motion | 이동 중 팔·머리가 물리적으로 흔들리는지 확인 |
| 스턴 입력 차단 | 피격 0.5초 동안 키 입력이 이동에 반영 안 되는지 |
| 넉백 방향 | 충돌 법선 방향으로 날아가는지 |
| Root 복귀 | 0.5초 후 CharacterController 위치로 Root 정렬되는지 |
| 네트워크 동기화 | 한쪽에서 피격 시 반대 클라이언트도 스턴 상태 일치하는지 |
| 낙사 | FallZone 진입 시 FullRagdoll + 리스폰 트리거 동작하는지 |
