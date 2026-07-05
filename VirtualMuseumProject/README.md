# Virtual Museum Platform - 1차 구현 코드

`virtual_museum_spec_v2.md` 사양서를 기반으로 한 1차(First-pass) Unity C# 구현입니다.
로컬 JSON 백엔드 + 웹캠 MediaPipe(목업 포함) 핸드트래킹 + 발굴/재조립 퍼즐 미니게임을 포함합니다.

## 이 코드로 바로 로컬 데모가 되나요? → 예 (핵심 질문 답변)

**됩니다.** 아래 3가지 설계 덕분에 외부 서버·VR 기기·웹캠 없이도 씬 구성만 마치면 바로 플레이 가능합니다.

1. **백엔드**: `LocalJsonBackendService`가 `Application.persistentDataPath`의 로컬 JSON 파일만 사용합니다. 인터넷 연결이나 Firebase/AWS 계정이 전혀 필요 없습니다.
2. **핸드트래킹**: `HandTrackingManager`는 기본값(`useMockProvider = true`)이 마우스로 손 동작을 시뮬레이션하는 `MockHandLandmarkProvider`입니다. 즉 F키를 눌러도 실제 웹캠/MediaPipe 플러그인 없이 마우스로 "핀치=클릭, 검지 위치=마우스 위치"로 전체 흐름(그랩, 발굴 브러시, 재조립 퍼즐)을 그대로 테스트할 수 있습니다. 실제 웹캠 인식은 `useMockProvider = false` + MediaPipe Unity Plugin 설치 후 활성화하면 됩니다.
3. **더미 데이터**: 최초 실행 시 `StreamingAssets/DummyData/museum_data_seed.json`(루브르 유물 5종)이 자동으로 로컬 저장 위치에 복사되어, 별도 데이터 입력 없이 바로 학생 씬에 유물이 스폰됩니다.

즉 **Unity Editor Play 모드 또는 Windows/Mac 단독 빌드만으로 완전한 데모가 됩니다.**

## 폴더 구조

```
Assets/
 ├ Scripts/
 │   ├ Data/            ArtifactData, PlacementData, enum 등 데이터 모델
 │   ├ Backend/          IBackendService, LocalJsonBackendService (1단계 로컬 목업)
 │   ├ Core/             ServiceLocator (백엔드 구현체 교체 지점)
 │   ├ Auth/             AuthController, LoginUIManager, GameSessionData
 │   ├ TriLibIntegration/AdminStlPreviewLoader (관리자 STL 프리뷰)
 │   ├ AdminConsole/     ArtifactDragHandle, PedestalDropSlot (탑다운 배치 에디터)
 │   ├ Input/            InputModeManager, IPointerInputSource(마우스/핸드트래킹 추상화)
 │   ├ HandTracking/     HandTrackingManager, MockHandLandmarkProvider, MediaPipeHandLandmarkProvider
 │   ├ Student/          StudentArtifactSpawner, StudentInteractionManager, PedestalProximityTrigger
 │   ├ Excavation/       ExcavationBrush, ExcavationSessionController, ArtifactDurabilityController, ExcavationToolLibrary
 │   ├ Puzzle/           ArtifactShard, ShardDragController, ReassemblyPuzzleManager (파손 후 재조립)
 │   └ Vault/            VaultManager ("내 유물함")
 ├ Shaders/
 │   ├ BrushBlit.shader          발굴 브러시가 마스크에 그리는 Blit 셰이더
 │   └ ExcavationSurface.shader  흙/유물 블렌딩 Interactive Masking Shader (Shader Graph 노드 설명 포함)
 └ StreamingAssets/DummyData/museum_data_seed.json   루브르 유물 5종 초기 데이터
```

## Unity 프로젝트에 넣을 때 준비할 것

1. **필수 에셋**: TriLib 2 (Asset Store), TextMeshPro(Package Manager에서 Import TMP Essentials).
2. **씬 구성** (직접 만들어야 함, 코드는 스크립트 부착 대상 GameObject를 전제로 작성됨):
   - `Login` 씬: `LoginUIManager`를 붙인 Canvas + 학생입장/운영자탭 UI.
   - `AdminConsole` 씬: `AdminStlPreviewLoader`, `PedestalDropSlot`(좌대 개수만큼), `ArtifactDragHandle`(유물 아이콘).
   - `StudentPerspective` 씬: FPS 컨트롤러(플레이어 태그 "Player" 필수) + `StudentArtifactSpawner`, `StudentInteractionManager`, `InputModeManager`, `HandTrackingManager`, 발굴 오버레이(`ExcavationSessionController`, `ExcavationBrush`, `ReassemblyPuzzleManager`, `ShardDragController`).
3. **프리팹**: `pedestalPrefab`(좌대 + `PedestalProximityTrigger` 포함), `shardPrefab`(Collider + MeshRenderer + `ArtifactShard`)은 Unity Editor에서 직접 제작 후 인스펙터에 연결해야 합니다 (바이너리 프리팹 에셋은 텍스트로 생성할 수 없어 별도 제작 필요).
4. **머티리얼**: `ExcavationSurface` 셰이더를 사용하는 머티리얼을 만들어 `_ArtifactAlbedo`/`_DirtAlbedo`/`_DirtNormal` 텍스처를 채워 넣고, `StudentArtifactSpawner`의 `excavationDirtMaterialTemplate`에 연결하세요. `_DirtMaskTex`는 `ExcavationBrush`가 런타임에 자동 주입합니다.
5. **레이어**: 재조립 퍼즐 조각용 Layer(예: "Shard")를 만들어 `ShardDragController.shardLayerMask`에 지정하세요.

## 다음 단계로 넘어갈 때 고려할 부분

- **MediaPipe 실 연동**: `MediaPipeHandLandmarkProvider`에 실제 플러그인의 웹캠 프레임 전달/콜백 연결 필요 (플러그인 버전마다 API가 다름).
- **파손 메시**: 현재는 조각을 원형으로 절차적 배치합니다. 실제 서비스에서는 아티스트가 제작한 fracture mesh + 각 조각의 실제 정답 위치 데이터를 `ArtifactData`에 추가하는 것을 권장합니다.
- **클라우드 전환**: `IBackendService`를 구현하는 `FirebaseBackendService`/`AwsBackendService`를 작성해 `ServiceLocator.Register()`에 등록하면 됩니다.
