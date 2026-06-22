using System;
using System.Collections;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARCameraClassifier : MonoBehaviour
{
    public event Action<string, float> PredictionUpdated;

    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private TextAsset classDescriptions;
    [SerializeField] private TMP_Text predictionText;
    [SerializeField] private RawImage debugPreview;
#pragma warning disable CS0414
    [SerializeField] private BackendType backendType = BackendType.GPUCompute;
    [SerializeField] private float intervalSeconds = 2f;
#pragma warning restore CS0414
    [SerializeField] private BackendType editorBackendType = BackendType.CPU;
    [SerializeField] private float editorIntervalSeconds = 1.25f;
    [SerializeField] private Vector2Int cameraImageSize = new(224, 224);
    [SerializeField] private bool useRenderedCameraInEditor = true;

    private const int DefaultInputSize = 224;

    private Worker worker;
    private Tensor<float> inputTensor;
    private TextureTransform textureTransform;
    private Texture2D cameraTexture;
    private RenderTexture renderTexture;
    private string[] labels;
    private Coroutine classificationLoop;
    private bool resourcesReady;

    private IEnumerator Start()
    {
        EnsureUI();

        if (!ValidateReferences())
        {
            yield break;
        }

        labels = classDescriptions.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        predictionText.text = "Prediction: loading model\nConfidence: 0%";

        SetupInference();
        resourcesReady = true;
        StartClassificationLoop();

        yield return null;
    }

    private void OnEnable()
    {
        if (resourcesReady)
        {
            StartClassificationLoop();
        }
    }

    private void OnDisable()
    {
        StopClassificationLoop();
    }

    private bool ValidateReferences()
    {
        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<ARCameraManager>();
        }

        if (arCamera == null && cameraManager != null)
        {
            arCamera = cameraManager.GetComponent<Camera>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
        }

        if (modelAsset == null || classDescriptions == null || cameraManager == null || predictionText == null)
        {
            Debug.LogError("ARCameraClassifier needs cameraManager, modelAsset, classDescriptions, and predictionText assigned.", this);
            return false;
        }

        return true;
    }

    private void SetupInference()
    {
        var sourceModel = ModelLoader.Load(modelAsset);
        var inputShape = ResolveInputShape(sourceModel);
        var layout = ResolveTensorLayout(inputShape);
        var graph = new FunctionalGraph();
        var graphInput = graph.AddInput(sourceModel, 0);
        var normalizedInput = NormalizeInput(graphInput, layout);
        var outputs = Functional.Forward(sourceModel, normalizedInput);
        var runtimeModel = graph.Compile(outputs);

        worker = new Worker(runtimeModel, ResolveBackendType());
        inputTensor = new Tensor<float>(inputShape);
        textureTransform = new TextureTransform().SetTensorLayout(layout).SetCoordOrigin(CoordOrigin.TopLeft);

        var width = inputShape.rank == 4 && layout == TensorLayout.NHWC ? inputShape[2] : inputShape[3];
        var height = inputShape.rank == 4 && layout == TensorLayout.NHWC ? inputShape[1] : inputShape[2];
        cameraImageSize = new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
        cameraTexture = new Texture2D(cameraImageSize.x, cameraImageSize.y, TextureFormat.RGBA32, false);
        renderTexture = new RenderTexture(cameraImageSize.x, cameraImageSize.y, 24, RenderTextureFormat.ARGB32);

        if (debugPreview != null)
        {
            debugPreview.texture = cameraTexture;
        }
    }

    private TensorShape ResolveInputShape(Model model)
    {
        if (model.inputs.Count > 0 && model.inputs[0].shape.IsStatic())
        {
            return model.inputs[0].shape.ToTensorShape();
        }

        return new TensorShape(1, 3, DefaultInputSize, DefaultInputSize);
    }

    private TensorLayout ResolveTensorLayout(TensorShape shape)
    {
        if (shape.rank == 4 && shape[3] == 3)
        {
            return TensorLayout.NHWC;
        }

        return TensorLayout.NCHW;
    }

    private FunctionalTensor NormalizeInput(FunctionalTensor input, TensorLayout layout)
    {
        var mean = new[] { 0.485f, 0.456f, 0.406f };
        var std = new[] { 0.229f, 0.224f, 0.225f };
        var inverseStd = std.Select(value => 1f / value).ToArray();
        var bias = mean.Zip(std, (m, s) => -m / s).ToArray();
        var channelShape = layout == TensorLayout.NHWC ? new TensorShape(1, 1, 1, 3) : new TensorShape(1, 3, 1, 1);

        return input * Functional.Constant(channelShape, inverseStd) + Functional.Constant(channelShape, bias);
    }

    private IEnumerator ClassifyLoop()
    {
        while (resourcesReady)
        {
            if (TryUpdateCameraTexture())
            {
                ClassifyFrame();
            }
            else if (predictionText != null)
            {
                predictionText.text = "Prediction: waiting for AR camera\nConfidence: 0%";
            }

            yield return new WaitForSeconds(ResolveIntervalSeconds());
        }
    }

    private void StartClassificationLoop()
    {
        if (classificationLoop == null)
        {
            classificationLoop = StartCoroutine(ClassifyLoop());
        }
    }

    private void StopClassificationLoop()
    {
        if (classificationLoop == null)
        {
            return;
        }

        StopCoroutine(classificationLoop);
        classificationLoop = null;
    }

    private BackendType ResolveBackendType()
    {
#if UNITY_EDITOR
        return editorBackendType;
#else
        return backendType;
#endif
    }

    private float ResolveIntervalSeconds()
    {
#if UNITY_EDITOR
        return Mathf.Max(0.1f, editorIntervalSeconds);
#else
        return Mathf.Max(0.1f, intervalSeconds);
#endif
    }

    private bool TryUpdateCameraTexture()
    {
#if UNITY_EDITOR
        if (useRenderedCameraInEditor && TryRenderCameraTexture())
        {
            return true;
        }
#endif

        if (cameraManager == null || !cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            return false;
        }

        using (cpuImage)
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = cameraImageSize,
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            var rawTextureData = cameraTexture.GetRawTextureData<byte>();
            cpuImage.Convert(conversionParams, rawTextureData);
            cameraTexture.Apply(false);
        }

        return true;
    }

#if UNITY_EDITOR
    private bool TryRenderCameraTexture()
    {
        if (arCamera == null || renderTexture == null || cameraTexture == null)
        {
            return false;
        }

        var previousTarget = arCamera.targetTexture;
        var previousActive = RenderTexture.active;

        arCamera.targetTexture = renderTexture;
        RenderTexture.active = renderTexture;
        arCamera.Render();
        cameraTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        cameraTexture.Apply(false);

        arCamera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        return true;
    }
#endif

    private void ClassifyFrame()
    {
        TextureConverter.ToTensor(cameraTexture, inputTensor, textureTransform);
        worker.Schedule(inputTensor);

        var output = worker.PeekOutput() as Tensor<float>;
        using var cpuOutput = output.ReadbackAndClone();
        var result = FindBestClass(cpuOutput);
        var label = result.index >= 0 && result.index < labels.Length ? labels[result.index] : $"Class {result.index}";

        predictionText.text = $"Prediction: {label}\nConfidence: {result.confidence * 100f:0.0}%";
        PredictionUpdated?.Invoke(label, result.confidence);
    }

    private (int index, float confidence) FindBestClass(Tensor<float> output)
    {
        var length = output.shape.length;
        var maxLogit = float.NegativeInfinity;
        var maxIndex = 0;

        for (var i = 0; i < length; i++)
        {
            var value = output[i];
            if (value > maxLogit)
            {
                maxLogit = value;
                maxIndex = i;
            }
        }

        var minValue = float.PositiveInfinity;
        var sumRaw = 0f;

        for (var i = 0; i < length; i++)
        {
            var value = output[i];
            minValue = Mathf.Min(minValue, value);
            sumRaw += value;
        }

        if (minValue >= 0f && sumRaw > 0.99f && sumRaw < 1.01f)
        {
            return (maxIndex, output[maxIndex]);
        }

        var sum = 0f;
        for (var i = 0; i < length; i++)
        {
            sum += Mathf.Exp(output[i] - maxLogit);
        }

        var confidence = sum > 0f ? 1f / sum : 0f;
        return (maxIndex, confidence);
    }

    private void EnsureUI()
    {
        if (predictionText != null)
        {
            return;
        }

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        var textObject = new GameObject("Prediction Result", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);
        predictionText = textObject.GetComponent<TMP_Text>();
        predictionText.text = "Prediction: pending\nConfidence: 0%";
        predictionText.fontSize = 42;
        predictionText.color = Color.white;
        predictionText.alignment = TextAlignmentOptions.TopLeft;

        var rect = predictionText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(32f, -32f);
        rect.sizeDelta = new Vector2(900f, 160f);
    }

    private void OnDestroy()
    {
        resourcesReady = false;
        StopClassificationLoop();

        inputTensor?.Dispose();
        worker?.Dispose();

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
