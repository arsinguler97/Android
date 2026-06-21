using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARSceneResetter : MonoBehaviour
{
    [SerializeField] private ARSession arSession;
    [SerializeField] private bool resetOnStart = true;

    private IEnumerator Start()
    {
        if (!resetOnStart)
        {
            yield break;
        }

        if (arSession == null)
        {
            arSession = FindFirstObjectByType<ARSession>();
        }

        if (arSession == null)
        {
            yield break;
        }

        arSession.Reset();
        yield return null;
        arSession.enabled = false;
        yield return null;
        arSession.enabled = true;
    }
}
