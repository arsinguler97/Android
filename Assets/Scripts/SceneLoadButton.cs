using UnityEngine;

public class SceneLoadButton : MonoBehaviour
{
    [SerializeField] private string sceneName = "ScanScene";

    public void LoadTargetScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("SceneLoadButton needs a sceneName.", this);
            return;
        }

        AppSceneLoader.Load(sceneName);
    }
}
