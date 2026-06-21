using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AnimalScanSceneController : MonoBehaviour
{
    [Serializable]
    private class AnimalLabel
    {
        public string label;
        public string displayName;
    }

    [SerializeField] private ARCameraClassifier classifier;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string animalSceneName = "AnimalScene";
    [SerializeField, Range(0f, 1f)] private float minimumConfidence = 0.75f;
    [SerializeField] private float activationDelaySeconds = 1.5f;
    [SerializeField] private List<AnimalLabel> supportedLabels = new();

    private readonly Dictionary<string, string> displayNameByLabel = new(StringComparer.OrdinalIgnoreCase);
    private bool loadingAnimalScene;
    private float enabledAtTime;

    private void Awake()
    {
        if (classifier == null)
        {
            classifier = FindFirstObjectByType<ARCameraClassifier>();
        }

        RebuildLookup();
        SetStatus("Scan an animal");
        AnimalRecognitionState.Clear();
        enabledAtTime = Time.time;
    }

    private void OnEnable()
    {
        ResetScanState();

        if (classifier != null)
        {
            classifier.PredictionUpdated += HandlePredictionUpdated;
        }
    }

    private void OnDisable()
    {
        if (classifier != null)
        {
            classifier.PredictionUpdated -= HandlePredictionUpdated;
        }
    }

    private void RebuildLookup()
    {
        displayNameByLabel.Clear();

        foreach (var supportedLabel in supportedLabels)
        {
            if (supportedLabel == null || string.IsNullOrWhiteSpace(supportedLabel.label))
            {
                continue;
            }

            var label = supportedLabel.label.Trim();
            displayNameByLabel[label] = string.IsNullOrWhiteSpace(supportedLabel.displayName)
                ? label
                : supportedLabel.displayName.Trim();
        }
    }

    private void HandlePredictionUpdated(string label, float confidence)
    {
        if (loadingAnimalScene || Time.time < enabledAtTime + activationDelaySeconds || confidence < minimumConfidence || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var normalizedLabel = label.Trim();
        if (!displayNameByLabel.TryGetValue(normalizedLabel, out var displayName))
        {
            SetStatus("Scan an animal");
            return;
        }

        loadingAnimalScene = true;
        AnimalRecognitionState.SetAnimal(normalizedLabel, displayName, confidence);
        SetStatus($"Animal detected!\n{displayName} - {confidence * 100f:0}%");
        Debug.Log($"[AnimalScanSceneController] Detected {displayName} from label '{normalizedLabel}' at {confidence * 100f:0.0}%. Loading {animalSceneName}.", this);
        AppSceneLoader.Load(animalSceneName);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    public void ResetScanState()
    {
        loadingAnimalScene = false;
        enabledAtTime = Time.time;
        AnimalRecognitionState.Clear();
        SetStatus("Scan an animal");
        Debug.Log($"[AnimalScanSceneController] Scan state reset. Detection locked until t={enabledAtTime + activationDelaySeconds:0.00}.", this);
    }
}
