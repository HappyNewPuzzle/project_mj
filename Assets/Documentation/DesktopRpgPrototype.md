# Desktop RPG Prototype

Unity 6000.3.19f1 / URP 2D 기반의 Windows x64 데스크톱 오버레이 프로토타입입니다. `Mojinloop > Rebuild Desktop RPG Prototype` 메뉴가 Scene, Prefab, ScriptableObject 설정과 Build Settings를 생성합니다.

## 구조

- `Desktop`: Win32 투명·테두리 없는 Topmost 창, 작업 영역 우하단 배치, click-through
- `InputActivity`: Editor 입력 소스와 Windows 저수준 키보드/마우스 훅, 메인 스레드 전달
- `Gameplay`: 0.5초 입력 유지 상태, Hero 상태, 배경 스크롤, 전체 흐름
- `Combat`: 몬스터 이동/피해/사망/재생성, 데미지와 코인 효과
- `Data`: 창/영웅/몬스터/게임 수치를 조절하는 ScriptableObject

## 보안 및 입력 프라이버시

Windows 훅 콜백은 키 코드, 문자, 프로세스, 입력 순서, 커서 위치를 읽거나 저장하거나 전송하지 않습니다. 허용된 key-down, mouse-button-down, wheel 메시지인지 확인하고, `LLKHF_INJECTED`/`LLMHF_INJECTED` 계열 플래그가 있는 자동화 입력은 무시한 뒤 `Interlocked.Exchange`로 단일 활동 플래그만 설정합니다. Unity API는 훅 콜백에서 호출하지 않고 `Update`에서 플래그를 소비합니다. 이벤트별 로그도 남기지 않습니다.

## 실행 및 빌드

1. Unity에서 `Mojinloop > Rebuild Desktop RPG Prototype`을 실행합니다.
2. `Assets/Scenes/DesktopRpgPrototype.unity`를 열고 Play 합니다. Editor에서는 Game View 입력이 mock 활동으로 처리됩니다.
3. Build Profiles에서 Windows, x86_64, IL2CPP를 선택하고 빌드합니다. 생성 메뉴가 640x240 Windowed, Run In Background, 시작 Scene을 설정합니다.
4. Windows Player에서 메모장/브라우저에 포커스를 둔 채 키 입력 또는 클릭으로 캐릭터가 0.5초간 동작하는지 확인합니다. F10은 개발용 Pause입니다.

## 주요 Inspector 값

- Active Hold Duration 0.5초, World Scroll Speed 2.2, Attack Range 1.35
- Hero Damage 10, Attack Interval 0.4초
- Monster HP 30, Move Speed 1.2 units/sec, Spawn Delay 0.8초
- 창 640x240, 우측 여백 16, 하단 여백 8, Topmost/Click-through 활성

외부 패키지는 추가하지 않았습니다. Unity 패키지 라이선스만 적용됩니다.

## 테스트 체크리스트

- 투명 영역에서 바탕화면이 보이고 캐릭터/HUD는 보이는지
- 테두리/타이틀바가 없고 작업 표시줄을 침범하지 않는지
- 다른 앱 포커스 중 전역 입력 활동으로 Run/Attack이 재개되는지
- 마지막 입력 0.5초 뒤 즉시 Idle, 스크롤/이동/추가 피해가 정지하는지
- 몬스터 사망 후 코인과 다음 몬스터가 나오는지
- Player 종료 뒤 훅이 해제되고 다른 앱 입력이 정상인지

## 제한 및 교체 항목

Hero는 `Reference/character.png`의 첫 Idle 프레임, Monster는 `Reference/monster.png`의 초록 드래곤 첫 Idle 프레임을 사용합니다. 반복 Ground만 색상 사각형 placeholder이며, Run/Attack/Hit/Die Animator Clip은 같은 원본 시트의 나머지 프레임으로 확장해야 합니다. 효과는 uGUI Text 기반입니다. 다중 모니터에서는 주 디스플레이 작업 영역을 기준으로 배치합니다. 실제 Windows 투명/Topmost/전역 훅은 Editor가 아니라 Windows Standalone에서만 검증할 수 있습니다.

백신은 저수준 훅을 민감하게 볼 수 있습니다. 코드 서명, Steam 배포 빌드의 평판 확보, 개인정보 미수집 설명을 권장합니다. 훅은 게임 프로세스 내부에서만 설치되고 종료 생명주기에서 해제됩니다.
