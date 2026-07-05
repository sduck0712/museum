using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Input;

namespace VirtualMuseum.Vault
{
    /// <summary>
    /// "내 유물함" 화면. 탐험 중 V키로 열고 V/ESC로 닫는다.
    /// 열려 있는 동안 InputModeManager를 InfoPanel 모드로 전환해
    /// 이동/시야/상호작용 입력을 잠그고 커서를 활성화한다.
    /// (씬 배선 없이 동작하도록 IMGUI로 구현 — 프로덕션에서는 uGUI로 교체 지점)
    /// </summary>
    public class VaultUIManager : MonoBehaviour
    {
        /// <summary>StudentInteractionManager가 InfoPanel 모드 입력 처리와 충돌하지 않도록 공개하는 플래그.</summary>
        public static bool IsOpen { get; private set; }

        private Vector2 _scroll;

        private void Update()
        {
            var modeManager = InputModeManager.Instance;
            if (modeManager == null) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.V))
            {
                if (IsOpen)
                {
                    Close();
                }
                else if (modeManager.CurrentMode == InteractionMode.FPSExploration ||
                         modeManager.CurrentMode == InteractionMode.HandTrackingExploration)
                {
                    Open();
                }
            }
            else if (IsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Open()
        {
            IsOpen = true;
            InputModeManager.Instance.EnterInfoPanel();
        }

        private void Close()
        {
            IsOpen = false;
            InputModeManager.Instance.ReturnToExploration();
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            const float w = 520f, h = 460f;
            Rect area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUILayout.BeginArea(area, GUI.skin.box);

            var titleStyle = new GUIStyle(GUI.skin.label)
            { richText = true, fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var entryStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 };

            var vault = VaultManager.Instance;
            GUILayout.Label($"<b>내 유물함 (My Vault)</b>  {vault.CollectedArtifacts.Count} / 5", titleStyle);
            GUILayout.Space(8);

            if (vault.CollectedArtifacts.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("아직 발굴한 유물이 없습니다.\n좌대에 다가가 [E]로 발굴을 시작해 보세요!",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 });
                GUILayout.FlexibleSpace();
            }
            else
            {
                _scroll = GUILayout.BeginScrollView(_scroll);
                foreach (var entry in vault.CollectedArtifacts)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    string badge = entry.IsRestored
                        ? "  <color=#ff9955>[복원됨 - 등급 페널티]</color>"
                        : "  <color=#77dd77>[온전한 발굴]</color>";
                    GUILayout.Label($"<b>{entry.Artifact.artifactName}</b>{badge}", entryStyle);
                    GUILayout.Label(
                        $"<color={RarityColorHex(entry.Artifact.rarityTier)}>희귀도: {entry.Artifact.rarityTier}</color>" +
                        $"   발굴 난이도: {entry.Artifact.excavationDifficulty}", entryStyle);
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(6);
            GUILayout.Label("V 또는 ESC: 닫기",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            GUILayout.EndArea();
        }

        private static string RarityColorHex(RarityTier tier) => tier switch
        {
            RarityTier.Legendary => "#ffb347",
            RarityTier.Rare => "#7ec8ff",
            RarityTier.Uncommon => "#9be89b",
            _ => "#cccccc"
        };

        private void OnDestroy()
        {
            if (IsOpen) IsOpen = false;
        }
    }
}
