using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AnimalAppFlowSetup
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string ScanScenePath = "Assets/Scenes/ScanScene.unity";
    private const string AnimalScenePath = "Assets/Scenes/AnimalScene.unity";

    [MenuItem("Tools/Animal App/Configure Flow")]
    public static void ConfigureFlow()
    {
        ConfigureAnimalScene();
        ConfigureScanScene();
        ConfigureMainMenu();
        ConfigureBuildSettings();
        EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ConfigureMainMenu()
    {
        EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

        var loaderObject = GameObject.Find("Scene Loader") ?? new GameObject("Scene Loader");
        var loader = loaderObject.GetComponent<TapToLoadScene>() ?? loaderObject.AddComponent<TapToLoadScene>();
        SetPrivateField(loader, "sceneName", "ScanScene");
        EnsureEventSystem();

        SaveActiveScene();
    }

    private static void ConfigureScanScene()
    {
        EditorSceneManager.OpenScene(ScanScenePath, OpenSceneMode.Single);

        var aiObject = GameObject.Find("AIAnimalRecognition");
        if (aiObject == null)
        {
            throw new InvalidOperationException("AIAnimalRecognition not found in ScanScene.");
        }

        SetEditorOnlyIfExists("AITestImageBoard");

        var classifier = aiObject.GetComponent<ARCameraClassifier>();
        if (classifier == null)
        {
            throw new InvalidOperationException("ARCameraClassifier not found in ScanScene.");
        }

        var spawner = aiObject.GetComponent<RecognizedAnimalSpawner>();
        if (spawner != null)
        {
            UnityEngine.Object.DestroyImmediate(spawner);
        }

        var canvas = EnsureCanvas("Scan UI Canvas");
        EnsureEventSystem();
        DestroyDuplicateChildren(canvas.transform, "Animals List Panel");
        DestroyDuplicateChildren(canvas.transform, "Animals List Button");
        var panel = EnsurePanel(canvas.transform, "Scan Panel", new Vector2(620f, 620f), new Color(0f, 0f, 0f, 0.55f));

        var previewObject = GameObject.Find("Camera Preview") ?? new GameObject("Camera Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        previewObject.transform.SetParent(panel.transform, false);
        var rawImage = previewObject.GetComponent<RawImage>();
        rawImage.color = Color.white;
        SetStretch(previewObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.92f));

        var statusText = EnsureText(panel.transform, "Scan Status Text", "Scan an animal", 34, TextAlignmentOptions.Center);
        statusText.transform.SetParent(panel.transform, false);
        SetStretch(statusText.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.16f));

        var listPanel = EnsurePanel(canvas.transform, "Animals List Panel", new Vector2(560f, 560f), new Color(0f, 0f, 0f, 0.82f));
        ConfigureAnimalsList(listPanel);
        listPanel.SetActive(false);

        var animalsListUI = canvas.GetComponent<AnimalsListUI>() ?? canvas.gameObject.AddComponent<AnimalsListUI>();
        SetPrivateField(animalsListUI, "listPanel", listPanel);
        ConfigureAnimalsListButtons(listPanel, animalsListUI);

        var buttonTransform = canvas.transform.Find("Animals List Button");
        var button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : CreateButton(canvas.transform, "Animals List Button", "Animals List");
        var buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 80f);
        buttonRect.sizeDelta = new Vector2(360f, 86f);
        button.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(button.onClick, animalsListUI.Show);

        SetPrivateField(classifier, "predictionText", statusText);
        SetPrivateField(classifier, "debugPreview", rawImage);
        SetPrivateField(classifier, "editorBackendType", Unity.InferenceEngine.BackendType.CPU);
        SetPrivateField(classifier, "editorIntervalSeconds", 1.25f);

        var controllerObject = GameObject.Find("Animal Scan Scene Controller") ?? new GameObject("Animal Scan Scene Controller");
        var controller = controllerObject.GetComponent<AnimalScanSceneController>() ?? controllerObject.AddComponent<AnimalScanSceneController>();
        ConfigureScanController(controller, classifier, statusText);
        ConvertLegacyTextComponents();
        DisableScanSceneNonButtonRaycasts(canvas);

        SaveActiveScene();
    }

    private static void ConfigureAnimalScene()
    {
        if (!System.IO.File.Exists(AnimalScenePath))
        {
            EditorSceneManager.OpenScene(ScanScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), AnimalScenePath, true);
        }

        EditorSceneManager.OpenScene(AnimalScenePath, OpenSceneMode.Single);

        DestroyIfExists("AITestImageBoard");
        DestroyIfExists("Scan UI Canvas");
        DestroyIfExists("XR Origin");
        DestroyIfExists("AR Session");
        DestroyIfExists("EventSystem");

        var aiObject = GameObject.Find("AIAnimalRecognition");
        if (aiObject == null)
        {
            throw new InvalidOperationException("AIAnimalRecognition not found in AnimalScene.");
        }

        var classifier = aiObject.GetComponent<ARCameraClassifier>();
        if (classifier != null)
        {
            UnityEngine.Object.DestroyImmediate(classifier);
        }

        var spawner = aiObject.GetComponent<RecognizedAnimalSpawner>() ?? aiObject.AddComponent<RecognizedAnimalSpawner>();
        SetPrivateField(spawner, "useRecognizedAnimalState", true);
        SetPrivateField(spawner, "clearStateAfterSpawn", false);
        SetPrivateField(spawner, "minimumConfidence", 0.75f);
        SetPrivateField(spawner, "minimumHorizontalDot", 0.75f);
        ConfigureSpawnerSfx(spawner);

        DestroyIfExists("Animal Scan Scene Controller");

        var canvas = EnsureCanvas("Animal Placement UI Canvas");
        var title = EnsureText(canvas.transform, "Recognized Animal Text", "Place the animal", 38, TextAlignmentOptions.Center);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.08f, 1f);
        titleRect.anchorMax = new Vector2(0.92f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -60f);
        titleRect.sizeDelta = new Vector2(0f, 90f);

        var backButton = GameObject.Find("Back Button")?.GetComponent<Button>() ?? CreateButton(canvas.transform, "Back Button", "Back");
        var backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(36f, -36f);
        backRect.sizeDelta = new Vector2(190f, 76f);

        var loader = backButton.GetComponent<SceneLoadButton>() ?? backButton.gameObject.AddComponent<SceneLoadButton>();
        SetPrivateField(loader, "sceneName", "ScanScene");
        backButton.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(backButton.onClick, loader.LoadTargetScene);

        var uiObject = GameObject.Find("Animal Placement UI Controller") ?? new GameObject("Animal Placement UI Controller");
        var uiController = uiObject.GetComponent<AnimalPlacementUIController>() ?? uiObject.AddComponent<AnimalPlacementUIController>();
        SetPrivateField(uiController, "titleText", title);
        ConvertLegacyTextComponents();
        DisableNonButtonRaycasts(canvas);

        SaveActiveScene();
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuPath, true),
            new EditorBuildSettingsScene(ScanScenePath, true),
            new EditorBuildSettingsScene(AnimalScenePath, true)
        };
    }

    private static void ConfigureScanController(AnimalScanSceneController controller, ARCameraClassifier classifier, TMP_Text statusText)
    {
        var so = new SerializedObject(controller);
        so.FindProperty("classifier").objectReferenceValue = classifier;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("animalSceneName").stringValue = "AnimalScene";
        so.FindProperty("minimumConfidence").floatValue = 0.75f;

        var labels = so.FindProperty("supportedLabels");
        var data = new[,]
        {
            { "lion", "Lion" },
            { "alligator", "Alligator" },
            { "American alligator", "Alligator" },
            { "bear", "Bear" },
            { "brown bear", "Bear" },
            { "American black bear", "Bear" },
            { "ice bear", "Bear" },
            { "sloth bear", "Bear" },
            { "fox", "Fox" },
            { "red fox", "Fox" },
            { "kit fox", "Fox" },
            { "Arctic fox", "Fox" },
            { "grey fox", "Fox" },
            { "elephant", "Elephant" },
            { "Indian elephant", "Elephant" },
            { "African elephant", "Elephant" }
        };

        labels.arraySize = data.GetLength(0);
        for (var i = 0; i < labels.arraySize; i++)
        {
            var item = labels.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("label").stringValue = data[i, 0];
            item.FindPropertyRelative("displayName").stringValue = data[i, 1];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureSpawnerSfx(RecognizedAnimalSpawner spawner)
    {
        var lion = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/Lion.wav");
        var alligator = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/Alligator.wav");
        var bear = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/Bear.wav");
        var fox = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/Fox.wav");
        var elephant = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/Elephant.wav");

        var so = new SerializedObject(spawner);
        var mappings = so.FindProperty("animalPrefabs");
        for (var i = 0; i < mappings.arraySize; i++)
        {
            var item = mappings.GetArrayElementAtIndex(i);
            var label = item.FindPropertyRelative("label").stringValue;
            var sfx = item.FindPropertyRelative("sfx");
            sfx.objectReferenceValue = ResolveSfxForLabel(label, lion, alligator, bear, fox, elephant);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spawner);
    }

    private static AudioClip ResolveSfxForLabel(string label, AudioClip lion, AudioClip alligator, AudioClip bear, AudioClip fox, AudioClip elephant)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var normalized = label.ToLowerInvariant();
        if (normalized.Contains("lion"))
        {
            return lion;
        }

        if (normalized.Contains("alligator"))
        {
            return alligator;
        }

        if (normalized.Contains("bear"))
        {
            return bear;
        }

        if (normalized.Contains("fox"))
        {
            return fox;
        }

        if (normalized.Contains("elephant"))
        {
            return elephant;
        }

        return null;
    }

    private static Canvas EnsureCanvas(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            return existing.GetComponent<Canvas>();
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        GameObject eventSystemObject;

        if (eventSystem == null)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }
        else
        {
            eventSystemObject = eventSystem.gameObject;
        }

        var oldStandalone = eventSystemObject.GetComponent<StandaloneInputModule>();
        if (oldStandalone != null)
        {
            UnityEngine.Object.DestroyImmediate(oldStandalone, true);
        }

        if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        var inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
        if (inputModule != null)
        {
            inputModule.rightClick = default;
            EditorUtility.SetDirty(inputModule);
        }

        EditorUtility.SetDirty(eventSystemObject);
    }

    private static GameObject EnsurePanel(Transform parent, string name, Vector2 size, Color color)
    {
        var existing = parent.Find(name);
        var panel = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        return panel;
    }

    private static void DestroyDuplicateChildren(Transform parent, string childName)
    {
        var first = true;
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name != childName)
            {
                continue;
            }

            if (first)
            {
                first = false;
                continue;
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void ConfigureAnimalsList(GameObject listPanel)
    {
        if (listPanel.transform.Find("Title") == null)
        {
            var title = EnsureText(listPanel.transform, "Title", "Supported Animals", 42, TextAlignmentOptions.Center);
            SetStretch(title.rectTransform, new Vector2(0f, 0.78f), new Vector2(1f, 0.96f));

            var body = EnsureText(listPanel.transform, "List", "Fox\nLion\nElephant\nBear\nAlligator", 36, TextAlignmentOptions.Center);
            SetStretch(body.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.78f));

            var close = CreateButton(listPanel.transform, "Close Button", "Close");
            var rect = close.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.04f);
            rect.anchorMax = new Vector2(0.5f, 0.04f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(260f, 76f);
        }
    }

    private static void ConfigureAnimalsListButtons(GameObject listPanel, AnimalsListUI animalsListUI)
    {
        var closeButton = listPanel.transform.Find("Close Button")?.GetComponent<Button>();
        if (closeButton == null || animalsListUI == null)
        {
            return;
        }

        closeButton.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(closeButton.onClick, animalsListUI.Hide);
    }

    private static TMP_Text EnsureText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions alignment)
    {
        var existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);

        var legacyText = go.GetComponent<Text>();
        if (legacyText != null)
        {
            UnityEngine.Object.DestroyImmediate(legacyText, true);
        }

        var uiText = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = Color.white;
        uiText.raycastTarget = false;
        return uiText;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        var text = EnsureText(go.transform, "Label", label, 30, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, Vector2.zero, Vector2.one);
        return go.GetComponent<Button>();
    }

    private static void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null)
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void SetEditorOnlyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            return;
        }

        go.tag = "EditorOnly";
        EditorUtility.SetDirty(go);
    }

    private static void ConvertLegacyTextComponents()
    {
        foreach (var legacyText in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = legacyText.gameObject;
            var text = legacyText.text;
            var fontSize = legacyText.fontSize;
            var color = legacyText.color;
            var alignment = ConvertAlignment(legacyText.alignment);
            var raycastTarget = legacyText.raycastTarget;

            UnityEngine.Object.DestroyImmediate(legacyText, true);

            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = raycastTarget;
            EditorUtility.SetDirty(go);
        }
    }

    private static void DisableNonButtonRaycasts(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
            EditorUtility.SetDirty(graphic);
        }

        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                EditorUtility.SetDirty(image);
            }
        }
    }

    private static void DisableScanSceneNonButtonRaycasts(Canvas canvas)
    {
        DisableNonButtonRaycasts(canvas);

        var listPanel = canvas.transform.Find("Animals List Panel");
        if (listPanel != null)
        {
            var panelImage = listPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.raycastTarget = true;
                EditorUtility.SetDirty(panelImage);
            }
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
        {
            throw new InvalidOperationException($"Field not found: {target.GetType().Name}.{fieldName}");
        }

        field.SetValue(target, value);
        EditorUtility.SetDirty((UnityEngine.Object)target);
    }

    private static void SaveActiveScene()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
