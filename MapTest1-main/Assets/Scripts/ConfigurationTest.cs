using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

/// <summary>
/// Simple test script to verify Magic Leap 2 Space configuration is working
/// </summary>
public class ConfigurationTest : MonoBehaviour
{
    void Start()
    {
        // Test XR initialization
        var xrGeneralSettings = XRGeneralSettings.Instance;
        if (xrGeneralSettings != null && xrGeneralSettings.Manager != null)
        {
            Debug.Log("✅ XR Management is initialized");
            
            var activeLoader = xrGeneralSettings.Manager.activeLoader;
            if (activeLoader != null)
            {
                Debug.Log($"✅ Active XR Loader: {activeLoader.GetType().Name}");
            }
            else
            {
                Debug.LogWarning("⚠️ No active XR loader found");
            }
        }
        else
        {
            Debug.LogError("❌ XR Management is not initialized");
        }
        
        // Test OpenXR compilation
        #if UNITY_XR_OPENXR
        Debug.Log("✅ OpenXR is enabled in build");
        #else
        Debug.LogWarning("⚠️ OpenXR is not enabled in build");
        #endif
        
        // Test Magic Leap compilation
        #if MAGICLEAP
        Debug.Log("✅ Magic Leap platform detected");
        #else
        Debug.Log("ℹ️ Magic Leap platform not detected (normal for editor)");
        #endif
        
        Debug.Log("Configuration test completed");
    }
}