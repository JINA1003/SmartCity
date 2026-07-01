using CesiumForUnity;
using Nova;
using NovaSamples.Effects;
using UnityEngine;

/// <summary>
/// Keeps Nova's background blur camera rendering the same Cesium view as the dynamic main camera.
/// </summary>
public class BackgroundBlurCameraBinder : MonoBehaviour
{
    [SerializeField] private Camera dynamicCamera;
    [SerializeField] private Camera backgroundCamera;
    [SerializeField] private BackgroundBlurGroup blurGroup;
    [SerializeField] private ScreenSpace screenSpace;
    [SerializeField] private CesiumCameraManager cesiumCameraManager;

    private void Awake()
    {
        ResolveReferences();
        Bind();
    }

    private void LateUpdate()
    {
        if (dynamicCamera == null || backgroundCamera == null)
        {
            ResolveReferences();
        }

        MatchBackgroundCamera();
    }

    private void ResolveReferences()
    {
        dynamicCamera ??= Camera.main;
        backgroundCamera ??= FindCameraByName("BackgroundCamera");
        blurGroup ??= GetComponent<BackgroundBlurGroup>();
        screenSpace ??= GetComponent<ScreenSpace>();
        cesiumCameraManager ??= FindFirstObjectByType<CesiumCameraManager>();
    }

    private void Bind()
    {
        if (screenSpace != null && dynamicCamera != null)
        {
            screenSpace.TargetCamera = dynamicCamera;
        }

        if (blurGroup != null)
        {
            blurGroup.BackgroundCamera = backgroundCamera;
            blurGroup.PropertyMatchCamera = dynamicCamera;
        }

        if (cesiumCameraManager != null && backgroundCamera != null &&
            !cesiumCameraManager.additionalCameras.Contains(backgroundCamera))
        {
            cesiumCameraManager.additionalCameras.Add(backgroundCamera);
        }

        MatchBackgroundCamera();
    }

    private void MatchBackgroundCamera()
    {
        if (dynamicCamera == null || backgroundCamera == null)
        {
            return;
        }

        backgroundCamera.cullingMask = dynamicCamera.cullingMask;
        backgroundCamera.clearFlags = dynamicCamera.clearFlags;
        backgroundCamera.backgroundColor = dynamicCamera.backgroundColor;
        backgroundCamera.nearClipPlane = dynamicCamera.nearClipPlane;
        backgroundCamera.farClipPlane = dynamicCamera.farClipPlane;
    }

    private static Camera FindCameraByName(string cameraName)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Camera camera in cameras)
        {
            if (camera.name == cameraName)
            {
                return camera;
            }
        }

        return null;
    }
}
