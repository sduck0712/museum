#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VirtualMuseum.AdminConsole;
using VirtualMuseum.Auth;
using VirtualMuseum.Core;
using VirtualMuseum.Excavation;
using VirtualMuseum.HandTracking;
using VirtualMuseum.Input;
using VirtualMuseum.Puzzle;
using VirtualMuseum.Student;

namespace VirtualMuseum.EditorTools
{
    /// <summary>
    /// 데모용 씬/프리팹/머티리얼/텍스처를 코드로 일괄 생성하는 에디터 도구.
    /// 메뉴: Tools > Virtual Museum > Build Demo Scenes
    /// 실행 후 Assets/Generated/Scenes/Login.unity 를 열고 Play하면 전체 플로우가 동작한다.
    /// (씬 에셋은 바이너리/YAML이라 저장소에 직접 담는 대신 생성 스크립트로 제공)
    /// </summary>
    public static class DemoSceneBuilder
    {
        private const string GenRoot = "Assets/Generated";
        private const string ScenesDir = GenRoot + "/Scenes";
        private const string PrefabsDir = GenRoot + "/Prefabs";
        private const string MaterialsDir = GenRoot + "/Materials";
        private const string TexturesDir = GenRoot + "/Textures";
        private const string ShardLayerName = "Shard";

        private static Font _uiFont;

        [MenuItem("Tools/Virtual Museum/Build Demo Scenes")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();
            int shardLayer = EnsureLayer(ShardLayerName);

            // 1) 텍스처/머티리얼
            var brushTex = CreateBrushTexture();
            var excavationMat = CreateExcavationMaterial();
            var pedestalMat = CreateSimpleMaterial("PedestalMat", new Color(0.55f, 0.55f, 0.6f));
            var floorMat = CreateSimpleMaterial("FloorMat", new Color(0.35f, 0.33f, 0.3f));
            var wallMat = CreateSimpleMaterial("WallMat", new Color(0.62f, 0.58f, 0.52f));
            var shardMat = CreateSimpleMaterial("ShardMat", new Color(0.72f, 0.5f, 0.38f));
            var previewMat = CreateSimpleMaterial("PreviewMat", new Color(0.85f, 0.83f, 0.78f));
            var tokenMat = CreateSimpleMaterial("TokenMat", new Color(0.32f, 0.5f, 0.75f));
            var slotMat = CreateSimpleMaterial("SlotMat", new Color(0.45f, 0.42f, 0.5f));
            var woodMat = CreateSimpleMaterial("WoodMat", new Color(0.4f, 0.28f, 0.18f));
            var metalMat = CreateSimpleMaterial("MetalMat", new Color(0.6f, 0.6f, 0.65f));
            var soilTopMat = CreateTexturedMaterial("SoilTopMat", CreateSoilGridTexture());
            var soilSideMat = CreateTexturedMaterial("SoilSideMat", CreateSoilLayerTexture());

            // 2) 프리팹
            var pedestalPrefab = CreatePedestalPrefab(pedestalMat, soilTopMat, soilSideMat, woodMat, metalMat);
            var shardPrefab = CreateShardPrefab(shardMat, shardLayer);

            // 3) 씬
            BuildLoginScene();
            BuildAdminScene(previewMat, tokenMat, slotMat);
            BuildStudentScene(pedestalPrefab, shardPrefab, excavationMat, floorMat, wallMat, brushTex, shardLayer);

            // 4) 빌드 세팅 등록
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenesDir + "/Login.unity", true),
                new EditorBuildSettingsScene(ScenesDir + "/AdminConsole.unity", true),
                new EditorBuildSettingsScene(ScenesDir + "/StudentPerspective.unity", true),
            };

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenesDir + "/Login.unity");

            EditorUtility.DisplayDialog("Virtual Museum",
                "데모 씬 생성 완료!\n\nLogin 씬이 열렸습니다. Play 버튼을 눌러 시작하세요.\n" +
                "- 학생 입장: 이름 입력 후 입장\n- 운영자: admin / admin1234", "확인");
        }

        [MenuItem("Tools/Virtual Museum/Reset Local Museum Data")]
        public static void ResetLocalData()
        {
            string dataPath = Path.Combine(Application.persistentDataPath, "museum_data.json");
            string adminPath = Path.Combine(Application.persistentDataPath, "admin_accounts.json");
            string stlDir = Path.Combine(Application.persistentDataPath, "StlFiles");
            if (File.Exists(dataPath)) File.Delete(dataPath);
            if (File.Exists(adminPath)) File.Delete(adminPath);
            if (Directory.Exists(stlDir)) Directory.Delete(stlDir, recursive: true);
            Debug.Log("[DemoSceneBuilder] 로컬 데이터 초기화 완료. 다음 실행 시 seed 데이터/샘플 STL이 다시 복사됩니다.");
        }

        // ---------------------------------------------------------------- 공통 유틸

        private static Font UIFont
        {
            get
            {
                if (_uiFont != null) return _uiFont;
                try { _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_uiFont == null)
                {
                    try { _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                }
                return _uiFont;
            }
        }

        private static void EnsureFolders()
        {
            foreach (string dir in new[] { GenRoot, ScenesDir, PrefabsDir, MaterialsDir, TexturesDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
                }
            }
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning("[DemoSceneBuilder] 빈 레이어 슬롯이 없어 Shard 레이어를 추가하지 못했습니다. (기본 레이어로 동작)");
            return 0;
        }

        private static void SetRef(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[DemoSceneBuilder] 필드를 찾을 수 없음: {target.GetType().Name}.{fieldName}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogError($"[DemoSceneBuilder] 필드 없음: {fieldName}"); return; }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string fieldName, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogError($"[DemoSceneBuilder] 필드 없음: {fieldName}"); return; }
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string fieldName, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogError($"[DemoSceneBuilder] 필드 없음: {fieldName}"); return; }
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T ReplaceAsset<T>(T asset, string path) where T : Object
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // ---------------------------------------------------------------- 텍스처 / 머티리얼

        private static Texture2D CreateBrushTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            return ReplaceAsset(tex, TexturesDir + "/SoftBrush.asset");
        }

        private static Texture2D CreateNoiseTexture(string name, Color dark, Color light, float scale)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            float ox = Random.Range(0f, 100f), oy = Random.Range(0f, 100f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(ox + x * scale, oy + y * scale);
                    n = Mathf.Lerp(n, Mathf.PerlinNoise(ox + x * scale * 4f, oy + y * scale * 4f), 0.35f);
                    tex.SetPixel(x, y, Color.Lerp(dark, light, n));
                }
            tex.Apply();
            return ReplaceAsset(tex, $"{TexturesDir}/{name}.asset");
        }

        private static Texture2D CreateFlatNormalTexture()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            // UnpackNormal의 RG/AG 언패킹 모두에서 (0,0,1)이 나오도록 x를 r과 a에 함께 저장
            var flat = new Color(0.5f, 0.5f, 1f, 0.5f);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, flat);
            tex.Apply();
            return ReplaceAsset(tex, TexturesDir + "/FlatNormal.asset");
        }

        private static Material CreateExcavationMaterial()
        {
            var shader = Shader.Find("VirtualMuseum/ExcavationSurface");
            if (shader == null)
            {
                Debug.LogError("[DemoSceneBuilder] ExcavationSurface 셰이더를 찾을 수 없습니다.");
                shader = Shader.Find("Standard");
            }

            var artifactAlbedo = CreateNoiseTexture("ArtifactMarble",
                new Color(0.72f, 0.7f, 0.66f), new Color(0.92f, 0.9f, 0.86f), 0.05f);
            var dirtAlbedo = CreateNoiseTexture("DirtSoil",
                new Color(0.32f, 0.22f, 0.13f), new Color(0.55f, 0.42f, 0.28f), 0.08f);
            var flatNormal = CreateFlatNormalTexture();

            var mat = new Material(shader);
            mat.SetTexture("_ArtifactAlbedo", artifactAlbedo);
            mat.SetTexture("_DirtAlbedo", dirtAlbedo);
            mat.SetTexture("_DirtNormal", flatNormal);
            return ReplaceAsset(mat, MaterialsDir + "/ExcavationSurfaceMat.mat");
        }

        private static Material CreateSimpleMaterial(string name, Color color)
        {
            var mat = new Material(Shader.Find("Standard")) { color = color };
            return ReplaceAsset(mat, $"{MaterialsDir}/{name}.mat");
        }

        private static Material CreateTexturedMaterial(string name, Texture2D albedo)
        {
            var mat = new Material(Shader.Find("Standard")) { mainTexture = albedo };
            return ReplaceAsset(mat, $"{MaterialsDir}/{name}.mat");
        }

        /// <summary>발굴 지층 상판: 흙 노이즈 + 고고학 실측용 격자 눈금 (스펙 6절)</summary>
        private static Texture2D CreateSoilGridTexture()
        {
            const int size = 256;
            const int gridStep = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            float ox = Random.Range(0f, 100f), oy = Random.Range(0f, 100f);
            var dark = new Color(0.35f, 0.25f, 0.15f);
            var light = new Color(0.52f, 0.4f, 0.27f);
            var gridColor = new Color(0.85f, 0.82f, 0.7f);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(ox + x * 0.07f, oy + y * 0.07f);
                    Color c = Color.Lerp(dark, light, n);
                    bool onGrid = x % gridStep == 0 || y % gridStep == 0 ||
                                  (x + 1) % gridStep == 0 || (y + 1) % gridStep == 0;
                    if (onGrid) c = Color.Lerp(c, gridColor, 0.8f);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return ReplaceAsset(tex, TexturesDir + "/SoilGrid.asset");
        }

        /// <summary>지층 측면: 표토 → 점토 → 암반 3층 그라데이션 (스펙 6절)</summary>
        private static Texture2D CreateSoilLayerTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            float ox = Random.Range(0f, 100f);
            var topsoil = new Color(0.45f, 0.33f, 0.2f);  // 표토
            var clay = new Color(0.62f, 0.45f, 0.3f);     // 점토
            var bedrock = new Color(0.42f, 0.4f, 0.38f);  // 암반

            for (int y = 0; y < size; y++)
            {
                float t = y / (float)size; // 0=아래(암반) → 1=위(표토)
                for (int x = 0; x < size; x++)
                {
                    float wobble = (Mathf.PerlinNoise(ox + x * 0.05f, t * 4f) - 0.5f) * 0.15f;
                    float tt = Mathf.Clamp01(t + wobble);
                    Color c = tt < 0.35f ? Color.Lerp(bedrock, clay, tt / 0.35f)
                                         : Color.Lerp(clay, topsoil, (tt - 0.35f) / 0.65f);
                    float grain = (Mathf.PerlinNoise(ox + x * 0.2f, y * 0.2f) - 0.5f) * 0.08f;
                    tex.SetPixel(x, y, new Color(c.r + grain, c.g + grain, c.b + grain));
                }
            }
            tex.Apply();
            return ReplaceAsset(tex, TexturesDir + "/SoilLayers.asset");
        }

        // ---------------------------------------------------------------- 프리팹

        private static GameObject CreatePedestalPrefab(Material pedestalMat, Material soilTopMat,
            Material soilSideMat, Material woodMat, Material metalMat)
        {
            var root = new GameObject("Pedestal");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2f;
            trigger.center = new Vector3(0f, 1f, 0f);
            var proximity = root.AddComponent<PedestalProximityTrigger>();

            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Base";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            baseObj.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
            baseObj.GetComponent<Renderer>().sharedMaterial = pedestalMat;

            // --- 발굴 현장 연출 (스펙 6절): 지층 박스 + 격자 상판 + 나무 테두리 + 소품 ---
            // 시각 전용 파츠는 콜라이더를 제거해 발굴 브러시 레이캐스트/플레이어 이동을 방해하지 않는다.

            AddVisualCube(root.transform, "SoilLayers", soilSideMat,
                new Vector3(0f, 1.06f, 0f), new Vector3(1.1f, 0.12f, 1.1f));
            AddVisualCube(root.transform, "SoilTop", soilTopMat,
                new Vector3(0f, 1.125f, 0f), new Vector3(1.12f, 0.015f, 1.12f));

            // 나무 테두리 4변
            const float rimY = 1.13f, rimH = 0.1f, rimT = 0.05f, rimL = 1.22f;
            AddVisualCube(root.transform, "RimN", woodMat, new Vector3(0f, rimY, 0.585f), new Vector3(rimL, rimH, rimT));
            AddVisualCube(root.transform, "RimS", woodMat, new Vector3(0f, rimY, -0.585f), new Vector3(rimL, rimH, rimT));
            AddVisualCube(root.transform, "RimE", woodMat, new Vector3(0.585f, rimY, 0f), new Vector3(rimT, rimH, rimL));
            AddVisualCube(root.transform, "RimW", woodMat, new Vector3(-0.585f, rimY, 0f), new Vector3(rimT, rimH, rimL));

            // 소품: 발굴 붓 (손잡이 + 브러시 머리)
            var brushHandle = AddVisualPrimitive(root.transform, PrimitiveType.Cylinder, "PropBrushHandle", woodMat,
                new Vector3(0.42f, 1.15f, 0.42f), new Vector3(0.02f, 0.09f, 0.02f));
            brushHandle.transform.localRotation = Quaternion.Euler(90f, 35f, 0f);
            AddVisualCube(root.transform, "PropBrushHead", metalMat,
                new Vector3(0.35f, 1.15f, 0.32f), new Vector3(0.05f, 0.02f, 0.06f));

            // 소품: 트레이 (좌대 옆 바닥)
            AddVisualCube(root.transform, "PropTray", metalMat,
                new Vector3(0.95f, 0.015f, 0.2f), new Vector3(0.3f, 0.03f, 0.22f));
            // 소품: 소형 삽 (트레이 위)
            var shovelHandle = AddVisualPrimitive(root.transform, PrimitiveType.Cylinder, "PropShovelHandle", woodMat,
                new Vector3(0.95f, 0.045f, 0.2f), new Vector3(0.018f, 0.1f, 0.018f));
            shovelHandle.transform.localRotation = Quaternion.Euler(90f, -20f, 0f);

            var hint = new GameObject("Hint");
            hint.transform.SetParent(root.transform, false);
            hint.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var textMesh = hint.AddComponent<TextMesh>();
            textMesh.text = "[Q] 정보   [E] 발굴";
            textMesh.fontSize = 64;
            textMesh.characterSize = 0.028f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            if (UIFont != null)
            {
                textMesh.font = UIFont;
                hint.GetComponent<MeshRenderer>().sharedMaterial = UIFont.material;
            }
            hint.AddComponent<BillboardLabel>();
            hint.SetActive(false);

            SetRef(proximity, "interactionHintUI", hint);

            string path = PrefabsDir + "/Pedestal.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject AddVisualCube(Transform parent, string name, Material mat,
            Vector3 localPos, Vector3 localScale)
        {
            return AddVisualPrimitive(parent, PrimitiveType.Cube, name, mat, localPos, localScale);
        }

        private static GameObject AddVisualPrimitive(Transform parent, PrimitiveType type, string name,
            Material mat, Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // 시각 전용: 콜라이더 제거 (브러시 레이캐스트/이동 간섭 방지)
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            return go;
        }

        private static GameObject CreateShardPrefab(Material shardMat, int shardLayer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Shard";
            go.transform.localScale = new Vector3(0.14f, 0.11f, 0.16f);
            go.layer = shardLayer;
            go.GetComponent<Renderer>().sharedMaterial = shardMat;
            go.AddComponent<ArtifactShard>();

            string path = PrefabsDir + "/Shard.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------------------------------------------------------- uGUI 헬퍼

        private static Canvas CreateCanvasWithEventSystem(out GameObject canvasGO)
        {
            canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvasGO.AddComponent<GraphicRaycaster>();

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            return canvas;
        }

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 size, Color color)
        {
            var rt = CreateUIObject(name, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize,
            Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = CreateUIObject(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = UIFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color bgColor)
        {
            var img = CreatePanel(name, parent, new Vector2(0, 48), bgColor);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            var text = CreateText("Label", img.transform, label, 20, Color.white);
            Stretch(text.rectTransform);
            AddLayout(img.gameObject, 48);
            return button;
        }

        private static InputField CreateInput(string name, Transform parent, string placeholder,
            bool isPassword = false)
        {
            var img = CreatePanel(name, parent, new Vector2(0, 48), new Color(0.95f, 0.95f, 0.97f));
            var input = img.gameObject.AddComponent<InputField>();
            input.targetGraphic = img;

            var textComp = CreateText("Text", img.transform, "", 20, new Color(0.1f, 0.1f, 0.12f),
                TextAnchor.MiddleLeft);
            Stretch(textComp.rectTransform, 12, 6);
            textComp.supportRichText = false;

            var placeholderComp = CreateText("Placeholder", img.transform, placeholder, 20,
                new Color(0.45f, 0.45f, 0.5f), TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch(placeholderComp.rectTransform, 12, 6);

            input.textComponent = textComp;
            input.placeholder = placeholderComp;
            if (isPassword) input.contentType = InputField.ContentType.Password;

            AddLayout(img.gameObject, 48);
            return input;
        }

        private static void Stretch(RectTransform rt, float paddingX = 0, float paddingY = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(paddingX, paddingY);
            rt.offsetMax = new Vector2(-paddingX, -paddingY);
        }

        private static void AddLayout(GameObject go, float preferredHeight)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = preferredHeight;
        }

        private static void SetupVerticalPanel(GameObject panel, Vector2 size)
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 24, 24);
            vlg.spacing = 14;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        // ---------------------------------------------------------------- Login 씬

        private static void BuildLoginScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.12f);
            camGO.AddComponent<AudioListener>();

            CreateCanvasWithEventSystem(out GameObject canvasGO);

            var bg = CreatePanel("Background", canvasGO.transform, Vector2.zero, new Color(0.09f, 0.1f, 0.14f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            var title = CreateText("Title", canvasGO.transform, "가상 박물관  Virtual Museum", 42,
                new Color(0.92f, 0.88f, 0.75f), TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0, -90);
            title.rectTransform.sizeDelta = new Vector2(900, 70);

            var panelColor = new Color(0.15f, 0.17f, 0.22f, 0.97f);

            // 학생 입장 패널
            var studentPanel = CreatePanel("StudentEntryPanel", canvasGO.transform, Vector2.zero, panelColor).gameObject;
            SetupVerticalPanel(studentPanel, new Vector2(520, 330));
            var studentLabel = CreateText("Header", studentPanel.transform, "학생 입장", 26, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            AddLayout(studentLabel.gameObject, 40);
            var nameInput = CreateInput("NameInput", studentPanel.transform, "이름 또는 학번 (미입력 시 Guest)");
            var enterButton = CreateButton("EnterButton", studentPanel.transform, "입장하기",
                new Color(0.2f, 0.55f, 0.35f));
            var toAdminButton = CreateButton("ToAdminButton", studentPanel.transform, "운영자 로그인 →",
                new Color(0.3f, 0.32f, 0.4f));

            // 운영자 로그인 패널
            var adminPanel = CreatePanel("AdminLoginPanel", canvasGO.transform, Vector2.zero, panelColor).gameObject;
            SetupVerticalPanel(adminPanel, new Vector2(520, 430));
            var adminLabel = CreateText("Header", adminPanel.transform, "운영자 로그인", 26, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            AddLayout(adminLabel.gameObject, 40);
            var idInput = CreateInput("IdInput", adminPanel.transform, "아이디 (데모: admin)");
            var pwInput = CreateInput("PwInput", adminPanel.transform, "비밀번호 (데모: admin1234)", isPassword: true);
            var errorText = CreateText("ErrorText", adminPanel.transform, "", 17,
                new Color(1f, 0.45f, 0.4f));
            AddLayout(errorText.gameObject, 26);
            var loginButton = CreateButton("LoginButton", adminPanel.transform, "로그인",
                new Color(0.25f, 0.45f, 0.7f));
            var toStudentButton = CreateButton("ToStudentButton", adminPanel.transform, "← 학생 입장으로",
                new Color(0.3f, 0.32f, 0.4f));

            // LoginUIManager 배선
            var manager = canvasGO.AddComponent<LoginUIManager>();
            SetRef(manager, "studentEntryPanel", studentPanel);
            SetRef(manager, "adminLoginPanel", adminPanel);
            SetRef(manager, "errorText", errorText);
            SetRef(manager, "studentNameInput", nameInput);
            SetRef(manager, "studentEnterButton", enterButton);
            SetRef(manager, "adminIdInput", idInput);
            SetRef(manager, "adminPasswordInput", pwInput);
            SetRef(manager, "adminLoginButton", loginButton);
            SetRef(manager, "showAdminTabButton", toAdminButton);
            SetRef(manager, "showStudentTabButton", toStudentButton);

            EditorSceneManager.SaveScene(scene, ScenesDir + "/Login.unity");
        }

        // ---------------------------------------------------------------- Admin 씬

        private static void BuildAdminScene(Material previewMat, Material tokenMat, Material slotMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("TopDownCamera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.transform.position = new Vector3(0f, 18f, 4f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<PhysicsRaycaster>(); // 3D 오브젝트 드래그 이벤트에 필요

            CreateLight();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, 0f, 4f);
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            floor.GetComponent<Renderer>().sharedMaterial = slotMat;

            // 좌대 슬롯 5개 (seed 데이터의 아크 배치와 동일 좌표)
            Vector3[] slotPositions =
            {
                new Vector3(-6, 0, 6), new Vector3(-3, 0, 7.5f), new Vector3(0, 0, 8),
                new Vector3(3, 0, 7.5f), new Vector3(6, 0, 6)
            };
            for (int i = 0; i < slotPositions.Length; i++)
            {
                var slot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                slot.name = $"Slot_PDT-0{i + 1}";
                slot.transform.position = slotPositions[i] + Vector3.up * 0.05f;
                slot.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);
                slot.GetComponent<Renderer>().sharedMaterial = slotMat;
                var dropSlot = slot.AddComponent<PedestalDropSlot>();
                SetString(dropSlot, "pedestalID", $"PDT-0{i + 1}");
                CreateTopDownLabel(slot.transform, $"PDT-0{i + 1}", new Vector3(0f, 0.6f, -1.1f));
            }

            // 드래그 토큰 5개
            for (int i = 0; i < 5; i++)
            {
                var token = GameObject.CreatePrimitive(PrimitiveType.Cube);
                token.name = $"Token_LOUVRE-00{i + 1}";
                token.transform.position = new Vector3(-6f + i * 3f, 0.3f, 0f);
                token.transform.localScale = new Vector3(1f, 0.4f, 1f);
                token.GetComponent<Renderer>().sharedMaterial = tokenMat;
                var handle = token.AddComponent<ArtifactDragHandle>();
                SetString(handle, "artifactID", $"LOUVRE-00{i + 1}");
                CreateTopDownLabel(token.transform, $"LOUVRE-00{i + 1}", new Vector3(0f, 0.5f, -1f));
            }

            // STL 프리뷰 지점
            var previewRoot = new GameObject("PreviewSpawnPoint");
            previewRoot.transform.position = new Vector3(9f, 0.5f, 0f);
            CreateTopDownLabel(previewRoot.transform, "STL Preview", new Vector3(0f, 0.6f, -1.6f));

            // 관리자 시스템
            var systems = new GameObject("AdminSystems");
            var previewLoader = systems.AddComponent<AdminStlPreviewLoader>();
            SetRef(previewLoader, "previewSpawnPoint", previewRoot.transform);
            SetRef(previewLoader, "defaultPreviewMaterial", previewMat);
            var hud = systems.AddComponent<AdminConsoleHUD>();
            SetRef(hud, "previewLoader", previewLoader);

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, ScenesDir + "/AdminConsole.unity");
        }

        private static void CreateTopDownLabel(Transform parent, string text, Vector3 worldOffset)
        {
            var label = new GameObject("Label");
            // 부모가 스케일된 오브젝트여도 라벨 크기/위치가 왜곡되지 않도록 월드 기준으로 배치 후 부착
            label.transform.position = parent.position + worldOffset;
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 탑다운 카메라를 향하도록
            label.transform.SetParent(parent, true);
            var tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            if (UIFont != null)
            {
                tm.font = UIFont;
                label.GetComponent<MeshRenderer>().sharedMaterial = UIFont.material;
            }
        }

        // ---------------------------------------------------------------- Student 씬

        private static void BuildStudentScene(GameObject pedestalPrefab, GameObject shardPrefab,
            Material excavationMat, Material floorMat, Material wallMat, Texture2D brushTex, int shardLayer)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, 0f, 4f);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // 전시실 벽 4면 (콜라이더 유지 - 플레이어 이탈 방지)
            CreateWall(wallMat, "WallN", new Vector3(0f, 2.5f, 14.5f), new Vector3(30f, 5f, 1f));
            CreateWall(wallMat, "WallS", new Vector3(0f, 2.5f, -6.5f), new Vector3(30f, 5f, 1f));
            CreateWall(wallMat, "WallE", new Vector3(14.5f, 2.5f, 4f), new Vector3(1f, 5f, 22f));
            CreateWall(wallMat, "WallW", new Vector3(-14.5f, 2.5f, 4f), new Vector3(1f, 5f, 22f));

            // 플레이어 (FPS)
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.radius = 0.35f;

            var camGO = new GameObject("PlayerCamera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(player.transform, false);
            camGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            camGO.AddComponent<AudioListener>();

            var fps = player.AddComponent<SimpleFPSController>();
            SetRef(fps, "cameraTransform", camGO.transform);

            // 시스템 매니저
            var systems = new GameObject("Systems");
            var handTracking = systems.AddComponent<HandTrackingManager>();
            var inputMode = systems.AddComponent<InputModeManager>();
            SetRef(inputMode, "handTrackingManager", handTracking);

            var spawner = systems.AddComponent<StudentArtifactSpawner>();
            SetRef(spawner, "pedestalPrefab", pedestalPrefab);
            SetRef(spawner, "excavationDirtMaterialTemplate", excavationMat);
            SetFloat(spawner, "pedestalTopHeight", 1.11f); // 지층 상판(1.13) 기준, 살짝 반매립되도록

            var interaction = systems.AddComponent<StudentInteractionManager>();
            systems.AddComponent<VirtualMuseum.Vault.VaultUIManager>(); // V키 내 유물함

            // 발굴 시스템 (오버레이 - 평소 비활성)
            var excavationSystem = new GameObject("ExcavationSystem");
            var brush = excavationSystem.AddComponent<ExcavationBrush>();
            SetRef(brush, "brushShapeTexture", brushTex);
            var puzzle = excavationSystem.AddComponent<ReassemblyPuzzleManager>();
            SetRef(puzzle, "shardPrefab", shardPrefab);
            var shardDrag = excavationSystem.AddComponent<ShardDragController>();
            SetRef(shardDrag, "puzzleManager", puzzle);
            SetInt(shardDrag, "shardLayerMask", shardLayer > 0 ? 1 << shardLayer : ~0);
            var session = excavationSystem.AddComponent<ExcavationSessionController>();
            SetRef(session, "brush", brush);
            SetRef(session, "puzzleManager", puzzle);

            var particlesGO = new GameObject("SuccessParticles");
            particlesGO.transform.SetParent(excavationSystem.transform, false);
            var particles = particlesGO.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.startSpeed = 2.5f;
            main.startLifetime = 1.2f;
            main.startColor = new Color(1f, 0.85f, 0.4f);
            SetRef(session, "successParticles", particles);

            // 파손 파편 파티클 (갈색 파편이 짧게 튀는 연출)
            var breakGO = new GameObject("BreakParticles");
            breakGO.transform.SetParent(excavationSystem.transform, false);
            var breakPs = breakGO.AddComponent<ParticleSystem>();
            var breakMain = breakPs.main;
            breakMain.playOnAwake = false;
            breakMain.startSpeed = 3.5f;
            breakMain.startLifetime = 0.7f;
            breakMain.startSize = 0.06f;
            breakMain.startColor = new Color(0.6f, 0.42f, 0.3f);
            var breakEmission = breakPs.emission;
            breakEmission.rateOverTime = 0f;
            breakEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)40) });
            SetRef(session, "breakParticles", breakPs);

            excavationSystem.SetActive(false);

            // UI
            CreateCanvasWithEventSystem(out GameObject canvasGO);

            var dim = CreatePanel("DimOverlay", canvasGO.transform, Vector2.zero, Color.black);
            Stretch(dim.rectTransform);
            dim.raycastTarget = false;
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            dimGroup.alpha = 0f;
            dimGroup.blocksRaycasts = false;
            dimGroup.interactable = false;

            var infoPanel = CreatePanel("InfoPanel", canvasGO.transform, Vector2.zero,
                new Color(0.13f, 0.14f, 0.18f, 0.98f)).gameObject;
            SetupVerticalPanel(infoPanel, new Vector2(780, 540));

            var nameText = CreateText("NameText", infoPanel.transform, "유물 이름", 28, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            AddLayout(nameText.gameObject, 44);
            var rarityText = CreateText("RarityText", infoPanel.transform, "", 18,
                new Color(0.9f, 0.8f, 0.5f));
            AddLayout(rarityText.gameObject, 28);

            var videoRT = CreateUIObject("VideoImage", infoPanel.transform);
            var videoImage = videoRT.gameObject.AddComponent<RawImage>();
            videoImage.color = Color.white;
            AddLayout(videoRT.gameObject, 240);
            videoRT.gameObject.SetActive(false);

            var descText = CreateText("DescriptionText", infoPanel.transform, "", 19,
                new Color(0.9f, 0.9f, 0.92f), TextAnchor.UpperLeft);
            var descLE = descText.gameObject.AddComponent<LayoutElement>();
            descLE.flexibleHeight = 1;
            var closeButton = CreateButton("CloseButton", infoPanel.transform, "닫기 (Q / ESC)",
                new Color(0.3f, 0.32f, 0.4f));

            var infoView = infoPanel.AddComponent<InfoPanelView>();
            SetRef(infoView, "nameText", nameText);
            SetRef(infoView, "rarityText", rarityText);
            SetRef(infoView, "descriptionText", descText);
            SetRef(infoView, "videoImage", videoImage);
            SetRef(infoView, "closeButton", closeButton);
            infoPanel.SetActive(false);

            // 상호작용 매니저 배선
            SetRef(interaction, "infoPanelUI", infoPanel);
            SetRef(interaction, "excavationOverlayUI", excavationSystem);
            SetRef(interaction, "backgroundDimOverlay", dimGroup);

            EditorSceneManager.SaveScene(scene, ScenesDir + "/StudentPerspective.unity");
        }

        private static void CreateWall(Material mat, string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateLight()
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
#endif
