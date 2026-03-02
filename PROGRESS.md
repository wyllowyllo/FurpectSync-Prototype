
# FurpectSync Prototype — Progress

## Day 1 (2026-03-01) — P0 컨버전스 모드 핵심

### 완료
- [x] 폴더 구조 생성 (Scripts/Network, Scripts/Character, Scripts/UI, Resources, Materials)
- [x] `NetworkManager.cs` — Photon 접속, 룸 참가, ActorNumber 기반 팀 배정, 팀 2명 충족 시 캐릭터 스폰
- [x] `ConvergenceController.cs` — CharacterController.Move() 기반 이동, 입력 RPC 전송(20Hz), 입력 버퍼(100ms), 벡터 합산, 가속/감속 커브, OnPhotonSerializeView 위치 동기화, Lerp 보간, 버퍼 크기 `[]` 키 조절, Cinemachine 카메라 타겟 설정
- [x] `DebugUI.cs` — Ping, Buffer 상태, Vector 표시, Mode, Players 디버그 오버레이
- [x] PUN2 임포트 + AppId 설정
- [x] Cinemachine 설치 (`com.unity.cinemachine` 3.1.6)
- [x] TextMeshPro 설치 (TMP Essential Resources 임포트 완료)
- [x] TestScene 생성 (Plane, Light, Camera, NetworkManager 오브젝트, SpawnPoints)
- [x] TestScene에 CinemachineCamera 추가 + Body 설정
- [x] ConvergenceCharacter.prefab 생성 (Resources/ — Capsule + PhotonView + ConvergenceController + CharacterController)
- [x] PhotonView에 ConvergenceController를 Observed Component로 등록
- [x] 팀 머티리얼 생성 (TeamA=Blue, TeamB=Red, Eye)
- [x] DebugUI Canvas 구성 (Canvas + TMP 텍스트 + DebugUI 컴포넌트 연결)
- [x] SoStylized 에셋 임포트
- [x] 비마스터인 자기 팀 플레이어의 권위 보정 추가
- [x] 2클라이언트 테스트 (빌드+에디터)

### 미완료
- (없음 — Day 1 목표 모두 완료)

### 알려진 이슈
- **ActorNumber 재사용 불가**: Photon ActorNumber는 재접속 시 증가함. 팀 배정이 ActorNumber 1,2/3,4 하드코딩이므로, 테스트 간 방 재시작 필요. (심각도: 낮음, 프로토타입 한정)
- **PUN2 Vector2 RPC 직렬화**: Vector2 직접 전송 시 직렬화 실패 가능. float x, float y 분리 전송으로 대응 완료.
- **URP material.color**: URP Lit 셰이더는 `_BaseColor` 프로퍼티 사용. `SetColor("_BaseColor", color)`로 대응 완료.
