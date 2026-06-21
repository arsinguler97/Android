using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TapToLoadScene : MonoBehaviour
{
    [SerializeField] private string sceneName = "ScanScene";

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            LoadTargetScene();
        }
    }

    public void LoadTargetScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("TapToLoadScene needs a sceneName.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
