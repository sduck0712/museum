# MediaPipe 핸드트래킹 실연동 가이드

데모는 기본적으로 **Mock 프로바이더**(마우스 위치=검지, 좌클릭=핀치)로 동작하므로 이 문서는
실제 웹캠 손 인식으로 전환할 때만 필요합니다.

## 아키텍처 요약

```
웹캠(WebCamTexture)
   → MediaPipeTasksHandProvider (MEDIAPIPE 심볼 시 활성)
       - LIVE_STREAM 모드 HandLandmarker에 매 프레임 전달
       - 결과 콜백에서 21개 랜드마크 → HandLandmarkResult 변환 (x 좌우반전 보정)
   → HandTrackingManager (핀치 판정: landmark 4↔8 거리 ≤ pinchThreshold)
   → HandTrackingInputSource (IPointerInputSource)
   → ExcavationBrush / ShardDragController / StudentInteractionManager
```

게임 로직은 전부 `IPointerInputSource`만 참조하므로 **프로바이더 교체 시 코드 수정이 전혀 필요 없습니다.**

## 설치 절차

1. **플러그인 설치** — [homuler/MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin)
   - 권장: Releases에서 `MediaPipeUnity.*.unitypackage` 다운로드 후 임포트
   - 또는 UPM: `https://github.com/homuler/MediaPipeUnityPlugin.git?path=Packages/com.github.homuler.mediapipe`
2. **모델 배치** — [hand_landmarker.task](https://developers.google.com/mediapipe/solutions/vision/hand_landmarker#models) 다운로드
   → `Assets/StreamingAssets/mediapipe/hand_landmarker.task`
3. **심볼 정의** — Player Settings → Other Settings → Scripting Define Symbols에 `MEDIAPIPE` 추가
4. **매니저 설정** — StudentPerspective 씬의 `Systems` 오브젝트 → `HandTrackingManager` → `useMockProvider` 체크 해제
5. Play 후 **F키**로 활성화 → 우측 하단에 "Webcam + MediaPipe Tasks" 라벨과 포인터가 표시되면 성공

## 버전별 체크리스트 (컴파일 오류 시)

`MediaPipeTasksHandProvider.cs`는 플러그인 v0.14+ Tasks API 기준입니다. 버전에 따라 아래를 조정하세요.

| 항목 | 확인할 것 |
|---|---|
| 네임스페이스 | `Mediapipe.Tasks.Vision.HandLandmarker` 존재 여부 (구버전은 Solution 기반 API) |
| `Image` 생성자 | 버전에 따라 `Image(format, w, h, widthStep, byte[])` 대신 Texture 입력 유틸(`TextureFrame`) 제공 — 플러그인의 공식 샘플 참조 |
| 결과 콜백 시그니처 | `(HandLandmarkerResult, Image, long)` 파라미터 순서/타입 |
| 랜드마크 접근 | `result.handLandmarks[0].landmarks[i].x` 경로 (버전에 따라 `HandLandmarks` 대문자) |

## 좌표 보정 규칙 (스펙 7.1)

- **좌우반전**: 웹캠은 거울상 → `viewportX = 1 - mediapipeX`
- **y축**: MediaPipe는 위=0, Unity 뷰포트는 아래=0 → `viewportY = 1 - mediapipeY`
- **핀치 임계값**: `HandTrackingManager.pinchThreshold` (기본 0.05, 정규화 거리) — 카메라 화각/거리에 따라 조정

## 웹캠 권한/미검출 처리 (스펙 7.2)

`StartCapture()`가 false를 반환하면(디바이스 없음, 모델 없음, 초기화 실패)
`InputModeManager`가 자동으로 마우스 모드를 유지하고 경고 로그를 남깁니다 — 별도 처리 불필요.

## 확장 아이디어

- 두 손 인식(numHands: 2) 후 두 검지 거리로 핀치 인/아웃 줌 → `ExcavationSessionController`의 FOV 줌에 연결
- 손바닥 방향(landmark 0→9 벡터)으로 유물 회전 제스처 → 탐험 모드 그랩(스펙 7.3) 구현 시 활용
