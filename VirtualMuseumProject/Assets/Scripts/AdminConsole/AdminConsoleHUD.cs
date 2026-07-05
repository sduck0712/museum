using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VirtualMuseum.Auth;
using VirtualMuseum.Core;
using VirtualMuseum.Data;

namespace VirtualMuseum.AdminConsole
{
    /// <summary>
    /// 관리자 콘솔의 데모용 IMGUI HUD.
    /// - 유물 선택 / 메타데이터(내구도·권장도구·난이도) 편집
    /// - 로컬 STL 파일 경로 입력 → 프리뷰 로드 → 백엔드 "업로드"(persistentDataPath 복사) 후 등록
    /// - 학생 씬 미리보기 / 로그아웃
    /// 프로덕션에서는 uGUI 폼으로 교체하면 되고, 백엔드 호출부는 그대로 재사용된다.
    /// </summary>
    public class AdminConsoleHUD : MonoBehaviour
    {
        [SerializeField] private AdminStlPreviewLoader previewLoader;
        [SerializeField] private string loginSceneName = "Login";
        [SerializeField] private string studentSceneName = "StudentPerspective";

        private List<ArtifactData> _artifacts = new();
        private int _selectedIndex;
        private string _stlPathInput = "";
        private string _statusMessage = "유물 데이터 로드 중...";
        private bool _busy;

        private async void Start()
        {
            if (previewLoader == null) previewLoader = SceneQuery.Find<AdminStlPreviewLoader>();

            try
            {
                _artifacts = await ServiceLocator.CurrentBackend.FetchMuseumLayoutAsync();
                _statusMessage = $"유물 {_artifacts.Count}종 로드 완료. 토큰을 좌대로 드래그해 배치를 바꿀 수 있습니다.";
            }
            catch (Exception e)
            {
                _statusMessage = "유물 데이터 로드 실패 (콘솔 로그 확인)";
                Debug.LogError($"[AdminConsoleHUD] {e}");
            }
        }

        private ArtifactData Selected =>
            (_artifacts.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _artifacts.Count)
                ? _artifacts[_selectedIndex] : null;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 400, 330), GUI.skin.box);
            GUILayout.Label($"<b>관리자 콘솔</b>  (로그인: {GameSessionData.Instance.DisplayName})",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

            var artifact = Selected;
            if (artifact != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", GUILayout.Width(30)))
                    _selectedIndex = (_selectedIndex - 1 + _artifacts.Count) % _artifacts.Count;
                GUILayout.Label($"{artifact.artifactID} - {artifact.artifactName}",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("▶", GUILayout.Width(30)))
                    _selectedIndex = (_selectedIndex + 1) % _artifacts.Count;
                GUILayout.EndHorizontal();

                // 발굴 메타데이터 편집 (스펙 3절: maxDurability / recommendedToolType / difficulty)
                GUILayout.BeginHorizontal();
                GUILayout.Label($"최대 내구도: {artifact.maxDurability}", GUILayout.Width(140));
                artifact.maxDurability = (int)GUILayout.HorizontalSlider(artifact.maxDurability, 10, 100);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("권장 도구:", GUILayout.Width(80));
                if (GUILayout.Button(artifact.recommendedToolType.ToString()))
                    artifact.recommendedToolType = (ExcavationToolType)(((int)artifact.recommendedToolType + 1) % 4);
                GUILayout.Label("난이도:", GUILayout.Width(60));
                if (GUILayout.Button(artifact.excavationDifficulty.ToString()))
                    artifact.excavationDifficulty = (ExcavationDifficulty)(((int)artifact.excavationDifficulty + 1) % 3);
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                GUILayout.Label("STL 파일 경로 (로컬):");
                _stlPathInput = GUILayout.TextField(_stlPathInput);

                GUI.enabled = !_busy;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("프리뷰 로드")) _ = LoadPreviewAsync();
                if (GUILayout.Button("이 유물에 STL 등록 + 저장")) _ = RegisterStlAndSaveAsync();
                GUILayout.EndHorizontal();

                if (GUILayout.Button("메타데이터만 저장")) _ = SaveSelectedAsync();
                GUI.enabled = true;
            }

            GUILayout.Space(6);
            GUILayout.Label(_statusMessage, new GUIStyle(GUI.skin.label) { wordWrap = true });

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("학생 씬 미리보기")) SceneManager.LoadScene(studentSceneName);
            if (GUILayout.Button("로그아웃"))
            {
                GameSessionData.Instance.ClearSession();
                SceneManager.LoadScene(loginSceneName);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private async System.Threading.Tasks.Task LoadPreviewAsync()
        {
            if (previewLoader == null) { _statusMessage = "PreviewLoader가 없습니다."; return; }

            _busy = true;
            _statusMessage = "프리뷰 로드 중...";
            try
            {
                bool ok = await previewLoader.LoadPreviewAsync(_stlPathInput.Trim().Trim('"'));
                _statusMessage = ok ? "프리뷰 로드 완료" : "프리뷰 로드 실패 - 경로/포맷을 확인하세요.";
            }
            catch (Exception e)
            {
                _statusMessage = $"프리뷰 로드 오류: {e.Message}";
            }
            finally { _busy = false; }
        }

        private async System.Threading.Tasks.Task RegisterStlAndSaveAsync()
        {
            var artifact = Selected;
            if (artifact == null) return;

            _busy = true;
            _statusMessage = "STL 업로드 중...";
            try
            {
                string storedPath = await ServiceLocator.CurrentBackend
                    .UploadArtifactFileAsync(_stlPathInput.Trim().Trim('"'), artifact.artifactID);
                artifact.stlFileURL = storedPath;
                await ServiceLocator.CurrentBackend.SaveArtifactDataAsync(artifact);
                _statusMessage = $"등록 완료: {artifact.artifactID} ← {storedPath}";
            }
            catch (Exception e)
            {
                _statusMessage = $"등록 실패: {e.Message}";
            }
            finally { _busy = false; }
        }

        private async System.Threading.Tasks.Task SaveSelectedAsync()
        {
            var artifact = Selected;
            if (artifact == null) return;

            _busy = true;
            try
            {
                await ServiceLocator.CurrentBackend.SaveArtifactDataAsync(artifact);
                _statusMessage = $"{artifact.artifactID} 저장 완료";
            }
            catch (Exception e)
            {
                _statusMessage = $"저장 실패: {e.Message}";
            }
            finally { _busy = false; }
        }
    }
}
