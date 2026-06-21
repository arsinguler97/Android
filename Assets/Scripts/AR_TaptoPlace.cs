using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class AR_TaptoPlace : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;

    private static readonly List<ARRaycastHit> Hits = new();

    private void Awake()
    {
        if (raycastManager == null)
        {
            raycastManager = FindFirstObjectByType<ARRaycastManager>();
        }

    }

    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame)
        {
            return;
        }

        TryPlace(pointer.position.ReadValue());
    }



    private void PlaceOnPlane(ARRaycastHit hit)
    {
        Transform trackedPlane = hit.trackable != null ? hit.trackable.transform : null;
        transform.SetParent(trackedPlane, true);
        transform.SetPositionAndRotation(hit.pose.position, hit.pose.rotation);
    }


    private void TryPlace(Vector2 screenPosition)
    {
        if (raycastManager != null &&
            raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
        {
            PlaceOnPlane(Hits[0]);
            return;
        }

#if UNITY_EDITOR
        Camera arCamera = Camera.main;
        if (arCamera != null && Physics.Raycast(arCamera.ScreenPointToRay(screenPosition), out RaycastHit hit))
        {
            PlaceOnEnvironmentSurface(hit);
        }
#endif
    }

#if UNITY_EDITOR
    private void PlaceOnEnvironmentSurface(RaycastHit hit)
    {
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
    }
#endif
}

