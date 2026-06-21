using System.Collections;
using System.Reflection;
using System.Linq;
using TMPro;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;

public class AIWebcamClassifier : MonoBehaviour
{
    [SerializeField] ModelAsset modelAsset;
    [SerializeField] TextAsset classDescriptions;
    [SerializeField] RawImage webcamPreview;
    [SerializeField] TMP_Text predictionText;
    [SerializeField] BackendType backendType = BackendType.GPUCompute;
    [SerializeField] float intervalSeconds = 0.5f;

    const int DefaultInputSize = 224;

    WebCamTexture webcamTexture;
    Worker worker;
    Tensor<float> inputTensor;
    TextureTransform textureTransform;
    string[] labels;
    Coroutine classificationLoop;
    bool resourcesReady;
    WebCamDevice[] webcamDevices;

    IEnumerator Start()
    {
        EnsureUI();

        if (!ValidateReferences())
            yield break;

        labels = classDescriptions.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        predictionText.text = "Prediction: loading model\nConfidence: 0%";

        SetupInference();

        predictionText.text = "Prediction: starting camera\nConfidence: 0%";
        DisableVuforiaRuntime();
        yield return null;

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            predictionText.text = "Prediction: webcam permission denied\nConfidence: 0%";
            yield break;
        }

        webcamDevices = WebCamTexture.devices;
        if (webcamDevices.Length == 0)
        {
            predictionText.text = "Prediction: no webcam device found\nConfidence: 0%";
            yield break;
        }

        StartWebcam();

        predictionText.text = $"Prediction: waiting for webcam {webcamTexture.deviceName}\nConfidence: 0%";
        resourcesReady = true;
        classificationLoop = StartCoroutine(ClassifyLoop());
    }

    bool ValidateReferences()
    {
        if (modelAsset == null || classDescriptions == null || webcamPreview == null || predictionText == null)
        {
            Debug.LogError("AIWebcamClassifier needs modelAsset, classDescriptions, webcamPreview, and predictionText assigned.", this);
            return false;
        }

        return true;
    }

    void SetupInference()
    {
        var sourceModel = ModelLoader.Load(modelAsset);
        var inputShape = ResolveInputShape(sourceModel);
        var layout = ResolveTensorLayout(inputShape);
        var graph = new FunctionalGraph();
        var graphInput = graph.AddInput(sourceModel, 0);
        var normalizedInput = NormalizeInput(graphInput, layout);
        var outputs = Functional.Forward(sourceModel, normalizedInput);
        var runtimeModel = graph.Compile(outputs);

        worker = new Worker(runtimeModel, backendType);
        inputTensor = new Tensor<float>(inputShape);
        textureTransform = new TextureTransform().SetTensorLayout(layout).SetCoordOrigin(CoordOrigin.TopLeft);
    }

    TensorShape ResolveInputShape(Model model)
    {
        if (model.inputs.Count > 0 && model.inputs[0].shape.IsStatic())
            return model.inputs[0].shape.ToTensorShape();

        return new TensorShape(1, 3, DefaultInputSize, DefaultInputSize);
    }

    TensorLayout ResolveTensorLayout(TensorShape shape)
    {
        if (shape.rank == 4 && shape[3] == 3)
            return TensorLayout.NHWC;

        return TensorLayout.NCHW;
    }

    FunctionalTensor NormalizeInput(FunctionalTensor input, TensorLayout layout)
    {
        var mean = new[] { 0.485f, 0.456f, 0.406f };
        var std = new[] { 0.229f, 0.224f, 0.225f };
        var inverseStd = std.Select(v => 1f / v).ToArray();
        var bias = mean.Zip(std, (m, s) => -m / s).ToArray();
        var channelShape = layout == TensorLayout.NHWC ? new TensorShape(1, 1, 1, 3) : new TensorShape(1, 3, 1, 1);

        return input * Functional.Constant(channelShape, inverseStd) + Functional.Constant(channelShape, bias);
    }

    void StartWebcam()
    {
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            Destroy(webcamTexture);
        }

        var deviceName = webcamDevices != null && webcamDevices.Length > 0 ? webcamDevices[0].name : null;
        webcamTexture = string.IsNullOrEmpty(deviceName) ? new WebCamTexture(1280, 720, 30) : new WebCamTexture(deviceName, 1280, 720, 30);
        webcamPreview.texture = webcamTexture;
        webcamTexture.Play();
    }

    IEnumerator ClassifyLoop()
    {
        var retryCount = 0;

        while (resourcesReady)
        {
            if (webcamTexture != null && webcamTexture.width > 16 && webcamTexture.height > 16)
            {
                ClassifyFrame();
            }
            else if (predictionText != null && webcamTexture != null)
            {
                predictionText.text = $"Prediction: waiting for webcam {webcamTexture.deviceName} {webcamTexture.width}x{webcamTexture.height} playing={webcamTexture.isPlaying}\nConfidence: 0%";

                if (retryCount < 3)
                {
                    retryCount++;
                    DisableVuforiaRuntime();
                    yield return new WaitForSeconds(0.5f);
                    StartWebcam();
                }
            }

            yield return new WaitForSeconds(intervalSeconds);
        }
    }

    void ClassifyFrame()
    {
        TextureConverter.ToTensor(webcamTexture, inputTensor, textureTransform);
        worker.Schedule(inputTensor);

        var output = worker.PeekOutput() as Tensor<float>;
        using var cpuOutput = output.ReadbackAndClone();
        var result = FindBestClass(cpuOutput);
        var label = result.index >= 0 && result.index < labels.Length ? labels[result.index] : $"Class {result.index}";
        predictionText.text = $"Prediction: {label}\nConfidence: {result.confidence * 100f:0.0}%";
    }

    (int index, float confidence) FindBestClass(Tensor<float> output)
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
            return (maxIndex, output[maxIndex]);

        var sum = 0f;
        for (var i = 0; i < length; i++)
            sum += Mathf.Exp(output[i] - maxLogit);

        var confidence = sum > 0f ? 1f / sum : 0f;
        return (maxIndex, confidence);
    }

    void EnsureUI()
    {
        if (webcamPreview != null && predictionText != null)
            return;

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

        if (webcamPreview == null)
        {
            var previewObject = new GameObject("Webcam Preview", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(canvas.transform, false);
            webcamPreview = previewObject.GetComponent<RawImage>();
            webcamPreview.color = Color.white;
            var rect = webcamPreview.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        if (predictionText == null)
        {
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
    }

    void DisableVuforiaRuntime()
    {
        DisableSingletonBehaviour("Vuforia.VuforiaBehaviour");
        InvokeSingletonMethod("Vuforia.VuforiaApplication", "Pause");
        InvokeSingletonMethod("Vuforia.VuforiaApplication", "Stop");
        InvokeSingletonMethod("Vuforia.VuforiaApplication", "Deinit");
    }

    void DisableSingletonBehaviour(string typeName)
    {
        var type = FindType(typeName);
        var instance = GetInstance(type);
        if (instance is Behaviour behaviour)
            behaviour.enabled = false;
    }

    void InvokeSingletonMethod(string typeName, string methodName)
    {
        var type = FindType(typeName);
        var instance = GetInstance(type);
        var method = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
        method?.Invoke(instance, null);
    }

    System.Type FindType(string typeName)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        return null;
    }

    object GetInstance(System.Type type)
    {
        if (type == null)
            return null;

        var property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return property?.GetValue(null);
    }

    void OnDestroy()
    {
        resourcesReady = false;

        if (classificationLoop != null)
            StopCoroutine(classificationLoop);

        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            Destroy(webcamTexture);
        }

        inputTensor?.Dispose();
        worker?.Dispose();
    }
}
