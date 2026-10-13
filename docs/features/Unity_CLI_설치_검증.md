### Unity CLI 설치 검증

- 기능 설명: Unity CLI가 현재 프로젝트의 Editor에 연결되어 독립 검증 씬을 생성·저장·재조회할 수 있는지 확인한다.
- 동작 조건 및 처리 흐름: 공식 unity CLI에서 대상 프로젝트를 명시해 상태와 Console 기준선을 조회한다. 기존 열린 Untitled 씬에 미저장 변경이 없음을 확인한 뒤 새 씬을 열고 카메라·조명·바닥·큐브를 배치한다. 검증 씬만 저장하고 다시 열어 오브젝트 및 누락 스크립트를 조회하며 카메라 화면을 캡처한다. 배포 빌드 목록에는 추가하지 않는다.
- 구현 상태: CLI 연결, 씬 생성·저장·재조회 및 화면 캡처 완료. 현재 Editor에 검증 씬을 열어 두었다. 기존 컴파일 오류 7건 때문에 프로젝트 전체 정상 상태와 Play 검증은 미확인이다.

#### 관련 코드

| 파일 경로 | 클래스 / 메서드 | 역할 |
| --- | --- | --- |
| Assets/Scenes/TestScene/UnityCliSmokeTest.unity | 독립 씬 | Main Camera, Directional Light, CLI Test Ground, CLI Created Cube 배치 |
| Assets/Scenes/TestScene/UnityCliSmokeTest.unity.meta | GUID 677fd1aa1c7fe0843830a48cdf52b89c | Unity Editor에서 생성한 씬 식별 정보 |
| Packages/manifest.json | com.unity.pipeline | 공식 unity CLI와 현재 Editor 연결. 이번 작업에서 변경하지 않음 |

#### 연결 및 협업

- 연결된 기능: 없음. 플레이어 및 게임 세션 없이 CLI 연결만 검증한다.
- 공유하는 코드 및 상태: 프로젝트의 기존 URP 렌더링 설정을 사용한다. 제품 스크립트와 프리팹을 참조하지 않는다.
- 중복 또는 충돌 사항: 기존 게임 공통 구조 검증 씬과 목적이 다르다. 기존 씬 파일·게임 코드·패키지·빌드 설정은 수정하지 않았다. Unity가 저장 경로 없는 Untitled 씬과 새 씬의 추가 로드를 거부하여, 미저장 변경이 없음을 확인한 뒤 단독 씬으로 생성했다.
- 승인 필요 사항 및 결과: 사용자가 CLI 설치 확인용 새 씬 생성을 요청했다. 테스트 코드는 작성하지 않았다. 현재 master 브랜치 지침에 따라 Git CLI와 커밋을 수행하지 않았다.

#### 실제 검증 결과

- 공식 unity 버전: 1.0.0-beta.12. 상태 조회에서 현재 프로젝트, 포트 7800, Editor 6000.3.22f1, ready 확인.
- 초기 샌드박스 조회에서는 인스턴스가 보이지 않았으나 승인된 로컬 실행에서 정상 연결을 확인했다.
- C# 조회 및 생성: unity command eval로 실행. 씬 파일 저장 성공 후 OpenScene으로 다시 열어 저장 상태와 루트 4개를 확인했다.
- 저장된 큐브 위치: (0, 0.5, 0). 누락 스크립트 0개. EditorBuildSettings에 검증 씬 없음.
- 화면 확인: unity command screenshot --view game --width 960 --height 540 실행. 프로젝트의 현재 렌더링 설정에서 검은 배경 위 바닥과 큐브의 흰 윤곽선이 보임을 확인했다. Play 중 캡처는 아니다.
- Console 회귀: 생성 전 커서 2877 및 세션 04b34e721c5740d99922688470098d9a 기준으로 생성 후 신규 경고·오류 0건. 기존 compilationFailed=true, Console 오류 7건은 유지된다.
- 기존 오류: unity-cli-workspace 패키지에 포함된 UnityCliInputTestSceneGenerator, UnityCliUiTestSceneGenerator, UnityCliPerfSceneGenerator가 UnityCliInputSimulationTestBootstrap 등 테스트용 타입을 찾지 못하는 CS0246/CS0103 오류 7건. manifest에 akiojin/unity-cli 저장소 전체가 unity-cli-workspace로 등록되어 있고, 패키지 문서의 Bridge 설치 경로는 UnityCliBridge/Packages/unity-cli-bridge이다. 이번 요청에서는 해당 설치 설정을 변경하지 않았다.
- 별도 unity-cli 실행 파일: v0.4.1 확인. --project 대상 조회에서 인스턴스를 찾지 못했으며, 이 실행 파일과 akiojin Bridge 간 연결 성공은 미확인이다. 위 씬 생성 검증은 공식 unity 명령을 사용했다.
- Play 및 빌드: 실행하지 않음. 기존 컴파일 오류가 남아 있어 프로젝트 전체 정상 설치 판정은 보류한다.
