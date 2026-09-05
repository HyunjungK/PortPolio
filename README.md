# PortPolio

[코드 설명]

CResourceManager.cs : 리소스(Prefab, Sprite 등) 데이터를 가져올 때 공통적으로 사용할 수 있는 코드이다.

UIManager.cs : 팝업 및 페이지를 공통으로 관리할 수 있는 코드이다.

CharacterBase.cs / CharacterStateMachine.cs

: Monster와 Player를 공통으로 관리하기 위해서 만든 베이스 코드이다. 상태에 따라 행동이 달라지도록 상태패턴을 이용하여 작업하였다.

유니티 전반적인 작업은 unity CLI 를 이용하여 MCP서버를 연결해서 작업하였다.
AI는 코덱스를 이용하였고, 스킬은 unity-cli를 사용하면서 
프리팹 같은 경우에는 unity-ugui 스킬을 사용하였다.

여기에서 사용한 리소스의 경우에는 클로드로 프롬프트 만들어서 나노바나나로 뽑은 것도 있고,
코덱스의 imagen 이용해서 생성한 것도 있다.

