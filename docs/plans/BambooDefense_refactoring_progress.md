# BambooDefense 리팩토링 진행 기록

## 재개할 때 먼저 읽기

- 실행 요청: 2026-10-14 사용자가 계획의 모든 질문에 권장안을 승인하고 S0~S7 순차 실행·진행 문서 갱신을 요청했다.
- 계획: [BambooDefense 전환 리팩토링 계획](BambooDefense_전환_리팩토링_계획.md)
- 현재 상태: **사용자 요청으로 작업 중단·인계. S3 코드 적용 및 컴파일 확인 완료, S3 Play 검증 전**.
- 재개 지점: 이 문서의 **다른 터미널에서 재개하는 순서**부터 수행한다. S1을 다시 구현하거나 스폰 값을 다시 이전하지 않는다.
- 중요: 마지막 조회에서 Unity는 Play=false, compiling=false, 활성 씬은 `Assets/Scenes/Defense/BambooDefense.unity`, **dirty=true**였다. 미저장 변경의 원인을 확정하지 못했으므로 자동 저장·씬 재로드·재생성·에디터 종료를 하지 않았다. 재개 시 편집 중인 내용을 먼저 확인한다.
- 2026-10-14 추가 지침: 프로젝트 내부 수정은 별도 승인 요청 없이 수행한다. 기존 작업·연결 보존과 검증은 계속한다.
- Git: 현재 `refactoring`. AGENTS.md에 따라 Git CLI·커밋·브랜치 변경을 수행하지 않는다.
- 테스트 코드 작성 금지. 실행 검증은 Unity CLI의 읽기/Play 조작·빌드와 수동 절차로 수행한다.
- 사용자 승인: 구현과 Unity 에셋 이전·실행 검증을 포함한다. 과거 UNITY_REFACTOR_CONTEXT.md의 조사만 수행하라는 제한은 이번 명시적 구현 요청으로 갱신되었다.

## 확정한 제품 범위

- 정식 플레이: **Intro → BambooDefense → 게임오버 → 현재 씬 재도전**.
- 기존 Build 게임: 새 빌드 검증까지 보존. 이후 역참조·제작 도구 의존을 확인한 목록만 정리한다. 기존 콘텐츠 일괄 삭제는 승인으로 해석하지 않는다.
- 생존 기록: 현재 세션 화면 표시. 최고 기록·세이브 기능 추가 없음.
- 검증 목표: Windows Standalone 64비트. 기존 키보드·마우스·패드 연결 보존. 실제 하드웨어 입력 확인은 API 조작과 구분한다.
- 밸런스·피해·패링·회피·연계·시간 기준 보존. 버그가 확인되면 별도 항목으로 기록한다.

## 단계 현황

| 단계 | 상태 | 변경 / 완료 근거 | 다음 조건 |
| --- | --- | --- | --- |
| S0 기준선 | 기본 실행 기준 확보 | 375개 사본/해시, Play 스폰·피격·게임오버·Restart API 실행 | 실제 입력·전체 전투 경계는 최종 검증에 유지 |
| S1 Inspector/생성기 | 완료 | console_status compilationFailed=false, errors=0. Build 호출 전후 씬 bytes 동일·dirty=false | 신규 생성만 허용 |
| S2 Director 책임 분리 | 구현·핵심 검증 완료, 자연 재사용 관찰 보류 | 컴파일 오류0. 22/22 값 보존. 스폰·처치2건·투척수 재사용·근접 제어 재사용/HP30/돌진형 확인 | 특정 근접 인스턴스의 자연 재사용 미관찰 원인 확인. 실패 원인은 확정하지 않음 |
| S3 수명/취소/재사용 | 코드·컴파일 확인, Play 미검증 | 예약 권한 Playing 제한, 투척수 공통 취소 경로. 마지막 console_status: compilationFailed=false, compiling=false, errors=0 | 종료·예약 취소·재활성화 Play 확인 |
| S4 HUD 에셋 이전 | 대기 | 없음 | S3 |
| S5 공유 전투 정리 | 대기 | 없음 | 방어 기준 검증 |
| S6 정식 진입/빌드 | 대기 | 없음 | 앞 단계 검증 |
| S7 에셋/문서 정리 | 대기 | 없음 | 정식 빌드 흐름 검증 |

## 환경과 도구

- 프로젝트: `C:/Users/JUNGLE/Documents/w07-blindblade-plus`
- Unity CLI: `C:/Program Files/Unity Hub/resources/cli/unity.exe` (PATH에는 없음).
- 연결 조회: `unity.exe status --format json`을 샌드박스 밖에서 실행해 프로젝트 PID 21720, 포트 7800, state ready 확인.
- 샌드박스 안 status는 STATUS_NO_INSTANCES로 실패한다. 연결 실패를 에디터 미실행으로 오인하지 않는다.
- Editor: `C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe`.
- Unity command에는 `--caller plugin --skill unity-cli --project-path C:/Users/JUNGLE/Documents/w07-blindblade-plus`를 전달한다. UI 명령은 ui-ugui 스킬 기준을 따른다.
- rg는 현재 PATH에서 찾지 못했다. 파일 탐색은 PowerShell Get-ChildItem/Select-String으로 수행한다.
- 설치된 CLI는 1.0.0-beta.5이며 `shell --no-pager`는 지원하지 않는다. 해당 옵션을 추가하지 않는다. CLI 업그레이드는 수행하지 않았다.
- 이전 도구 터미널 ID 8538의 CLI shell은 인계 시 종료했다. Unity Editor는 종료하지 않았다. 새 터미널은 아래 명령으로 다시 연결하며 이전 터미널 ID에 의존하지 않는다.

## 기준선과 복구

- 기준 커밋 해시: `a7b646ca3ded556de516c0fd3f4466ee96490782` (refs 직접 읽기). 미커밋 파일 기준은 아래 사본이다.
- 로컬 사본: `UserSettings/Refactoring/BambooDefense-20261014/Baseline` (375개, 6,445,991 bytes).
- SHA256 목록: `UserSettings/Refactoring/BambooDefense-20261014/baseline-manifest.json`.
- UserSettings는 Git 무시 대상이다. 다른 컴퓨터에서 재개한다면 미커밋 기준 사본도 함께 전달한다.
- 복구는 해당 단계 코드·에셋·.meta·문서를 함께 수행한다. 사용자 변경을 전체 리셋하지 않는다.

## 변경 파일

- `docs/plans/BambooDefense_전환_리팩토링_계획.md`: 승인된 권장안과 재개 규칙 추가.
- `docs/plans/BambooDefense_refactoring_progress.md`: 이 진행 기록 생성.
- `Assets/Editor/BambooDefenseSceneBuilder.cs`: 기존 씬 재생성 차단 및 신규 Spawner 연결.
- `Assets/Editor/BambooDefenseRefactoring.cs`: 명시적 직렬화 이전 도구.
- `Assets/Scripts/Defense/DefenseArenaBoundary.cs`: 신규 경계 기본값 23.6, 단위 설명.
- `Assets/Scripts/Defense/BambooDefenseDirector.cs`: 세션 기록과 구성 연결만 소유.
- `Assets/Scripts/Defense/BambooThrower.cs`: S3 StopThrowCycle 공통 종료 경로 추가.
- `Assets/Scripts/Defense/DefenseEnemySpawner.cs`: 배치·스폰·적 재사용 및 처치 알림.
- `Assets/Scripts/Defense/DefenseThrowBudget.cs`: 예약·비행 집계와 종료 구독.
- `Assets/Scenes/Defense/BambooDefense.unity`: Spawner 추가, 기존 22개 저장값과 프리팹/UnitRoot 참조 이전.
- `docs/features/대숲_방어.md`: 현재 책임·Inspector 조정 위치·수명 변경과 진행 문서 링크 추가. 나머지 과거 이력 정리는 S7에 남아 있다.
- 신규 스크립트 3개는 각각 `.meta`도 함께 보존해야 한다. GUID: Spawner `14c4bf4e5ad8c964d96be4f5092aad87`, ThrowBudget `1328648fdae09d84a9ab6b9f2ce35b81`, 이전 도구 `250d2ce70656c624d937b643e37213ce`.

## 다른 터미널에서 재개하는 순서

1. 이 문서, AGENTS.md, FEATURE_SPEC.md, 대숲 방어 명세, 계획서의 S3~S7을 읽는다. 현재 구현은 소스와 씬을 다시 읽어 확인한다. 이전 에이전트의 함수 저장소·메모리·터미널 ID는 필요하지 않다.
2. Unity 연결과 미저장 상태를 읽기 전용으로 확인한다. 현재 dirty=true인 편집 내용을 확인하기 전에는 `open_scene`, `SaveScene`, 씬 생성기, 새 씬 로드를 실행하지 않는다. 보존할 편집임이 확인되면 저장 및 사본 확보 후 Play 검증으로 진행한다.
3. S3의 `BambooDefenseDirector.TryReserveThrow`, `BambooThrower.StopThrowCycle/BeginRoutine/OnDisable`, `DefenseThrowBudget.Clear/HandleReleased`, Spawner의 사망 구독 정리를 확인한다. 이 코드는 이미 적용되어 있으므로 중복 추가하지 않는다.
4. S3 Play 검증을 수행한다. 전조 중 투척수 비활성화 시 예약 1회 반환, 발사 후 투척수 사망 시 기존 수리검 유지, 수리검 종료 알림 1회, GameOver/Director 비활성 시 새 예약 거절, 재도전 시 기록·예산·구독 초기화를 확인한다. 실제 결과와 미확인을 구분해 기록한다.
5. S2의 자연 재사용 미관찰을 다시 볼 경우 `_activeMelee`, `_idleMelee`, `_nextMeleeSpawnAt`, 플레이어 위치·생존 상태, NavMesh 샘플/완전 경로 조건을 함께 수집한다. 제어된 재사용은 성공했으므로 풀링 오류라고 가정해 코드를 바꾸지 않는다. 필요하면 Baseline의 원본 Director 산식과 비교한다.
6. S3 검증 후 S4로 진행한다. 기록·방향 Canvas를 Inspector에서 편집 가능한 HUD 프리팹으로 이전하고, 투척수의 실제 전조 시간을 이벤트로 전달한다. 런타임 생성 UI와 새 UI를 동시에 실행하지 않는다.
7. 이어 S5 공유 전투 → S6 Intro/BambooDefense 정식 빌드 → S7 근거가 있는 에셋·문서 정리 순으로 진행한다. 기존 씬·보스·공유 프리팹은 새 빌드 검증 전에 삭제하지 않는다.

### PowerShell에서 연결 확인

프로젝트 루트에서 실행한다. 아래 명령은 Git CLI를 사용하지 않는다.

```powershell
Set-Location 'C:/Users/JUNGLE/Documents/w07-blindblade-plus'
$unityCli = 'C:/Program Files/Unity Hub/resources/cli/unity.exe'
& $unityCli status --format json
& $unityCli command console_status --caller plugin --skill unity-cli --project-path 'C:/Users/JUNGLE/Documents/w07-blindblade-plus' --format json
& $unityCli command eval --caller plugin --skill unity-cli --project-path 'C:/Users/JUNGLE/Documents/w07-blindblade-plus' --format json 'return new { playing=UnityEditor.EditorApplication.isPlaying, compiling=UnityEditor.EditorApplication.isCompiling, scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, dirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty };'
```

샌드박스에서 STATUS_NO_INSTANCES가 나오면 기존에 확인한 ACL 제약 가능성을 고려한다. Unity가 없다고 가정하여 다른 Editor를 띄우거나 파일을 직접 재생성하지 않는다. 기존 사용자 승인은 유지되며 프로젝트 내부 변경 승인을 다시 묻지 않는다. 도구 실행 자체에는 해당 환경의 권한 정책이 적용된다.

반복 작업용 연결은 다음과 같다. shell 안에서는 `unity` 접두어 없이 `command ...`를 입력한다.

```powershell
& 'C:/Program Files/Unity Hub/resources/cli/unity.exe' shell --format json --no-banner
```

### 이미 수행한 이전과 주의점

- `BambooDefenseRefactoring.MigrateSpawner()`는 이미 실행하여 씬에 저장했다. `_spawner` 연결이 있으므로 반복 실행할 필요가 없다. 원본 Director의 임시 이전 필드는 22/22 비교 후 코드에서 제거했다.
- 씬 파일에는 `_spawner: {fileID: 1968806062}`와 Spawner 1개가 저장되어 있다. 기존 Director·씬의 GUID는 보존했다.
- S1 생성기는 기존 씬이 있으면 경고 후 중단한다. 메뉴 이름은 `BlindBlade/대숲 방어/없는 씬만 신규 생성`이다.
- HUD·공유 Player/애니메이션·Intro·빌드 목록은 아직 전환하지 않았다. BambooDefense는 Build Settings에서 여전히 비활성이다.
- S3의 두 동작 변경(Playing 밖 예약 거절, 활성 투척수 재시작 전 예약/구독 정리)은 컴파일 확인만 끝났다. 실행 검증 없이 완료로 승격하지 않는다.
- S7에서 이전 도구의 한 번 쓰는 메서드를 유지할 이유가 있는지도 검토한다. 새 테스트 코드나 범용 프레임워크는 추가하지 않는다.

## 인계 시점 상태

- 중단 요청: 사용자가 여기서 마무리하고 다른 터미널에서 이어갈 수 있도록 진행 문서 갱신을 요청했다. 재개 요청 전에는 후속 단계 자동 실행을 계속하지 않는다.
- 마지막 Unity 상태: Play=false, paused=false, compiling=false, BambooDefense dirty=true. 미저장 씬은 보존했다.
- 마지막 Console 상태(2026-10-14 04:12:25 UTC): compilationFailed=false, consoleErrors=0, consoleWarnings=1. 경고 1건의 원인은 분류하지 않았다. 콘솔을 지우지 않았다.
- 새 테스트 코드, 빌드, 에셋 삭제, Git 커밋·푸시·브랜치 변경은 수행하지 않았다.
- 실행 증거와 원본 사본은 아래 로컬 경로에 남아 있다. 다른 컴퓨터로 옮길 경우 이 폴더도 전달해야 한다.

| 파일 / 경로 (`UserSettings/Refactoring/BambooDefense-20261014/` 기준) | 내용 |
| --- | --- |
| `Baseline/` | 변경 전 코드·에디터·관련 씬·Defense 프리팹·Player·빌드 목록·문서 375개 사본 |
| `baseline-manifest.json` | 원본 경로·SHA256·크기 |
| `baseline-play.json` | 준비 중 최초 조회. started=false·HP100·Canvas5이며 이후 별도 조회로 실제 스폰 확인 |
| `baseline-gameover.json` | 원본 GameOver·HP0·TimeScale0 확인 |
| `s2-field-comparison.json` | 이전한 22개 값·참조 모두 일치 |
| `s2-play.json` | 생존70.484초·투척수6·근접20 및 처치0→2 |
| `s2-reuse.json` | 자연 관찰: 투척수 재사용=true, 특정 근접 재사용=false, 예약1+비행2 |
| `s2-controlled-reuse.json` | 제어된 근접 재사용=true·동일 인스턴스·HP30·돌진형·active1/idle0 |

## 실행 기록

### S3 코드 적용 및 인계

- 마지막 명령이 중단되어 실행 결과를 재확인했다. 실제 파일에서 S3 패치와 기능명세 갱신이 적용된 것을 확인했다.
- `TryReserveThrow`는 Playing 상태 및 활성 Director에서만 허용하도록 변경했다.
- `StopThrowCycle`을 추가해 OnDisable과 BeginRoutine이 같은 코루틴·예약·비행 구독 정리를 사용한다. 이미 발사한 수리검은 파괴하지 않는다.
- 인계 시 console_status에서 컴파일 실패=false·컴파일 중=false·오류0을 확인했다. 해당 S3 동작의 Play 검증은 아직 없다.
- 기존 Unity 편집 씬이 dirty=true라 저장하지 않고 유지했다. 새 코드나 다음 단계 구현은 더 진행하지 않았다.

### S1 및 S2 이전

- S1 컴파일 오류 0. 생성기 Build 호출 전후 기존 씬 bytes 동일, dirty=false 확인.
- S2 신규 코드 컴파일 오류 0. 이전 중 임시 필드의 미사용 경고는 제거 후 재확인한다.
- `BambooDefenseRefactoring.MigrateSpawner()`로 Spawner 1개 생성·저장.
- `s2-field-comparison.json`: 원본 Director → 새 Spawner의 22개 값·참조 22/22 일치. 경로: `UserSettings/Refactoring/BambooDefense-20261014/`.
- 최종 S2 컴파일 오류0, 임시 미사용 필드 경고 제거.
- 자연 진행 생존70.484초에서 투척수6·근접20, 두 적에 피해를 전달해 처치0→2. 진단용 HP100000은 Play에서만 적용했고 저장하지 않았다.
- 생존163.646초 관찰: 투척수 재사용=true, 예약1+비행2=상한3. 근접 재사용=false 관찰은 완료 결과로 숨기지 않는다.
- 새 Play의 도달 가능한 위치에서 스폰 시각·종류 확률만 임시 제어한 확인: 동일 근접 인스턴스 재사용=true, HP30, charge=true, active1/idle0. `s2-controlled-reuse.json` 참조. 자연 진행의 미재사용은 경로/스폰 조건 추가 관찰 대상으로 남긴다. 스폰 알고리즘은 원본과 동일하게 유지했다.

### 2026-10-14 / S0 시작

- AGENTS.md, FEATURE_SPEC.md, 계획과 Unity CLI/UI 스킬을 확인했다.
- Unity live 연결을 확인했다. 아직 씬·프리팹·코드를 변경하지 않았다.
- 다음 검증: 활성 씬과 미저장 변경, 컴파일, 방어 게임 기준 Play.

## 미확인과 차단

- 실제 키보드·마우스·패드 조작과 화면 육안 확인 미실행.
- 빌드 미실행. 기준 Play: Playing, 생존 13.249초, 투척수 3·근접 적 3, HP20. 이후 GameOver·HP0·TimeScale0 확인 후 Restart API 실행. 재시작 세션에서도 생존 14.545초 뒤 게임오버 확인. Play 종료 후 dirty=false.
- 기준 결과: `UserSettings/Refactoring/BambooDefense-20261014/baseline-play.json`, `baseline-gameover.json`. 최초 배경 비활성 상태라 실행 중 Application.runInBackground=true를 설정했다. 프로젝트 설정은 변경하지 않았다.
- 승인된 제품 질문은 재질문하지 않는다. 새 충돌이 있으면 해당 작업과 근거를 남긴다.

## 다음 작업자에게 전달할 시작 문장

> `docs/plans/BambooDefense_refactoring_progress.md`를 먼저 읽고 이어서 작업해 주세요. S1과 S2 값 이전은 끝났고 S3 코드는 컴파일 오류 없이 적용됐지만 Play 검증 전입니다. Unity에 미저장 BambooDefense 씬이 있으므로 먼저 편집 내용을 확인해 보존하고, S3 검증부터 재개한 뒤 S4~S7을 순서대로 진행해 주세요. 프로젝트 내부 수정과 계획의 권장안은 이미 승인되었습니다. 현재 refactoring 브랜치에서는 Git CLI·커밋을 하지 않습니다.
