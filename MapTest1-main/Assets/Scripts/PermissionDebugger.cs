using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;

/// <summary>
/// Debug utility to test and verify permission requests on Magic Leap 2
/// </summary>
public class PermissionDebugger : MonoBehaviour
{
    [Header("Debug UI")]
    [SerializeField] private Button debugPermissionButton;
    [SerializeField] private Text debugStatusText;
    
    // Permission constants
    private const string SPACE_MANAGER_PERMISSION = "com.magicleap.permission.SPACE_MANAGER";
    private const string SPACE_IMPORT_EXPORT_PERMISSION = "com.magicleap.permission.SPACE_IMPORT_EXPORT";
    private const string SPATIAL_ANCHOR_PERMISSION = "com.magicleap.permission.SPATIAL_ANCHOR";
    
    private void Start()
    {
        if (debugPermissionButton != null)
        {
            debugPermissionButton.onClick.AddListener(TestPermissions);
        }
        
        UpdateDebugStatus("Permission Debugger Ready");
    }
    
    public void TestPermissions()
    {
        UpdateDebugStatus("Testing permissions...");
        
        // Check current permission status
        bool spaceManager = Permission.HasUserAuthorizedPermission(SPACE_MANAGER_PERMISSION);
        bool spaceImportExport = Permission.HasUserAuthorizedPermission(SPACE_IMPORT_EXPORT_PERMISSION);
        bool spatialAnchor = Permission.HasUserAuthorizedPermission(SPATIAL_ANCHOR_PERMISSION);
        
        Debug.Log($"[PermissionDebugger] Current Status:");
        Debug.Log($"[PermissionDebugger] SPACE_MANAGER: {spaceManager}");
        Debug.Log($"[PermissionDebugger] SPACE_IMPORT_EXPORT: {spaceImportExport}");
        Debug.Log($"[PermissionDebugger] SPATIAL_ANCHOR: {spatialAnchor}");
        
        string status = $"Permissions Status:\n" +
                       $"SPACE_MANAGER: {(spaceManager ? "✓" : "✗")}\n" +
                       $"SPACE_IMPORT_EXPORT: {(spaceImportExport ? "✓" : "✗")}\n" +
                       $"SPATIAL_ANCHOR: {(spatialAnchor ? "✓" : "✗")}";
        
        UpdateDebugStatus(status);
        
        // Request missing permissions one by one
        if (!spaceManager)
        {
            Debug.Log("[PermissionDebugger] Requesting SPACE_MANAGER permission");
            Permission.RequestUserPermission(SPACE_MANAGER_PERMISSION);
        }
        
        if (!spaceImportExport)
        {
            Debug.Log("[PermissionDebugger] Requesting SPACE_IMPORT_EXPORT permission");
            Permission.RequestUserPermission(SPACE_IMPORT_EXPORT_PERMISSION);
        }
        
        if (!spatialAnchor)
        {
            Debug.Log("[PermissionDebugger] Requesting SPATIAL_ANCHOR permission");
            Permission.RequestUserPermission(SPATIAL_ANCHOR_PERMISSION);
        }
    }
    
    private void UpdateDebugStatus(string message)
    {
        if (debugStatusText != null)
        {
            debugStatusText.text = message;
        }
        
        Debug.Log($"[PermissionDebugger] {message}");
    }
    
    // Manual permission check for testing
    [ContextMenu("Check Permissions")]
    public void CheckPermissionsManually()
    {
        TestPermissions();
    }
    
    // Force request all permissions
    [ContextMenu("Request All Permissions")]
    public void RequestAllPermissions()
    {
        Debug.Log("[PermissionDebugger] Force requesting all permissions");
        
        Permission.RequestUserPermission(SPACE_MANAGER_PERMISSION);
        Permission.RequestUserPermission(SPACE_IMPORT_EXPORT_PERMISSION);
        Permission.RequestUserPermission(SPATIAL_ANCHOR_PERMISSION);
        
        UpdateDebugStatus("All permissions requested");
    }
}