using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScanPlaceModeUIController : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private TMP_Text templateText;
    [SerializeField] private AnimalScanSceneController scanController;
    [SerializeField] private string placementFallbackText = "Place the animal";

    private GameObject bgObject;
    private GameObject scanPanelObject;
    private GameObject animalsListButtonObject;
    private GameObject animalsListPanelObject;
    private GameObject placementRoot;
    private TMP_Text placementTitleText;

    private void Awake()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }

        if (scanController == null)
        {
            scanController = FindFirstObjectByType<AnimalScanSceneController>();
        }

        bgObject = FindChild(targetCanvas, "Bg");
        scanPanelObject = FindChild(targetCanvas, "Scan Panel");
        animalsListButtonObject = FindChild(targetCanvas, "Animals List Button");
        animalsListPanelObject = FindChild(targetCanvas, "Animals List Panel");

        CreatePlacementUI();
        ShowScanMode();
    }

    public void ShowScanMode()
    {
        SetActive(bgObject, true);
        SetActive(scanPanelObject, true);
        SetActive(animalsListButtonObject, true);
        SetActive(animalsListPanelObject, false);
        SetActive(placementRoot, false);
    }

    public void ShowPlaceMode(string displayName, float confidence)
    {
        SetActive(bgObject, false);
        SetActive(scanPanelObject, false);
        SetActive(animalsListButtonObject, false);
        SetActive(animalsListPanelObject, false);
        SetActive(placementRoot, true);

        if (placementTitleText != null)
        {
            var title = string.IsNullOrWhiteSpace(displayName) ? placementFallbackText : $"Recognized: {displayName}";
            placementTitleText.text = $"{title}\nTap a flat surface";
        }
    }

    private void CreatePlacementUI()
    {
        if (targetCanvas == null || placementRoot != null)
        {
            return;
        }

        placementRoot = new GameObject("Placement Mode UI", typeof(RectTransform));
        placementRoot.transform.SetParent(targetCanvas.transform, false);

        var rootRect = placementRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        placementTitleText = CreateText("Recognized Animal Text", placementRoot.transform, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -45f), new Vector2(0f, 110f), new Vector2(0.5f, 1f));
        placementTitleText.text = placementFallbackText;
        placementTitleText.alignment = TextAlignmentOptions.Center;
        placementTitleText.fontSize = templateText != null ? templateText.fontSize : 42f;

        var buttonObject = new GameObject("Back Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(placementRoot.transform, false);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = new Vector2(36f, -36f);
        buttonRect.sizeDelta = new Vector2(190f, 76f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        var button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(HandleBackClicked);

        var label = CreateText("Label", buttonObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        label.text = "Back";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = templateText != null ? Mathf.Max(30f, templateText.fontSize) : 48f;
    }

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.pivot = pivot;

        var text = textObject.GetComponent<TMP_Text>();
        text.raycastTarget = false;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;

        if (templateText != null)
        {
            text.font = templateText.font;
            text.fontSharedMaterial = templateText.fontSharedMaterial;
        }

        return text;
    }

    private void HandleBackClicked()
    {
        if (scanController != null)
        {
            scanController.ResetScanState();
        }
        else
        {
            ShowScanMode();
        }
    }

    private GameObject FindChild(Canvas canvas, string objectName)
    {
        if (canvas == null)
        {
            return null;
        }

        var children = canvas.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (child.name == objectName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
