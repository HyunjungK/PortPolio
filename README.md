# Bakery - AI-Assisted Unity Casual RPG Prototype

Unity UGUI 기반의 캐주얼 베이커리  RPG 프로토 타입입니다.
초기 더미 UI와 단색 캐릭터 이미지를 기반으로, AI 생성 리소스와 Unity 런타임 구조를 결합해 포트폴리오 수준의 전투 화면과 UI 흐름으로 개선했습니다.

## 프로젝트 핵심

Bakery는 빵집 운영 화면과 전투 스테이지를 포함한 모바일 게임 프로토타입입니다.
이번 개선 작업에서는 단순한 화면 꾸미기보다, 실제 게임 구조 안에서 UI, 캐릭터, 애니메이션, 카메라 추적, 리소스 파이프라인이 함께 동작하도록 정리했습니다.

## 주요 구현

### 1. Unity UGUI 기반 UI 리디자인

- Main, WorldMap, Stage 페이지의 더미 UI를 목재/양피지 톤의 판타지 베이커리 스타일로 개선
- PopupArchive, PopupMakeBread, PopupDisplay, PopupFacility, PopupLevelup, PopupOffline, PopupResult UI 정리
- ScrollView/GridLayout 콘텐츠 위치 문제 수정
- 버튼, 패널, 텍스트, 보상 슬롯의 시각 스타일 통일

### 2. AI 생성 리소스 통합

- 별도 아트 리소스가 없는 상태에서 AI 이미지 생성 도구로 캐릭터, 몬스터, 스테이지 배경 제작
- 생성 이미지를 Unity 프로젝트의 'Assets/Art/GeneratedCharacter', 'Assets/Art/GeneratedBackgrounds'에 편입
- Texture Import 설정을 Sprite 용도로 조정
- Player/Monster 프리팹의 기존 더미 Image를 실제 캐릭터 스프라이트로 교체

### 3. 캐릭터 애니메이션 연결

- Player/Monster에 AnimatorController 연결
- Idle, Attack, Hit 애니메이션 클립 구성
- 상태머신과 Animator 사이를 'CharacterAnimationBridge'로 분리
- 게임 로직이 Animator 파라미터에 직접 강하게 의존하지 않도록 구성

### 4. 방향 전환 및 공격 방향 처리

- 이동 방향에 따라 캐릭터가 좌우 플립되도록 'CharacterFacing' 구현
- 공격 중에는 이동 방향보다 타겟 방향을 우선하도록 처리
- 플레이어가 이동 중 공격하더라도 몬스터를 바라보고 공격하도록 개선

### 5. 플레이어 추적형 Stage 화면

- 기존에는 캐릭터와 UI가 같은 Canvas에 묶여 있어 화면 추적 연출이 어려웠음
- 'BattleWorld' 레이어를 분리하여 배경/캐릭터/몬스터만 이동
- HUD, 타이머, 수집 패널, 버튼은 고정 UI로 유지
- 'StageCameraFollow'를 통해 플레이어 위치를 따라 전투 월드가 부드럽게 이동

## AI 활용 포인트

이 프로젝트에서 AI는 단순 이미지 생성 도구가 아니라, 프로토타입 완성도를 빠르게 끌어올리는 제작 파이프라인의 일부로 활용했습니다.

### 사용 방식

- 더미 리소스 분석 후 프로젝트 분위기에 맞는 캐릭터/몬스터/배경 콘셉트 도출
- AI 이미지 생성 프롬프트를 게임 화면 요구사항에 맞게 작성
- 생성 결과물을 Unity Sprite로 임포트하고 프리팹에 연결
- 기존 상태머신, Animator, UGUI 계층 구조에 맞춰 실제 런타임에서 동작하도록 통합
- UI 개선, 리소스 연결, 코드 수정, Unity CLI 검증을 반복 수행

### AI 활용으로 해결한 문제

| 문제 | 해결 |
| --- | --- |
| 아트 리소스 부재 | AI 이미지 생성으로 플레이어, 몬스터, 배경 제작 |
| 더미 UI로 인한 낮은 완성도 | UGUI 프리팹 구조를 유지하면서 시작 스타일 개선 |
| 생성 이미지만으로는 게임에서 동작하지 않음 | Sprite Import, prefab binding, Animator 연결까지 구현 |
| 이동 방향과 공격 방향 충돌 | 상태머신 기준으로 공격 중 타겟 방향 우선 처리 |
| UI와 전투 오브젝트가 같은 Canvas에 묶임 | BattleWorld 분리 및 플레이어 추적 화면 구현|

## 코드 하이라이트

### 'StageCameraFollow.cs'

플레이어 위치를 기준으로 전투 월드 레이어만 이동시키는 카메라 추적 로직입니다.
HUD는 고정하고 배경/캐릭터만 움직이도록 'BattleWorld' 구조와 함께 사용합니다.

### 'CharacterFacing.cs'

캐릭터의 기본 방향과 이동 방향을 기준으로 좌우 플립을 처리합니다.
공격 중에는 타겟 방향이 우선되도록 상태머신과 연동했습니다.

### 'CharacterAnimationBridge.cs'

상태머신과 AnimatorController 사이를 연결하는 브릿지 컴포넌트입니다.
'Idle', 'Attack', 'Hit' 애니메이션 호출을 한 곳에 모아 캐릭터 로직을 단순하게 유지했습니다.

### 'CharacterStateMachine.cs'

전투 시작, 플레이어/몬스터 스폰, 몬스터 리스폰, 스테이지 클리어, 결과 팝업 호출을 담당합니다.
'pageStage.CharacterRoot'를 통해 캐릭터가 고정 UI가 아닌 'BattleWorld' 아래 생성되도록 변경했습니다.

## 개선 전 / 개선 후

| 구분 | 개선 전 | 개선 후 |
| --- | --- | --- |
| UI | 더미 배치 중심 | 목재/양피지 톤으로 통일 |
| 캐릭터 | 단색 더미 Image | AI 생성 제빵사 캐릭터 |
| 몬스터 | 단색 더미 Image | AI 생성 꿀 반죽 몬스터 |
| 배경 | 기존 빵집 배경 재사용 | 전투용 꿀숲 베이커리 배경 |
| 애니메이션 | 없음 | Idle / Attack / Hit |
| 방향 처리 | 이동 방향 중심 | 공격 중 타겟 방향 우선 |
| 화면 구성 | UI와 캐릭터가 같은 Canvas 레이어 | BattleWorld와 고정 UI 분리 |

## 기술 스택

- Unity 6
- C#
- Unity UGUI
- Animator / AnimationClip
- Prefab workflow
- AI image generation workflow
- Unity CLI based validation

## 포트폴리오 한 줄 요약

> AI 생성 리소스를 Unity UGUI 기반 게임 구조에 통합하고, 프리팹, Animator, 상태머신, 플레이어 추적형 전투 화면까지 연결해 더미 프로토타입을 포트폴리오 수준의 모바일 RPG 화면으로 개선했습니다.


