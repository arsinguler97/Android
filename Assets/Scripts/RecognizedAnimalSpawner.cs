using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class RecognizedAnimalSpawner : MonoBehaviour
{
    [Serializable]
    private class AnimalPrefabMapping
    {
        public string label;
        public GameObject prefab;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public float scaleMultiplier = 1f;
        public bool alignBoundsToSurface = true;
        public AudioClip sfx;
    }

    [SerializeField] private ARCameraClassifier classifier;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private List<AnimalPrefabMapping> animalPrefabs = new();
    [SerializeField, Range(0f, 1f)] private float minimumConfidence = 0.7f;
    [SerializeField] private float spawnCooldownSeconds = 2f;
    [SerializeField] private bool replaceCurrentAnimal = true;
    [SerializeField, Range(0f, 1f)] private float minimumHorizontalDot = 0.75f;
    [SerializeField] private bool useRecognizedAnimalState;
    [SerializeField] private bool clearStateAfterSpawn;
    [SerializeField, Min(0f)] private float animalClickMaxDistance = 100f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField] private string editorFallbackLabel = "lion";

    private static readonly List<ARRaycastHit> Hits = new();

    private readonly Dictionary<string, AnimalPrefabMapping> mappingByLabel = new(StringComparer.OrdinalIgnoreCase);
    private GameObject currentAnimal;
    private string currentLabel;
    private AudioClip currentAnimalSfx;
    private float nextSpawnTime;
    private AnimalPrefabMapping pendingMapping;
    private string pendingLabel;
    private bool placementEnabled;


    private void Awake()
    {
        ResolveARRuntimeReferences();

        RebuildLookup();

        if (useRecognizedAnimalState)
        {
            LoadPendingAnimalFromState();
        }
    }

    private void OnEnable()
    {
        if (!useRecognizedAnimalState && classifier != null)
        {
            classifier.PredictionUpdated += HandlePredictionUpdated;
        }
    }

    private void OnDisable()
    {
        if (!useRecognizedAnimalState && classifier != null)
        {
            classifier.PredictionUpdated -= HandlePredictionUpdated;
        }
    }

    public bool SetPendingAnimal(string label)
    {
        placementEnabled = TrySetPendingAnimal(label);
        return placementEnabled;
    }

    public void ClearPendingAnimal()
    {
        placementEnabled = false;
        pendingLabel = null;
        pendingMapping = null;
    }

    private void Update()
    {
        ResolveARRuntimeReferences();

        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        var screenPosition = pointer.position.ReadValue();
        if (TryPlayCurrentAnimalSfx(screenPosition))
        {
            return;
        }

        if (!placementEnabled || pendingMapping == null || pendingMapping.prefab == null || Time.time < nextSpawnTime)
        {
            return;
        }

        if (!replaceCurrentAnimal && currentAnimal != null && string.Equals(currentLabel, pendingLabel, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (TryGetSpawnPose(screenPosition, out var pose))
        {
            SpawnAnimal(pendingLabel, pendingMapping, pose);
            nextSpawnTime = Time.time + spawnCooldownSeconds;
        }
    }


private void RebuildLookup()
    {
        mappingByLabel.Clear();

        foreach (var mapping in animalPrefabs)
        {
            if (mapping == null || string.IsNullOrWhiteSpace(mapping.label) || mapping.prefab == null)
            {
                continue;
            }

            mappingByLabel[mapping.label.Trim()] = mapping;
        }
    }

    private void ResolveARRuntimeReferences()
    {
        if (classifier == null || !classifier.isActiveAndEnabled)
        {
            classifier = FindFirstObjectByType<ARCameraClassifier>();
        }

        if (raycastManager == null || !raycastManager.isActiveAndEnabled)
        {
            raycastManager = FindFirstObjectByType<ARRaycastManager>();
        }

        if (arCamera == null || !arCamera.isActiveAndEnabled)
        {
            arCamera = FindActiveARCamera();
        }
    }

    private Camera FindActiveARCamera()
    {
        var cameraManagers = FindObjectsByType<ARCameraManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var cameraManager in cameraManagers)
        {
            if (cameraManager == null || !cameraManager.isActiveAndEnabled)
            {
                continue;
            }

            var camera = cameraManager.GetComponent<Camera>();
            if (camera != null && camera.isActiveAndEnabled)
            {
                return camera;
            }
        }

        var mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        return FindFirstObjectByType<Camera>();
    }

private void HandlePredictionUpdated(string label, float confidence)
    {
        if (confidence < minimumConfidence || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var normalizedLabel = label.Trim();
        if (!mappingByLabel.TryGetValue(normalizedLabel, out var mapping))
        {
            return;
        }

        pendingLabel = normalizedLabel;
        pendingMapping = mapping;
        Debug.Log($"Recognized animal ready: {pendingLabel} ({confidence * 100f:0.0}%)", this);
    }

private void SpawnAnimal(string label, AnimalPrefabMapping mapping, Pose pose)
    {
        if (replaceCurrentAnimal && currentAnimal != null)
        {
            Destroy(currentAnimal);
        }

        currentAnimal = Instantiate(mapping.prefab, pose.position, pose.rotation);
        currentAnimal.SetActive(true);

        if (mapping.alignBoundsToSurface)
        {
            AlignBoundsBottomToPoint(currentAnimal, pose.position);
        }

        currentAnimal.transform.position += pose.rotation * mapping.positionOffset;
        currentAnimal.transform.rotation *= Quaternion.Euler(mapping.rotationOffset);
        currentAnimal.transform.localScale *= Mathf.Max(0.0001f, mapping.scaleMultiplier);
        PrepareAnimalClickSfx(currentAnimal, mapping.sfx);

        currentLabel = label;
        Debug.Log($"Spawned recognized animal: {label}", currentAnimal);

        if (clearStateAfterSpawn)
        {
            AnimalRecognitionState.Clear();
        }
    }

    private bool TryPlayCurrentAnimalSfx(Vector2 screenPosition)
    {
        if (currentAnimal == null || currentAnimalSfx == null || arCamera == null)
        {
            return false;
        }

        var ray = arCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hit, animalClickMaxDistance))
        {
            return false;
        }

        if (!hit.transform.IsChildOf(currentAnimal.transform))
        {
            return false;
        }

        AudioSource.PlayClipAtPoint(currentAnimalSfx, currentAnimal.transform.position, sfxVolume);
        return true;
    }

    private void PrepareAnimalClickSfx(GameObject animal, AudioClip clip)
    {
        currentAnimalSfx = null;

        if (animal == null || clip == null)
        {
            return;
        }

        EnsureAnimalClickCollider(animal);
        currentAnimalSfx = clip;
    }

    private void EnsureAnimalClickCollider(GameObject animal)
    {
        var colliders = animal.GetComponentsInChildren<Collider>(true);
        foreach (var animalCollider in colliders)
        {
            if (animalCollider.enabled && animalCollider.gameObject.activeInHierarchy)
            {
                return;
            }
        }

        var renderers = animal.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        var boxCollider = animal.AddComponent<BoxCollider>();
        var scale = animal.transform.lossyScale;
        boxCollider.center = animal.transform.InverseTransformPoint(bounds.center);
        boxCollider.size = new Vector3(
            SafeDivide(bounds.size.x, scale.x),
            SafeDivide(bounds.size.y, scale.y),
            SafeDivide(bounds.size.z, scale.z));
    }

    private float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / Mathf.Abs(divisor) : value;
    }

    private void LoadPendingAnimalFromState()
    {
        if (!AnimalRecognitionState.HasAnimal)
        {
#if UNITY_EDITOR
            if (TrySetPendingAnimal(editorFallbackLabel))
            {
            placementEnabled = true;
            Debug.Log($"AnimalScene opened without recognized animal. Using editor fallback: {pendingLabel}", this);
            return;
            }
#endif
            Debug.LogWarning("AnimalScene opened without a recognized animal.", this);
            return;
        }

        if (!TrySetPendingAnimal(AnimalRecognitionState.Label))
        {
            Debug.LogWarning($"No prefab mapping found for recognized animal: {AnimalRecognitionState.Label}", this);
            return;
        }

        placementEnabled = true;
        Debug.Log($"Ready to place recognized animal from state: {pendingLabel}", this);
    }

    private bool TrySetPendingAnimal(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalizedLabel = label.Trim();
        if (!mappingByLabel.TryGetValue(normalizedLabel, out var mapping))
        {
            return false;
        }

        pendingLabel = normalizedLabel;
        pendingMapping = mapping;
        return true;
    }

private void AlignBoundsBottomToPoint(GameObject instance, Vector3 targetPoint)
    {
        if (instance == null)
        {
            return;
        }

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        var hasBounds = false;
        var bounds = new Bounds();
        foreach (var renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        var bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        instance.transform.position += targetPoint - bottomCenter;
    }


private bool TryGetSpawnPose(Vector2 screenPosition, out Pose pose)
    {
        pose = default;

        if (raycastManager == null || !raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
        {
            return false;
        }

        foreach (var hit in Hits)
        {
            var surfaceUp = hit.pose.rotation * Vector3.up;
            if (Vector3.Dot(surfaceUp.normalized, Vector3.up) < minimumHorizontalDot)
            {
                continue;
            }

            var forward = arCamera != null ? Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up) : Vector3.forward;
            var rotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
            pose = new Pose(hit.pose.position, rotation);
            return true;
        }

        return false;
    }
}
