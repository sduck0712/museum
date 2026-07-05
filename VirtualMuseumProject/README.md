# Virtual Museum Platform — 로컬 데모 (v2 검수/고도화 버전)

`virtual_museum_spec_v2` 사양서 기반 Unity 프로젝트입니다.
Sonnet 1차 구현 코드를 전면 검수해 **치명적 버그를 수정**하고, **씬 자동 생성 도구 / 자체 STL 로더 / FPS 컨트롤러**를 추가해
외부 유료 에셋(TriLib)·웹캠·클라우드 없이 **Unity만으로 즉시 플레이 가능한 로컬 데모**로 고도화했습니다.

---

## 빠른 시작 (3단계)

1. **Unity 2021.3 LTS 이상(2022.3 LTS 권장)** 으로 `VirtualMuseumProject` 폴더를 프로젝트로 엽니다
   (Unity Hub → Add project from disk). 추가 패키지 설치 불필요 — 전부 내장 모듈만 사용합니다.
2. 메뉴 **`Tools > Virtual Museum > Build Demo Scenes`** 를 실행합니다.
   → 씬 3개(Login/AdminConsole/StudentPerspective), 프리팹, 머티리얼, 텍스처가 자동 생성·배선되고 Login 씬이 열립니다.
3. **Play** 를 누릅니다.

| 계정 | 값 |
|---|---|
| 학생 | 이름만 입력(또는 빈칸 = Guest) |
| 운영자 | `admin` / `admin1234` |

로컬 데이터 초기화: `Tools > Virtual Museum > Reset Local Museum Data`

## 조작법

| 키 | 동작 |
|---|---|
| WASD / Shift / 마우스 | 이동 / 달리기 / 시야 |
| Q | 유물 정보 패널 (Q/ESC로 닫기) |
| E | 발굴 미니게임 진입 |
| F | 핸드트래킹 토글 (기본: 마우스 시뮬레이션 Mock — 마우스 위치=검지, 좌클릭=핀치) |
| 1~4 | 발굴 도구 선택 (에어블로워/브러시/조각칼/피크) |
| 마우스 휠 | 발굴 중 줌 (줌에 따라 브러시 반경 자동 보정) |
| ESC | 발굴 종료 |

**발굴 규칙**: 드래그가 안전 속도를 초과하면(도구의 breakRisk × 초과속도) 내구도가 깎입니다.
내구도 0 → 파손 → 조각을 고스트 실루엣 위치로 드래그해 재조립 → 복원 흔적(틴트)이 남은 채 발굴 재개 → 100% 클리어 시 "내 유물함" 등록.

## 1차 코드 대비 주요 수정 사항

**컴파일/구조 (데모 블로커)**
- `using TriLibCore` 하드 의존 제거 → **자체 STL 파서(`SimpleStlLoader`) 내장**, TriLib은 설치 후 Scripting Define `TRILIB` 추가 시 선택적으로 활성화 (`ArtifactModelLoader`)
- 씬/프리팹/FPS 컨트롤러 부재 → `DemoSceneBuilder`(에디터 메뉴)로 전부 자동 생성, `SimpleFPSController` 신규 작성
- seed 데이터의 빈 `stlFileURL`로 유물 5종 전부 로드 실패 → **절차적 플레이스홀더 메시 폴백** 추가
- `ExcavationBrush`가 인스펙터 고정 renderer 요구 → 런타임 `SetTarget()` 주입 구조로 재설계 (유물별 마스크 RT 캐싱, 세션 재진입 시 진행률 유지)

**게임플레이 버그**
- 정보 패널/발굴 오버레이를 **닫는 코드 경로가 없던 문제** 수정 (Q/ESC)
- 재조립 퍼즐 클리어 불가 수정: 매 프레임 자동 스냅 제거(놓는 순간만 판정) + 드래그 중 회전 정렬 보조 + 고스트 실루엣 힌트(`showGhostSilhouette`)
- 내구도 로직 역전 수정: 데미지가 `maxDurability`에 비례하던 것을 제거하고, **안전 속도 초과분만 데미지**로 계산 (난이도별 임계치 차등)
- 도구 수치 스펙 정합: 조각칼=중간, 소형 삽/피크=높음 (기존 코드와 반대였음)
- 발굴 진행률이 마스크 좌하단 32×32 "구석"만 읽던 버그 → 다운샘플 Blit 후 리드백으로 수정
- 파손 시 유물 메시가 그대로 보이던 문제 → 파손 중 숨김, 복원 후 균열 틴트(`_Tint`) 영구 표시
- 발굴 완료 유물에 E 입력 시 정보 패널로 우회, 유물함 중복 등록 방지

**안정성/기타**
- `Task.Run` 안에서 `JsonUtility` 호출하던 것을 "파일 I/O만 백그라운드, 직렬화는 메인 스레드" 구조로 정리 + 세마포어로 읽기-수정-쓰기 직렬화
- 로그인 Error 상태에서 운영자 패널이 사라져 재시도 불가하던 UI 상태 머신 수정, 인증 중 버튼 비활성화, 예외 처리
- 핸드트래킹: 손 미검출 시 핀치 상태 잔존 수정, 웹캠 미검출 시 마우스 모드 강제 유지(스펙 7.2), 활성 시 우측 하단 상태/포인터 오버레이, `WebCamTexture` 해제
- 한글 렌더링: TMP 기본 폰트는 한글 글리프가 없어 uGUI 레거시 Text(OS 다이나믹 폰트) 사용
- `FindObjectsOfType` 버전 호환 래퍼(`SceneQuery`), RenderTexture/Texture2D/Material 해제 일괄 점검

## 폴더 구조

```
Assets/
 ├ Scripts/
 │   ├ Data/            ArtifactData, PlacementData, enum, 런타임 상태
 │   ├ Backend/         IBackendService, LocalJsonBackendService (1단계 로컬 목업)
 │   ├ Core/            ServiceLocator(백엔드 교체 지점), SceneQuery, BillboardLabel
 │   ├ Auth/            AuthController, LoginUIManager, GameSessionData
 │   ├ Input/           InputModeManager, IPointerInputSource (마우스/핸드트래킹 추상화)
 │   ├ HandTracking/    HandTrackingManager, Mock/MediaPipe 프로바이더
 │   ├ ModelLoading/    SimpleStlLoader(내장 STL 파서), ArtifactModelLoader(TriLib 선택적)
 │   ├ AdminConsole/    AdminConsoleHUD, AdminStlPreviewLoader, ArtifactDragHandle, PedestalDropSlot
 │   ├ Student/         SimpleFPSController, StudentArtifactSpawner, StudentInteractionManager,
 │   │                  PedestalProximityTrigger, InfoPanelView
 │   ├ Excavation/      ExcavationBrush, ExcavationSessionController, ArtifactDurabilityController, 도구 테이블
 │   ├ Puzzle/          ArtifactShard, ShardDragController, ReassemblyPuzzleManager
 │   ├ Vault/           VaultManager ("내 유물함")
 │   └ Editor/          DemoSceneBuilder (씬/프리팹/머티리얼 자동 생성)
 ├ Shaders/             BrushBlit(마스크 페인팅), ExcavationSurface(Interactive Masking + 복원 틴트)
 └ StreamingAssets/DummyData/museum_data_seed.json   루브르 유물 5종 (최초 실행 시 자동 복사)
```

로컬 데이터 위치: `Application.persistentDataPath` (`museum_data.json`, `admin_accounts.json`(SHA-256 해시), `StlFiles/`)

## 관리자 콘솔 사용법

- 파란 토큰을 좌대 슬롯 위로 드래그 → 배치(PlacementData) 자동 저장 → 학생 씬에 반영
- 좌측 HUD: 유물 선택 → 내구도/권장도구/난이도 편집 → 저장
- STL 등록: 로컬 `.stl` 파일 경로 입력 → [프리뷰 로드]로 확인 → [이 유물에 STL 등록 + 저장]
  → 파일이 persistentDataPath로 "업로드"되고 학생 씬에서 해당 모델이 발굴 대상으로 스폰됨

## 다음 단계 (실서비스 고도화 포인트)

- **MediaPipe 실 연동**: `MediaPipeHandLandmarkProvider`의 TODO 지점에 플러그인 콜백 연결
  (좌우반전 보정: `viewportX = 1 - mediaPipeX`), `HandTrackingManager.useMockProvider = false`
- **TriLib 2**: 설치 후 Player Settings → Scripting Define Symbols에 `TRILIB` 추가 시 OBJ/FBX 등 확장 포맷 지원
- **파손 메시**: 현재 절차적 큐브 조각 → 아티스트 제작 fracture mesh + 조각별 정답 위치 데이터로 교체 권장
- **탐험 모드 핸드트래킹 그랩**(스펙 7.3 유물 집기/회전)은 미구현 — `StudentInteractionManager`에 Kinematic Rigidbody + Slerp 추가 지점 주석 참고
- **클라우드 전환**: `IBackendService` 구현체(`FirebaseBackendService` 등) 작성 후 `ServiceLocator.Register()` 한 줄 교체
- **지층(Soil Layer) 박스/발굴 소품** 등 아트 연출(스펙 6절)은 데모에서는 유물 표면 흙 마스킹으로 대체
