# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**FurpectSync Prototype** — 2인 1캐릭터 협동 레이싱 멀티플레이어 프로토타입.
핵심 검증 목표: PUN2 환경에서 벡터 합산 이동이 체감상 자연스러운지 확인.

- **엔진:** Unity 6000.3.x (Unity 6)
- **렌더 파이프라인:** URP
- **네트워크:** Photon PUN 2 (FREE)
- **플랫폼:** PC
- **최대 인원:** 4인 (2팀 x 2인)
- **전체 명세:** `PROTOTYPE_SPEC.md` 참조

## Code Style & Design Principles

### SOLID 원칙

- **SRP:** 클래스는 단 하나의 변경 이유만 가져야 한다.
- **OCP:** 확장에는 열려 있고, 수정에는 닫혀 있어야 한다.
- **LSP:** 상위 타입을 하위 타입으로 치환해도 문제없이 작동해야 한다.
- **ISP:** 단일 목적의 작은 인터페이스를 선호한다.
- **DIP:** 고수준/저수준 모듈 모두 추상화에 의존해야 한다.

### 디미터의 법칙 (LoD)

"오직 가장 가까운 친구와만 이야기하라." `a.GetB().GetC().DoSomething()` 같은 Train Wreck 패턴 금지.

### 보이스카우트 원칙

코드를 발견했을 때보다 더 깨끗하게 만들어 놓고 떠나라. 중복 제거, 네이밍 개선, 미사용 변수/죽은 코드 정리.

### YAGNI (You Aren't Gonna Need It)

- 현재 사용되지 않는 이벤트, 함수, 콜백은 작성하지 않는다.
- "나중에 쓸 것 같다"는 추측으로 미리 구현 금지.
- 빈 메서드, 미사용 인터페이스 멤버, 호출처 없는 유틸리티 함수 금지.
- **예외:** 프레임워크/엔진이 강제하는 생명주기 메서드 (예: `Start()`, `Update()`)는 제외.
