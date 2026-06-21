using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class AppSceneLoader : MonoBehaviour
{
    private static readonly HashSet<string> AppScenes = new()
    {
        "MainMenu",
        "ScanScene",
        "AnimalScene"
    };

    private static readonly string[] ScanSceneObjectsHiddenDuringPlacement =
    {
        "Scan UI Canvas",
        "AITestImageBoard",
        "AIAnimalRecognition"
    };

    private const float ScanReactivationDelaySeconds = 2.5f;

    private static AppSceneLoader instance;
    private bool isLoading;

    public static void Load(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("AppSceneLoader needs a sceneName.");
            return;
        }

        EnsureInstance().StartCoroutine(instance.LoadRoutine(sceneName));
    }

    private static AppSceneLoader EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        var loaderObject = new GameObject("App Scene Loader");
        DontDestroyOnLoad(loaderObject);
        instance = loaderObject.AddComponent<AppSceneLoader>();
        return instance;
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        if (isLoading)
        {
            yield break;
        }

        isLoading = true;

        DisableInputObjectsInScenesToUnload(sceneName);

        var targetScene = SceneManager.GetSceneByName(sceneName);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            var loadMode = GetLoadMode(sceneName);
            var loadOperation = SceneManager.LoadSceneAsync(sceneName, loadMode);
            while (loadOperation != null && !loadOperation.isDone)
            {
                yield return null;
            }

            targetScene = SceneManager.GetSceneByName(sceneName);
        }

        if (targetScene.IsValid() && targetScene.isLoaded)
        {
            SceneManager.SetActiveScene(targetScene);
        }

        for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var loadedScene = SceneManager.GetSceneAt(i);
            if (!AppScenes.Contains(loadedScene.name) || ShouldKeepLoaded(sceneName, loadedScene.name))
            {
                continue;
            }

            var unloadOperation = SceneManager.UnloadSceneAsync(loadedScene);
            while (unloadOperation != null && !unloadOperation.isDone)
            {
                yield return null;
            }
        }

        CleanupDuplicateInputObjects();
        yield return SetScanScenePlacementMode(sceneName == "AnimalScene");
        CleanupDuplicateInputObjects();
        isLoading = false;
    }

    private static LoadSceneMode GetLoadMode(string targetSceneName)
    {
        return targetSceneName == "AnimalScene" ? LoadSceneMode.Additive : LoadSceneMode.Single;
    }

    private static bool ShouldKeepLoaded(string targetSceneName, string loadedSceneName)
    {
        if (loadedSceneName == targetSceneName)
        {
            return true;
        }

        return targetSceneName == "AnimalScene" && loadedSceneName == "ScanScene";
    }

    private IEnumerator SetScanScenePlacementMode(bool isPlacementMode)
    {
        var scanScene = SceneManager.GetSceneByName("ScanScene");
        if (!scanScene.IsValid() || !scanScene.isLoaded)
        {
            yield break;
        }

        if (isPlacementMode)
        {
            SetScanSceneObjectActive(scanScene, "Scan UI Canvas", false);
            SetScanSceneObjectActive(scanScene, "AITestImageBoard", false);
            SetScanSceneObjectActive(scanScene, "AIAnimalRecognition", false);
            Debug.Log("[AppSceneLoader] ScanScene hidden for AnimalScene.");
            yield break;
        }

        SetScanSceneObjectActive(scanScene, "AIAnimalRecognition", false);
        SetScanSceneObjectActive(scanScene, "Scan UI Canvas", true);
        SetScanSceneObjectActive(scanScene, "AITestImageBoard", true);
        ResetScanSceneController(scanScene);
        Debug.Log($"[AppSceneLoader] ScanScene UI restored. Classifier waits {ScanReactivationDelaySeconds:0.0}s.");

        yield return new WaitForSeconds(ScanReactivationDelaySeconds);

        if (SceneManager.GetSceneByName("ScanScene").isLoaded && !SceneManager.GetSceneByName("AnimalScene").isLoaded)
        {
            SetScanSceneObjectActive(scanScene, "AIAnimalRecognition", true);
            Debug.Log("[AppSceneLoader] ScanScene classifier reactivated.");
        }
    }

    private static void SetScanSceneObjectActive(Scene scanScene, string objectName, bool active)
    {
        var sceneObject = FindRootObject(scanScene, objectName);
        if (sceneObject != null)
        {
            sceneObject.SetActive(active);
        }
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == objectName)
            {
                return rootObject;
            }
        }

        return null;
    }

    private static void ResetScanSceneController(Scene scanScene)
    {
        foreach (var rootObject in scanScene.GetRootGameObjects())
        {
            var controller = rootObject.GetComponentInChildren<AnimalScanSceneController>(true);
            if (controller != null)
            {
                controller.ResetScanState();
                return;
            }
        }
    }

    private static void CleanupDuplicateInputObjects()
    {
        var activeScene = SceneManager.GetActiveScene();

        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem eventSystemToKeep = null;
        foreach (var eventSystem in eventSystems)
        {
            if (eventSystem.gameObject.scene == activeScene)
            {
                eventSystemToKeep = eventSystem;
                break;
            }
        }

        if (eventSystemToKeep == null && eventSystems.Length > 0)
        {
            eventSystemToKeep = eventSystems[0];
        }

        foreach (var eventSystem in eventSystems)
        {
            eventSystem.gameObject.SetActive(eventSystem == eventSystemToKeep);
        }

        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        AudioListener listenerToKeep = null;
        foreach (var listener in listeners)
        {
            if (listener.gameObject.scene == activeScene)
            {
                listenerToKeep = listener;
                break;
            }
        }

        if (listenerToKeep == null && listeners.Length > 0)
        {
            listenerToKeep = listeners[0];
        }

        foreach (var listener in listeners)
        {
            listener.enabled = listener == listenerToKeep;
        }
    }

    private static void DisableInputObjectsInScenesToUnload(string targetSceneName)
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var loadedScene = SceneManager.GetSceneAt(i);
            if (!loadedScene.isLoaded || !AppScenes.Contains(loadedScene.name) || ShouldKeepLoaded(targetSceneName, loadedScene.name))
            {
                continue;
            }

            DisableInputObjectsInScene(loadedScene);
        }
    }

    private static void DisableInputObjectsInScene(Scene scene)
    {
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            foreach (var eventSystem in rootObject.GetComponentsInChildren<EventSystem>(true))
            {
                eventSystem.gameObject.SetActive(false);
            }

            foreach (var listener in rootObject.GetComponentsInChildren<AudioListener>(true))
            {
                listener.enabled = false;
            }
        }
    }
}
