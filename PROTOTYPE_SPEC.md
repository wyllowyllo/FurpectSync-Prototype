# Furfect Sync — Multiplayer Prototype Development Specification

> **이 문서는 Claude Code가 프로토타입을 개발하기 위한 전체 맥락과 구현 명세입니다.**
> **기획 의도, 기술 결정의 근거, 구현 우선순위가 모두 포함되어 있습니다.**
> **코드를 작성하기 전에 반드시 이 문서 전체를 읽어주세요.**

---

## 1. 프로젝트 개요

### 한 줄 요약
"두 사람이 하나의 몸. 호흡이 곧 속도인 동물 난장판 커플 레이싱"

### 프로토타입 목적
이 프로토타입은 **단 하나의 질문에 답하기 위해 존재합니다:**

> "2인이 하나의 캐릭터를 조작하는 벡터 합산 이동이 PUN2 환경에서 체감상 자연스럽게 동작하는가?"

- 판정이 정확히 일치할 필요는 없음
- **체감상 판정 불일치가 드러나는지**가 핵심 검증 포인트
- UI/UX, 사운드, 이펙트, 라운드 시스템 등은 **전부 제외**

### 기술 스택
| 항목 | 선택 |
|------|------|
| 엔진 | Unity (2021.3 LTS 이상) |
| 네트워크 | Photon PUN 2 (FREE) |
| 개발 인원 | 1인 |
| 개발 기간 | 2~3일 (16~24시간) |

---

## 2. 게임 메커니즘 명세

이 게임에는 두 가지 이동 모드가 있으며, 플레이 중 자유롭게 전환 가능합니다.

### 2-1. 컨버전스 모드 (Convergence Mode) — P0 최우선 구현

**개념:** 2인이 하나의 캐릭터를 함께 조작합니다. 두 플레이어의 이동 입력이 벡터로 합산되어 캐릭터가 움직입니다.

**동작 규칙:**

| 두 플레이어 입력 | 결과 | 게임플레이 의미 |
|-----------------|------|---------------|
| 같은 방향 (예: 둘 다 W) | 속도 2배 | 호흡이 맞으면 보상 |
| 반대 방향 (예: W + S) | 정지 | 소통 실패 시 페널티 |
| 다른 방향 (예: W + D) | 사선 이동 | 의도치 않은 경로 이탈 |
| 한 명만 입력 | 절반 속도로 해당 방향 이동 | 파트너가 가만히 있는 상황 |

**핵심 구현 사항:**
- 각 클라이언트는 자신의 WASD 입력을 Vector2로 정규화하여 마스터 클라이언트에 RPC로 전송
- 마스터 클라이언트가 입력 버퍼(100ms)로 양쪽 입력을 수집 → 합산 → 캐릭터에 이동 적용
- 결과 위치를 OnPhotonSerializeView로 전체 브로드캐스트
- 비마스터 클라이언트는 수신된 위치를 Lerp로 보간하여 부드러운 이동 표현

**이동 느낌:** Fall Guys / PHOGS! 스타일의 물렁물렁한 느낌. 즉각적 반응이 아니라 부드러운 가속/감속 커브(ease-in/ease-out)를 적용합니다. 이것은 의도적인 게임 디자인이며, 동시에 네트워크 지연을 자연스럽게 마스킹하는 역할을 합니다.

### 2-2. 디바이드 모드 (Divide Mode) — P1 구현

**개념:** 하나의 캐릭터가 복제되어 두 개가 됩니다. 각 플레이어가 한 캐릭터씩 개별 조작하지만, 두 캐릭터는 고무줄 같은 줄로 연결되어 있습니다.

**동작 규칙:**
- 각자 독립적으로 WASD 조작 (일반적인 멀티플레이 캐릭터 이동)
- 두 캐릭터 사이의 거리가 일정 범위를 초과하면 고무줄이 당기는 힘 적용
- 컨버전스 모드 대비 이동 속도가 느림 (0.6~0.7배)
- 이 모드에서 새총(파트너를 투사체처럼 발사) 사용 가능

**고무줄 연결:**
- LineRenderer로 두 캐릭터 사이를 시각적으로 연결
- 물리적 당김: 두 캐릭터 간 거리가 maxDistance를 초과하면 서로를 향해 force 적용
- 고무줄 시각/물리는 각 클라이언트가 양쪽 위치를 참조해 로컬에서 처리 (동기화 불필요)

### 2-3. 모드 전환 (Convergence ↔ Divide)

- 어느 플레이어든 전환 키(예: Space)를 누르면 전환 요청
- **모드 전환은 반드시 마스터 클라이언트 권한으로 처리** (양쪽 상태 불일치 방지)
- 전환 시 0.3~0.5초 전환 연출 시간을 둠 (상태 수렴 시간 확보)
  - 프로토타입에서는 단순 딜레이(코루틴 WaitForSeconds)로 충분
  - 연출 이펙트는 필요 없음

**전환 처리:**
- 컨버전스 → 디바이드: 합체 캐릭터 Destroy → 각 플레이어 위치에 개별 캐릭터 Instantiate
- 디바이드 → 컨버전스: 두 캐릭터 Destroy → 중간점에 합체 캐릭터 Instantiate

### 2-4. 방해 액션 — P1~P2 구현

**몸통 박치기 (P1):**
- 팀 간 CharacterController/Rigidbody 충돌 시 상대를 밀어냄
- 피격자 측에서 OnCollisionEnter 감지 → AddForce 적용 (Owner-authoritative)
- 넉넉한 히트박스 사용 (캐주얼 파티게임이므로 정밀 판정 불필요)

**새총 투사체 발사 (P2, 선택):**
- 디바이드 모드에서만 사용 가능
- 파트너를 새총의 총알처럼 발사
- RPC로 발사 이벤트(origin, direction, speed) 전달 → 각 클라이언트가 투사체 시뮬레이션
- 피격 판정은 피격자 측에서 처리

### 2-5. 낙사 / 리스폰 — P2 선택

- 맵에 낙사 구역(y < threshold) 존재
- 낙사 판정은 각 캐릭터 소유자의 로컬에서 처리
- 낙사 시 일정 거리 뒤 지정 리스폰 포인트에 재배치
- 리스폰 위치만 동기화 (RPC)

---

## 3. 네트워크 아키텍처

### 스코프 제한
- 별도 서버 로직 없음 (PUN2 마스터 클라이언트가 권한 담당)
- 팀 매칭은 하드코딩 (방에 접속한 순서대로 2인 1팀)
- 로비/UI 없음 (ConnectUsingSettings → 자동 룸 생성/참가)

### 접속 흐름
```
클라이언트 시작
  → PhotonNetwork.ConnectUsingSettings()
  → OnConnectedToMaster()
    → PhotonNetwork.JoinOrCreateRoom("TestRoom", roomOptions, null)
  → OnJoinedRoom()
    → 접속 순서에 따라 팀 배정 (Player 1,2 = Team A / Player 3,4 = Team B)
    → 마스터 클라이언트가 팀별 캐릭터 Instantiate
```

### 팀 구성 (하드코딩)
```
Room 내 Player ActorNumber 기준:
  ActorNumber 1, 2 → Team A (하나의 캐릭터 공유)
  ActorNumber 3, 4 → Team B (하나의 캐릭터 공유)
```
- 최대 4인 (2팀 × 2인) 테스트 환경
- 8인까지 확장 가능하지만 프로토타입에서는 4인으로 제한

### 동기화 전략

#### 컨버전스 모드 — 입력 수집 → 합산 → 결과 브로드캐스트

```
[Player A 클라이언트]                    [마스터 클라이언트]                    [Player B 클라이언트]
       |                                        |                                        |
  WASD 입력                                     |                                   WASD 입력
       |                                        |                                        |
  Vector2 정규화                                |                                  Vector2 정규화
       |                                        |                                        |
  ---- RPC: SendInput(Vector2) ---------------> |                                        |
       |                                        | <--- RPC: SendInput(Vector2) ----------|
       |                                        |                                        |
       |                                  입력 버퍼 (100ms)                               |
       |                                  양쪽 입력 수집                                   |
       |                                        |                                        |
       |                                  Vector2 합산                                    |
       |                                  캐릭터 이동 적용                                 |
       |                                        |                                        |
       |                           OnPhotonSerializeView                                  |
       | <------ 위치/회전 브로드캐스트 -------> | <-------- 위치/회전 브로드캐스트 ------->|
       |                                        |                                        |
  Lerp 보간으로                                 |                              Lerp 보간으로
  부드럽게 표시                                 |                              부드럽게 표시
```

#### 디바이드 모드 — 표준 PUN2 캐릭터 동기화

각 플레이어가 자기 캐릭터의 PhotonView를 소유. PhotonTransformView로 위치 동기화.
고무줄은 양쪽 위치를 참조해 클라이언트 로컬에서 처리.

#### 모드 전환 — 마스터 클라이언트 RPC

```
전환 요청 → RPC → 마스터 클라이언트가 판단 → RPC로 전체에 전환 명령
→ 0.3초 딜레이 → 오브젝트 Destroy/Instantiate → 새 모드 시작
```

### 입력 버퍼 상세

```csharp
// 마스터 클라이언트에서 실행되는 입력 버퍼 로직
private Vector2 player1Input = Vector2.zero;
private Vector2 player2Input = Vector2.zero;
private float bufferTimer = 0f;
private const float BUFFER_TIME = 0.1f; // 100ms — 테스트 중 조절 필요

void Update()
{
    if (!PhotonNetwork.IsMasterClient) return;
    
    bufferTimer += Time.deltaTime;
    
    if (bufferTimer >= BUFFER_TIME)
    {
        Vector2 combined = (player1Input + player2Input);
        // combined.magnitude가 2에 가까우면 같은 방향 (빠름)
        // combined.magnitude가 0에 가까우면 반대 방향 (정지)
        
        ApplyMovement(combined);
        
        // 버퍼 초기화
        player1Input = Vector2.zero;
        player2Input = Vector2.zero;
        bufferTimer = 0f;
    }
}

[PunRPC]
void ReceiveInput(Vector2 input, PhotonMessageInfo info)
{
    // 보낸 사람의 ActorNumber로 어느 플레이어 입력인지 판별
    if (info.Sender.ActorNumber == team.player1ActorNumber)
        player1Input = input;
    else if (info.Sender.ActorNumber == team.player2ActorNumber)
        player2Input = input;
}
```

### 이동 보간 (비마스터 클라이언트)

```csharp
// 비마스터 클라이언트에서 수신된 위치를 부드럽게 따라감
private Vector3 networkPosition;
private Quaternion networkRotation;
private float lerpSpeed = 10f;

void Update()
{
    if (!photonView.IsMine && !PhotonNetwork.IsMasterClient)
    {
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
    }
}

public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
    if (stream.IsWriting) // 마스터가 전송
    {
        stream.SendNext(transform.position);
        stream.SendNext(transform.rotation);
    }
    else // 다른 클라이언트가 수신
    {
        networkPosition = (Vector3)stream.ReceiveNext();
        networkRotation = (Quaternion)stream.ReceiveNext();
    }
}
```

### 지연 마스킹을 위한 이동 커브

```csharp
// 가속/감속 커브로 지연을 자연스럽게 숨김
private Vector2 currentVelocity = Vector2.zero;
private float accelerationTime = 0.15f; // 가속에 걸리는 시간
private float decelerationTime = 0.1f;  // 감속에 걸리는 시간

void ApplyMovement(Vector2 combinedInput)
{
    Vector2 targetVelocity = combinedInput * moveSpeed;
    
    float smoothTime = (targetVelocity.magnitude > currentVelocity.magnitude) 
        ? accelerationTime 
        : decelerationTime;
    
    currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, Time.deltaTime / smoothTime);
    
    characterController.Move(new Vector3(currentVelocity.x, 0, currentVelocity.y) * Time.deltaTime);
}
```

---

## 4. 씬 / 오브젝트 구성

### 씬 구성 (단일 씬)

프로토타입은 **단일 씬**으로 구성합니다. 로비 씬 없음.

```
TestScene
├── Main Camera (Team A 캐릭터 추적)
├── Directional Light
├── NetworkManager (빈 오브젝트)
│   └── NetworkManager.cs (접속, 룸 생성, 팀 배정)
├── Ground (대형 Plane)
├── TestTrack
│   ├── StartPoint_TeamA
│   ├── StartPoint_TeamB
│   ├── FinishLine (단순 트리거)
│   ├── Walls (ProBuilder 또는 Cube)
│   ├── Obstacles (정적 Cube 장애물)
│   └── FallZone (y가 낮은 영역, 트리거)
├── RespawnPoints (빈 오브젝트 여러 개)
└── DebugUI (Canvas)
    ├── PingDisplay (TextMeshPro)
    ├── BufferStatus (TextMeshPro)
    ├── VectorDisplay (TextMeshPro)
    └── ModeDisplay (TextMeshPro)
```

### Resources 폴더 (PhotonNetwork.Instantiate 대상)

```
Resources/
├── ConvergenceCharacter.prefab
│   ├── Capsule (또는 동물 모델)
│   ├── PhotonView
│   ├── ConvergenceController.cs
│   └── Rigidbody (UseGravity, 회전 잠금)
├── DivideCharacter.prefab
│   ├── Capsule (색상으로 Player 구분)
│   ├── PhotonView
│   ├── DivideController.cs
│   └── Rigidbody
└── Projectile.prefab (선택, 새총용)
    ├── Sphere (작은 크기)
    └── Rigidbody
```

### 테스트 트랙 구성

ProBuilder 또는 Unity Primitive(Cube, Plane)로 제작합니다.
```
[Start A] [Start B]
    |         |
    v         v
============================  ← 직선 구간 (벡터 합산 테스트)
         |  |
      [장애물 구간]            ← Cube 장애물 사이로 통과
         |  |
============================  ← 넓은 구간 (충돌/방해 테스트)
         |  |
     [ 낙사 구역 ]            ← 양쪽에 떨어질 수 있는 구간
         |  |
============================
      [Finish Line]
```

길이: 약 100~150 유닛. 1회 완주에 30초~1분 소요되도록 조절.

---

## 5. 스크립트 구조

### 스크립트 목록

| 스크립트명 | 역할 | 부착 대상 |
|-----------|------|----------|
| `NetworkManager.cs` | 접속, 룸 생성, 팀 배정, 캐릭터 스폰 | 씬 내 빈 오브젝트 |
| `ConvergenceController.cs` | 컨버전스 모드 입력 수집/합산/이동 | ConvergenceCharacter 프리팹 |
| `DivideController.cs` | 디바이드 모드 개별 이동 + 고무줄 | DivideCharacter 프리팹 |
| `ModeManager.cs` | 모드 전환 관리 (마스터 권한) | 씬 내 빈 오브젝트 또는 NetworkManager와 합침 |
| `DebugUI.cs` | 핑, 버퍼, 벡터 디버그 표시 | Canvas |
| `FallZone.cs` | 낙사 감지 + 리스폰 (선택) | 낙사 트리거 영역 |
| `CameraFollow.cs` | 카메라가 팀 캐릭터를 추적 | Main Camera |

### NetworkManager.cs 핵심 흐름

```csharp
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        RoomOptions options = new RoomOptions { MaxPlayers = 4 };
        PhotonNetwork.JoinOrCreateRoom("TestRoom", options, null);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room. Player count: {PhotonNetwork.CurrentRoom.PlayerCount}");
        
        // 마스터 클라이언트가 팀 배정 및 캐릭터 스폰 관리
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnTeamCharacter();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // 2명이 모이면 팀 캐릭터 스폰
            AssignTeamAndSpawn();
        }
    }

    void AssignTeamAndSpawn()
    {
        // ActorNumber 기준 팀 배정
        // 1,2 → Team A / 3,4 → Team B
        // 같은 팀 2명이 모이면 ConvergenceCharacter 생성
    }
}
```

### ConvergenceController.cs 핵심 구조

```csharp
using Photon.Pun;
using UnityEngine;

public class ConvergenceController : MonoBehaviourPun, IPunObservable
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float accelerationTime = 0.15f;
    
    [Header("Input Buffer")]
    public float bufferTime = 0.1f; // 100ms, 런타임 조절 가능하도록 public
    
    // 마스터 클라이언트용
    private Vector2 player1Input;
    private Vector2 player2Input;
    private float bufferTimer;
    private Vector2 currentVelocity;
    
    // 비마스터 클라이언트용 보간
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    
    // 팀 정보
    public int player1ActorNumber;
    public int player2ActorNumber;
    
    void Update()
    {
        // 이 팀 소속 플레이어라면 입력 전송
        if (IsMyTeam())
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            photonView.RPC("ReceiveInput", RpcTarget.MasterClient, input);
        }
        
        // 마스터: 버퍼 처리 및 이동
        if (PhotonNetwork.IsMasterClient)
        {
            ProcessBufferAndMove();
        }
        else
        {
            // 비마스터: 보간
            InterpolatePosition();
        }
    }
    
    bool IsMyTeam()
    {
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        return myActor == player1ActorNumber || myActor == player2ActorNumber;
    }
    
    [PunRPC]
    void ReceiveInput(Vector2 input, PhotonMessageInfo info)
    {
        if (info.Sender.ActorNumber == player1ActorNumber)
            player1Input = input;
        else if (info.Sender.ActorNumber == player2ActorNumber)
            player2Input = input;
    }
    
    void ProcessBufferAndMove()
    {
        bufferTimer += Time.deltaTime;
        if (bufferTimer >= bufferTime)
        {
            Vector2 combined = player1Input + player2Input;
            ApplyMovement(combined);
            player1Input = Vector2.zero;
            player2Input = Vector2.zero;
            bufferTimer = 0f;
        }
    }
    
    void ApplyMovement(Vector2 combinedInput)
    {
        Vector2 targetVelocity = combinedInput * moveSpeed;
        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, 
            Time.deltaTime / accelerationTime);
        
        Vector3 move = new Vector3(currentVelocity.x, 0, currentVelocity.y) * Time.deltaTime;
        transform.Translate(move, Space.World);
        // 또는 Rigidbody.MovePosition 사용
    }
    
    void InterpolatePosition()
    {
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(currentVelocity); // 속도도 전송하면 보간 품질 향상
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            currentVelocity = (Vector2)stream.ReceiveNext();
        }
    }
}
```

---

## 6. 구현 우선순위 및 일정

### P0 — 반드시 구현 (Day 1, 8h)

| 시간 | 작업 |
|------|------|
| 0~1h | Unity 프로젝트 생성, PUN2 임포트, AppId 설정, 기본 씬 세팅 (Plane + 조명) |
| 1~3h | NetworkManager 구현 (접속 → 룸 → 팀 배정 → 캐릭터 스폰) |
| 3~6h | **ConvergenceController 구현** (입력 수집 → RPC → 버퍼 → 합산 → 이동 → 보간) |
| 6~8h | 체감 튜닝 (버퍼 크기 50/100/150ms 비교, 가속 커브 조절, 이동 속도 밸런싱) |

**Day 1 완료 기준:** 두 클라이언트에서 하나의 캐릭터를 함께 조작하며, 같은 방향 → 빠름 / 반대 → 정지 / 다른 방향 → 사선 이동이 체감됨.

### P1 — 중요 (Day 2, 8h)

| 시간 | 작업 |
|------|------|
| 0~3h | 디바이드 모드 (캐릭터 분리, 개별 조작, 고무줄 LineRenderer + 당김 force, 모드 전환 RPC) |
| 3~5h | 몸통 박치기 (OnCollision → AddForce, 팀 간 충돌만 활성화) |
| 5~7h | 테스트 트랙 구성 (직선 + 장애물 + 낙사 구역, ProBuilder 또는 Primitive) |
| 7~8h | 통합 테스트 (모드 전환, 충돌, 낙사/리스폰 확인) |

**Day 2 완료 기준:** 컨버전스 ↔ 디바이드 전환이 멀티에서 정상 동작. 팀 간 충돌로 밀림 확인.

### P2 — 선택 (Day 3, 4~8h)

| 시간 | 작업 |
|------|------|
| 0~3h | 버그 수정 및 폴리싱 |
| 3~6h | 검증 플레이 세션 (2인 실제 온라인 + Clumsy 지연 시뮬레이션) |
| 6~8h | 결과 정리 및 Go/No-Go 판단 근거 기록 |

---

## 7. 디버그 UI 명세

**반드시 포함해야 할 디버그 정보** (화면 좌상단, TextMeshPro):

```
[Ping] 47ms
[Buffer] 100ms | Inputs: P1✓ P2✗
[Vector] P1:(0.0, 1.0) + P2:(0.7, 0.7) = Combined:(0.7, 1.7)
[Mode] CONVERGENCE
[Players] Room: 4/4 | Team A: P1,P2 | Team B: P3,P4
```

- Ping: `PhotonNetwork.GetPing()`
- Buffer: 현재 버퍼 타이머 값, 각 플레이어 입력 수신 여부
- Vector: 양쪽 입력과 합산 결과를 실시간 표시 (동기화 상태 시각적 확인)
- Mode: 현재 모드 (CONVERGENCE / DIVIDE / TRANSITIONING)

**버퍼 크기 런타임 조절:**
- `[` `]` 키로 버퍼 시간 ±10ms 조절 가능하도록 구현
- 테스트 중 최적 버퍼 값을 찾기 위함

---

## 8. 필요 에셋 및 사전 준비

### Unity 패키지

| 패키지 | 설치 방법 | 용도 |
|--------|----------|------|
| PUN 2 - FREE | Asset Store | 네트워크 |
| ProBuilder | Package Manager (Unity 내장) | 테스트 트랙 제작 |
| TextMeshPro | Package Manager (Unity 내장) | 디버그 UI |

### 캐릭터

프로토타입에서는 **Unity Capsule**을 사용합니다.
- Team A: 파란색 Material
- Team B: 빨간색 Material
- 디바이드 모드 시 Player 1/2 구분: 밝은/어두운 톤 차이

### 외부 도구 (테스트용)

| 도구 | 용도 |
|------|------|
| **ParrelSync** | 1PC에서 Unity Editor 2개 실행 (개발 중 테스트) |
| **Clumsy** (jagt.github.io/clumsy) | 의도적 네트워크 지연 추가 (100~300ms 시뮬레이션) |
| **OBS Studio** | 플레이 영상 녹화 (검증 근거) |

### 사전 설정

1. Photon 계정 생성 → AppId 발급 (photonengine.com)
2. PUN2 임포트 후 PhotonServerSettings에 AppId 입력
3. Server Region: `kr` (한국) 또는 `jp` (일본) 설정
4. ParrelSync 설치 후 클론 프로젝트 생성

---

## 9. 테스트 시나리오

프로토타입 완성 후 다음 시나리오를 순서대로 수행합니다:

### 기본 검증 (ParrelSync, 로컬)

1. **동시 전진:** 두 클라이언트에서 W를 동시에 누름 → 캐릭터가 빠르게 전진하는지 확인
2. **반대 입력:** 한쪽 W, 한쪽 S → 캐릭터가 정지하는지 확인
3. **사선 이동:** 한쪽 W, 한쪽 D → 우상단 사선으로 이동하는지 확인
4. **한 명만 입력:** 한쪽만 W → 절반 속도로 전진하는지 확인
5. **모드 전환:** Space → 디바이드 → 개별 이동 → Space → 컨버전스 복귀
6. **충돌 테스트:** Team A와 Team B가 정면 충돌 → 밀림 확인

### 지연 검증 (Clumsy)

7. **150ms 지연:** Clumsy로 150ms 추가 → 시나리오 1~6 반복, 체감 변화 기록
8. **200ms 지연:** 200ms로 상향 → 동일 테스트, "네트워크가 느리다"는 인상이 드는지 기록
9. **300ms 지연:** 300ms 극단 테스트 → 어디서 체감이 깨지는지 확인

### 실제 온라인 검증 (빌드 후)

10. **.exe 빌드** → 팀원 1명에게 전달 → Discord 음성 통화 상태에서 시나리오 1~6 수행
11. 실제 Photon Cloud 경유 RTT에서의 체감 확인

---

## 10. Go / No-Go 판단 기준

### Go (본 개발 진행)
- 컨버전스 모드에서 입력 합산이 체감상 자연스럽게 동작
- 디바이드 모드 전환 시 양쪽 클라이언트 상태 일치
- Clumsy 150ms 환경에서도 "네트워크가 느리다"는 인상 없음
- 충돌 방해가 시각적으로 인지 가능

### Conditional Go (조건부 진행)
- 벡터 합산은 동작하지만 버퍼 튜닝이 더 필요
- 모드 전환 시 간헐적 깜빡거림이 있으나 연출로 대응 가능
→ 본 개발 시 해결책과 함께 진행

### No-Go (기획 재검토)
- 컨버전스 모드에서 캐릭터가 흔들리거나 워핑이 심함
- 모드 전환 시 한쪽 클라이언트가 다른 상태에 머물러 복구 불가
- 150ms 지연에서 "네트워크가 느리다"는 인상이 명확함
→ PUN2 대신 Photon Fusion/Quantum 검토 또는 기획 메커니즘 수정

---

## 11. 스코프 제한 및 기술적 주의사항

> 아래는 코딩 스타일이나 설계 원칙이 아닌, **이 프로토타입의 범위와 기술적 제약**에 관한 사항입니다.
> 코딩 원칙(SOLID, YAGNI 등)은 `CLAUDE.md`를 따릅니다.

### 프로토타입 범위 밖 (만들지 말 것)
- ❌ 서버 권한(Server-authoritative) 구조 구현하려 하지 마세요. PUN2는 릴레이 서버입니다.
- ❌ 완벽한 위치 동기화를 추구하지 마세요. 이 게임은 "대략적 일치"면 충분합니다.
- ❌ 로비/매치메이킹 UI를 만들지 마세요. 하드코딩으로 자동 접속합니다.
- ❌ 물리 엔진(Rigidbody)에 지나치게 의존하지 마세요. 클라이언트 간 물리 비결정성이 문제를 키웁니다. Transform 직접 조작 + 간단한 충돌만 사용하세요.
- ❌ 에셋/이펙트/사운드에 시간을 쓰지 마세요.

### 기술적 요구사항 (반드시 포함)
- ✅ 버퍼 크기를 Inspector 또는 런타임에서 조절 가능하게 public으로 노출하세요.
- ✅ 디버그 UI를 반드시 넣으세요. 동기화 상태를 눈으로 봐야 합니다.
- ✅ 가속/감속 커브를 적용하세요. 즉각 반응은 지연을 드러나게 합니다.
- ✅ 모든 모드 전환은 마스터 클라이언트 권한으로 처리하세요.
- ✅ ParrelSync로 개발 중 수시로 2클라이언트 테스트하세요.

---

## 12. 기획 원문 참조

> 아래는 기획서의 핵심 내용을 요약한 것입니다. 구현 시 참고하세요.

### 게임 컨셉
- 장르: 캐주얼 협동 레이싱 / 파티 게임
- 플랫폼: PC
- 타겟: 2인 1조 (커플/친구)
- 레퍼런스: 폴가이즈, PHOGS!, Paddle x3
- 1판 소요시간: 3~7분
- 최대 수용 인원: 8인 (4팀 × 2인)

### 코어 루프 (프로토타입에서는 레이싱 부분만 검증)
```
로비 → 팀 매칭 → 레이싱 (컨버전스/디바이드 전환) → 결승 → 하위 팀 탈락 → 반복 → 최종 1팀 → 팀 내 1:1 결전
```
프로토타입 범위: `레이싱 (컨버전스/디바이드 전환)` 부분만

### 캐릭터 특성
- 외형: 귀엽고 캐주얼한 동물 (프로토타입에서는 Capsule)
- 물리: 폴가이즈/PHOGS! 스타일의 물렁물렁한 느낌
- 이 물렁물렁한 느낌이 네트워크 지연 마스킹에 핵심적 역할을 합니다

### 방해 액션 상세
| 액션 | 설명 | 프로토타입 구현 |
|------|------|---------------|
| 길막 | 경로를 물리적으로 차단 | 캐릭터 충돌 자체로 구현됨 |
| 몸통 박치기 | 충돌로 상대를 밀어냄 | P1 구현 |
| 새총 (튕겨서 박치기) | 고무줄 파트너를 투사체로 발사 | P2 선택 |
| 고추 | 맵 아이템으로 불 뿜기 | 프로토타입 제외 |
