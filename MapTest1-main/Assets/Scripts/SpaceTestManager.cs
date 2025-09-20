using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.Android;
using MagicLeap.OpenXR.Features.LocalizationMaps;
using System.IO;
using System.Linq;
using UnityEngine.XR.OpenXR;
using System;

/// <summary>
/// Magic Leap 2スペースのエクスポート/インポート/ローカライゼーション機能を管理するメインクラス
/// WebDAVサーバーとの連携でスペース共有を実現
/// 
/// 【移植時の注意点】
/// - Magic Leap 2 SDKが必要（MagicLeap.OpenXR.Features.LocalizationMaps）
/// - UI要素（Button, Text）をインスペクターで設定必要
/// - WebDAVSpaceManagerを同じGameObjectにアタッチして参照設定
/// - スペース管理権限が必要（AndroidManifest.xml）
/// 
/// 【主な機能】
/// - スペースのエクスポート（ローカル + WebDAV）
/// - スペースのインポート（WebDAV優先、ローカルフォールバック）
/// - スペースへのローカライゼーション
/// - 重複クリック防止機能
/// </summary>
using System.Collections;
public class SpaceTestManager : MonoBehaviour
{

    // 操作重複防止用フラグ（Magic Leap 2のタッチ入力の重複クリック対策）
    private bool isExportInProgress = false;  // エクスポート処理中フラグ
    private bool isImportInProgress = false;  // インポート処理中フラグ
    [Header("UI Elements")]  // UI要素（Unity Editorで設定）
    [SerializeField] private Button exportButton;  // スペースエクスポートボタン
    [SerializeField] private Button importButton;  // スペースインポートボタン
    [SerializeField] private Button localizeButton;  // ローカライゼーションボタン
    [SerializeField] private Text statusText;  // ステータス表示用テキスト

    [Header("WebDAV Integration")]  // WebDAV連携設定
    [SerializeField] private WebDAVSpaceManager webdavManager;  // WebDAV管理コンポーネント（オプション）

    // Permission constants - Magic Leap 2 official permission names
    private static readonly string[] SPACE_MANAGER_PERMISSIONS = {
        "com.magicleap.permission.SPACE_MANAGER",
        "android.permission.WRITE_EXTERNAL_STORAGE", // Fallback for file operations
        "com.magicleap.permission.SPACE_MANAGER_PERMISSION"
    };
    
    private static readonly string[] SPACE_IMPORT_EXPORT_PERMISSIONS = {
        "com.magicleap.permission.SPACE_IMPORT_EXPORT",
        "android.permission.WRITE_EXTERNAL_STORAGE", // File write access
        "android.permission.READ_EXTERNAL_STORAGE"   // File read access for /sdcard/Download
    };
    
    private static readonly string[] SPATIAL_ANCHOR_PERMISSIONS = {
        "com.magicleap.permission.SPATIAL_ANCHOR",
        "com.magicleap.permission.SPATIAL_ANCHORS",
        "android.permission.ACCESS_FINE_LOCATION" // Spatial anchors may need location
    };

    private static readonly string[] NETWORK_PERMISSIONS = {
        "android.permission.INTERNET",
        "android.permission.ACCESS_NETWORK_STATE"
    };

    // Current working permission strings
    private string workingSpaceManagerPermission = null;
    private string workingSpaceImportExportPermission = null;
    private string workingSpatialAnchorPermission = null;
    private string workingNetworkPermission = null;

    // Permission status tracking
    private bool permissionsGranted = false;
    
    // File path for space export/import - using accessible Android paths
    private string SPACE_FILE_PATH
    {
        get
        {
            // Try multiple paths in order of preference for Magic Leap 2
            string[] possiblePaths = {
                // 1. Unity's persistent data path (most reliable)
                Path.Combine(Application.persistentDataPath, "unity_exported_space.zip"),
                // 2. External files directory (if available)
                Path.Combine(Application.temporaryCachePath, "unity_exported_space.zip"),
                // 3. Documents directory alternative
                Path.Combine("/sdcard/Documents", "unity_exported_space.zip")
            };
            
            // Return the first accessible path
            foreach (string path in possiblePaths)
            {
                try
                {
                    string directory = Path.GetDirectoryName(path);
                    if (Directory.Exists(directory) || CanCreateDirectory(directory))
                    {
                        Debug.Log($"Using accessible file path: {path}");
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Path {path} not accessible: {ex.Message}");
                }
            }
            
            // Fallback to persistent data path
            Debug.LogWarning("Using fallback persistent data path");
            return Path.Combine(Application.persistentDataPath, "unity_exported_space.zip");
        }
    }
    
    // Magic Leap Localization Map Feature reference
    private MagicLeapLocalizationMapFeature localizationMapFeature;
    
    // Localization state tracking
    private string lastImportedSpaceId = null;
    private bool isLocalizationInProgress = false;
    private TaskCompletionSource<(bool success, string errorMessage)> localizationTaskCompletionSource = null;
    private string currentLocalizationTargetId = null;
    
    

    private async void Start()
    {
        Debug.Log("[LOCALIZATION] Start method called");

        // Initialize WebDAV manager if not assigned
        if (webdavManager == null)
        {
            webdavManager = GetComponent<WebDAVSpaceManager>();
            if (webdavManager != null)
            {
                Debug.Log("[SpaceTestManager] WebDAVSpaceManager found and initialized");
            }
            else
            {
                Debug.LogWarning("[SpaceTestManager] WebDAVSpaceManager not found. WebDAV features will be disabled.");
            }
        }

        // Initialize Magic Leap Localization Map Feature
        InitializeLocalizationMapFeature();
        
        // Initialize UI and setup button listeners
        SetupUI();
        
        
        
        
        
        // Wait a moment for UI to fully initialize
        await Task.Delay(500);
        
        // Check and request permissions
        Debug.Log("Starting permission check and request process...");
        await CheckAndRequestPermissionsAsync();
    }
    
    private void InitializeLocalizationMapFeature()
    {
        try
        {
            localizationMapFeature = OpenXRSettings.Instance.GetFeature<MagicLeapLocalizationMapFeature>();
            if (localizationMapFeature == null)
            {
                Debug.LogError("MagicLeapLocalizationMapFeature not found. Make sure it's enabled in OpenXR settings.");
                HandleError("ローカライゼーションマップ機能の初期化", "OpenXR設定でMagic Leap 2 Localization Mapsが有効になっていません", OperationResult.FeatureNotAvailable);
            }
            else
            {
                Debug.Log("[LOCALIZATION] *** INIT *** MagicLeapLocalizationMapFeature initialized successfully");
                
                // CRITICAL: Enable localization events before subscribing (required by API)
                Debug.Log("[LOCALIZATION] *** INIT *** Enabling localization events...");
                var enableResult = localizationMapFeature.EnableLocalizationEvents(true);
                Debug.Log($"[LOCALIZATION] *** INIT *** EnableLocalizationEvents result: {enableResult}");
                
                if (enableResult != UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success)
                {
                    Debug.LogError($"[LOCALIZATION] *** INIT *** Failed to enable localization events: {enableResult}");
                }
                
                // Subscribe to localization changed event
                Debug.Log("[LOCALIZATION] *** INIT *** Subscribing to OnLocalizationChangedEvent");
                
                // Try static event subscription with enhanced debugging
                try 
                {
                    // Static event subscription (only available approach)
                    MagicLeapLocalizationMapFeature.OnLocalizationChangedEvent += OnLocalizationChanged;
                    Debug.Log("[LOCALIZATION] *** INIT *** Static event subscription completed");
                    
                    // Verify the event exists and we can subscribe
                    Debug.Log($"[LOCALIZATION] *** INIT *** Feature instance: {localizationMapFeature != null}");
                    Debug.Log($"[LOCALIZATION] *** INIT *** Event handler count: Checking if events work...");
                    
                    // Test event subscription by creating a dummy event (if possible)
                    // This will help us verify if the event system is working at all
                    Debug.Log($"[LOCALIZATION] *** INIT *** Event subscription test completed");
                }
                catch (Exception eventEx)
                {
                    Debug.LogError($"[LOCALIZATION] *** INIT *** Event subscription error: {eventEx.Message}");
                    Debug.LogError($"[LOCALIZATION] *** INIT *** Event subscription stack trace: {eventEx.StackTrace}");
                }
                
                // Verify subscription
                Debug.Log($"[LOCALIZATION] *** INIT *** All event subscriptions completed");
            }
        }
        catch (Exception ex)
        {
            HandleError("ローカライゼーションマップ機能の初期化", ex, OperationResult.InitializationError);
        }
    }

    private void SetupUI()
    {
        // Setup button event listeners
        if (exportButton != null)
        {
            exportButton.onClick.AddListener(OnExportButtonClicked);
            
            // Add XR Interaction support for Export button
            var exportXRInteractable = exportButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (exportXRInteractable != null)
            {
                exportXRInteractable.selectEntered.AddListener((args) => OnExportButtonClicked());
            }
        }

        if (importButton != null)
        {
            importButton.onClick.AddListener(OnImportButtonClicked);
            
            // Add XR Interaction support for Import button
            var importXRInteractable = importButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (importXRInteractable != null)
            {
                importXRInteractable.selectEntered.AddListener((args) => OnImportButtonClicked());
            }
        }

        if (localizeButton != null)
        {
            localizeButton.onClick.AddListener(OnLocalizeButtonClicked);
            
            // Add XR Interaction support for Localize button
            var localizeXRInteractable = localizeButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (localizeXRInteractable != null)
            {
                localizeXRInteractable.selectEntered.AddListener((args) => OnLocalizeButtonClicked());
            }
        }

        // Initially disable buttons until permissions are granted
        DisableSpaceFunctions();

        // Initialize status text
        UpdateProgressStatus(StatusMessages.INITIALIZING);
    }
    
    
    
    
    
    
    
    
    
    

    // Button event handlers

    /// <summary>
    /// スペースエクスポートボタンのクリックハンドラ
    /// ローカル保存後、WebDAVが有効な場合はアップロード
    /// </summary>
    public void OnExportButtonClicked()
    {
        // Prevent duplicate clicks
        if (isExportInProgress)
        {
            return;
        }
        
        StartCoroutine(OnExportButtonClickedCoroutine());
    }

    private System.Collections.IEnumerator OnExportButtonClickedCoroutine()
    {
        // Set flag to prevent duplicate execution
        isExportInProgress = true;
        
        try
        {
            // Run the async export process
            var task = OnExportButtonClickedAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }
        finally
        {
            // Always reset flag when done
            isExportInProgress = false;
        }
    }

    private async System.Threading.Tasks.Task OnExportButtonClickedAsync()
    {
        if (!permissionsGranted)
        {
            HandleError("Spaceエクスポート", "必要な権限が付与されていません", OperationResult.PermissionDenied);
            await CheckAndRequestPermissionsAsync();
            return;
        }

        if (localizationMapFeature == null)
        {
            HandleError("Spaceエクスポート", "ローカライゼーションマップ機能が初期化されていません", OperationResult.FeatureNotAvailable);
            return;
        }

        try
        {
            ShowOperationProgress("Spaceエクスポート", "最新のSpaceを検索中", 1, 3);

            // Get the latest (most recently created) space
            var latestSpace = await GetLatestSpaceAsync();
            if (latestSpace == null)
            {
                HandleError("Spaceエクスポート", "エクスポート可能なSpaceが見つかりません", OperationResult.NoSpaceFound);
                return;
            }

            string mapName = "名前なし"; // LocalizationMapにMapNameプロパティがない場合
            string mapId = latestSpace.Value.MapUUID;
            Debug.Log($"Found latest space: {mapId}, Name: {mapName}");
            ShowOperationProgress("Spaceエクスポート", $"Map: {mapName}\nID: {mapId}", 2, 3);

            // Export the space to binary data
            var exportResult = await ExportSpaceAsync(latestSpace.Value.MapUUID);
            if (!exportResult.success)
            {
                HandleError("Spaceエクスポート", exportResult.errorMessage, OperationResult.APIError);
                return;
            }

            ShowOperationProgress("Spaceエクスポート", "ファイルに保存中", 3, 3);

            // Save the exported data to file
            var saveResult = await SaveSpaceToFileAsync(exportResult.data);
            if (!saveResult.success)
            {
                HandleError("Spaceエクスポート", saveResult.errorMessage, OperationResult.FileAccessError);
                return;
            }

            // Upload to WebDAV if manager is available
            if (webdavManager != null)
            {
                ShowOperationProgress("Spaceエクスポート", "WebDAVにアップロード中", 4, 4);
                Debug.Log("[SpaceTestManager] Starting WebDAV upload...");

                // Use the new async method for proper synchronization
                await webdavManager.UploadSpaceDataAsync(exportResult.data, latestSpace.Value.MapUUID);
                ShowOperationSuccess("Spaceエクスポート",
                    $"エクスポート完了しました。\nMap: {mapName}\nローカル: {SPACE_FILE_PATH}\nWebDAV: unity_exported_space.zip としてアップロード完了");
                Debug.Log($"[SpaceTestManager] Space exported to local file and WebDAV upload completed as unity_exported_space.zip");
            }
            else
            {
                ShowOperationSuccess("Spaceエクスポート", $"エクスポート完了しました。\nMap: {mapName}\nファイルパス: {SPACE_FILE_PATH}");
                Debug.Log($"Space exported successfully to: {SPACE_FILE_PATH}");
            }
        }
        catch (Exception ex)
        {
            HandleError("Spaceエクスポート", ex, OperationResult.UnknownError);
        }
    }


    /// <summary>
    /// スペースインポートボタンのクリックハンドラ
    /// WebDAV優先、失敗時はローカルファイルから読み込み
    /// </summary>
    public void OnImportButtonClicked()
    {
        // Prevent duplicate clicks
        if (isImportInProgress)
        {
            return;
        }
        
        StartCoroutine(OnImportButtonClickedCoroutine());
    }

    private System.Collections.IEnumerator OnImportButtonClickedCoroutine()
    {
        // Set flag to prevent duplicate execution
        isImportInProgress = true;
        
        try
        {
            // Run the async import process
            var task = OnImportButtonClickedAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }
        finally
        {
            // Always reset flag when done
            isImportInProgress = false;
        }
    }

    private async System.Threading.Tasks.Task OnImportButtonClickedAsync()
    {
        if (!permissionsGranted)
        {
            HandleError("Spaceインポート", "必要な権限が付与されていません", OperationResult.PermissionDenied);
            await CheckAndRequestPermissionsAsync();
            return;
        }

        if (localizationMapFeature == null)
        {
            HandleError("Spaceインポート", "ローカライゼーションマップ機能が初期化されていません", OperationResult.FeatureNotAvailable);
            return;
        }

        try
        {
            byte[] spaceDataToImport = null;

            // Try to download from WebDAV first if manager is available
            if (webdavManager != null)
            {
                ShowOperationProgress("Space\u30a4\u30f3\u30dd\u30fc\u30c8", "WebDAV\u304b\u3089\u30c0\u30a6\u30f3\u30ed\u30fc\u30c9\u4e2d", 1, 2);
                Debug.Log("[SpaceTestManager] Attempting to download from WebDAV...");

                // Use the new async method for proper synchronization
                byte[] downloadedData = await webdavManager.DownloadSpaceDataAsync();

                if (downloadedData != null && downloadedData.Length > 0)
                {
                    spaceDataToImport = downloadedData;
                    Debug.Log($"[SpaceTestManager] Downloaded {downloadedData.Length} bytes from WebDAV");
                }
                else
                {
                    Debug.LogWarning("[SpaceTestManager] WebDAV download failed or returned empty data, falling back to local file");
                }
            }

            // Fallback to local file if WebDAV is not available or failed
            if (spaceDataToImport == null)
            {
                ShowOperationProgress("Spaceインポート", "ローカルファイルから読み込み中", 1, 2);

                // Read space data from file
                var fileReadResult = await ReadSpaceFromFileAsync();
                if (!fileReadResult.success)
                {
                    HandleError("Spaceインポート", fileReadResult.errorMessage, OperationResult.FileNotFound);
                    return;
                }
                spaceDataToImport = fileReadResult.data;
            }

            Debug.Log($"Space data ready for import. Data size: {spaceDataToImport.Length} bytes");
            ShowOperationProgress("Spaceインポート", $"データをインポート中\n({spaceDataToImport.Length:N0} bytes)", 2, 3);

            // Import the space data
            var importResult = await ImportSpaceAsync(spaceDataToImport);
            if (!importResult.success)
            {
                HandleError("Spaceインポート", importResult.errorMessage, OperationResult.APIError);
                return;
            }

            // Store the imported space ID for localization
            lastImportedSpaceId = importResult.spaceId;
            
            ShowOperationProgress("Spaceインポート", "インポート完了、Map情報を取得中", 3, 3);
            
            // Try to get the map name for the imported space
            string mapName = await GetMapNameByIdAsync(importResult.spaceId);
            string displayMapName = string.IsNullOrEmpty(mapName) ? "名前なし" : mapName;
            
            ShowOperationSuccess("Spaceインポート", $"インポート完了しました。\nMap: {displayMapName}\nID: {importResult.spaceId}");
            Debug.Log($"Space imported successfully with ID: {importResult.spaceId}");
        }
        catch (Exception ex)
        {
            HandleError("Spaceインポート", ex, OperationResult.UnknownError);
        }
    }

    public async void OnLocalizeButtonClicked()
    {
        Debug.Log("[LOCALIZATION] OnLocalizeButtonClicked started");
        Debug.Log($"[LOCALIZATION] permissionsGranted: {permissionsGranted}");
        Debug.Log($"[LOCALIZATION] localizationMapFeature: {localizationMapFeature}");
        
        if (!permissionsGranted)
        {
            HandleError("ローカライゼーション", "必要な権限が付与されていません", OperationResult.PermissionDenied);
            await CheckAndRequestPermissionsAsync();
            return;
        }

        if (localizationMapFeature == null)
        {
            Debug.LogError("[LOCALIZATION] Localization feature is null, re-initializing...");
            InitializeLocalizationMapFeature();
            
            if (localizationMapFeature == null)
            {
                HandleError("ローカライゼーション", "ローカライゼーションマップ機能が初期化できません", OperationResult.FeatureNotAvailable);
                return;
            }
        }

        if (isLocalizationInProgress)
        {
            HandleError("ローカライゼーション", "ローカライゼーション処理が既に実行中です", OperationResult.OperationInProgress);
            return;
        }

        try
        {
            ShowOperationProgress("ローカライゼーション", "対象Spaceを検索中", 1, 2);
            
            // Determine which space to localize to
            string targetSpaceId = await GetTargetSpaceForLocalizationAsync();
            if (string.IsNullOrEmpty(targetSpaceId))
            {
                HandleError("ローカライゼーション", "ローカライゼーション対象のSpaceが見つかりません", OperationResult.NoSpaceFound);
                return;
            }

            // Get map name for display
            string localizationMapName = await GetMapNameByIdAsync(targetSpaceId);
            string displayName = string.IsNullOrEmpty(localizationMapName) ? "名前なし" : localizationMapName;
            
            ShowOperationProgress("ローカライゼーション", $"Map: {displayName}\nID: {targetSpaceId}", 2, 3);
            Debug.Log($"Starting localization to space: {targetSpaceId}, Name: {localizationMapName}");

            // Check current localization state before starting
            Debug.Log("[LOCALIZATION] *** PRE-CHECK *** Checking current localization state...");
            
            // Get current localization map list to verify the target exists
            var mapResult = localizationMapFeature.GetLocalizationMapsList(out LocalizationMap[] availableMaps);
            Debug.Log($"[LOCALIZATION] *** PRE-CHECK *** Available maps result: {mapResult}");
            
            if (availableMaps != null && availableMaps.Length > 0)
            {
                Debug.Log($"[LOCALIZATION] *** PRE-CHECK *** Found {availableMaps.Length} available maps:");
                foreach (var map in availableMaps)
                {
                    Debug.Log($"[LOCALIZATION] *** PRE-CHECK *** Map ID: {map.MapUUID}, Type: {map.MapType}");
                }
                
                // Check if target space exists
                var targetExists = availableMaps.Any(m => m.MapUUID.Equals(targetSpaceId, StringComparison.OrdinalIgnoreCase));
                Debug.Log($"[LOCALIZATION] *** PRE-CHECK *** Target space {targetSpaceId} exists: {targetExists}");
            }
            else
            {
                Debug.LogWarning("[LOCALIZATION] *** PRE-CHECK *** No available maps found");
            }
            
            // Check current localization state
            try
            {
                var currentResult = localizationMapFeature.GetLocalizationMapsList(out LocalizationMap[] currentMaps);
                if (currentResult == UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success && currentMaps != null)
                {
                    var currentLocalizedMap = currentMaps.FirstOrDefault(m => m.MapType == MagicLeap.OpenXR.Features.LocalizationMaps.LocalizationMapType.OnDevice);
                    if (currentLocalizedMap.MapUUID != null)
                    {
                        Debug.Log($"[LOCALIZATION] *** PRE-CHECK *** Currently localized to: {currentLocalizedMap.MapUUID}");
                        
                        // Check if already localized to the target space
                        if (currentLocalizedMap.MapUUID.Equals(targetSpaceId, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log("[LOCALIZATION] *** PRE-CHECK *** Already localized to target space, but forcing re-localization for testing!");
                            ShowOperationProgress("ローカライゼーション", $"テスト: 再ローカライゼーションを強制実行\nMap: {displayName}", 2, 3);
                            // Continue with re-localization instead of returning
                        }
                    }
                    else
                    {
                        Debug.Log("[LOCALIZATION] *** PRE-CHECK *** Not currently localized");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LOCALIZATION] *** PRE-CHECK *** Error checking current state: {ex.Message}");
            }
            
            // Start localization process
            isLocalizationInProgress = true;
            currentLocalizationTargetId = targetSpaceId;
            var result = await RequestLocalizationAsync(targetSpaceId);
            
            if (!result.success)
            {
                isLocalizationInProgress = false;
                HandleError("ローカライゼーション", result.errorMessage, OperationResult.APIError);
                return;
            }

            ShowOperationProgress("ローカライゼーション", "ローカライゼーション結果を待機中", 3, 3);
            Debug.Log("Localization request sent successfully, waiting for result...");
            
            // Update button to cancel mode
            UpdateLocalizationButtonState(true);

            // Wait for localization result with timeout
            var localizationResult = await WaitForLocalizationResultAsync(targetSpaceId, 30000); // 30 seconds timeout
            
            isLocalizationInProgress = false;
            
            // Reset button state regardless of result
            UpdateLocalizationButtonState(false);
            
            if (!localizationResult.success)
            {
                HandleError("ローカライゼーション", localizationResult.errorMessage, OperationResult.TimeoutError);
                return;
            }

            // Success is handled in OnLocalizationChanged callback
            Debug.Log("Localization completed successfully!");
        }
        catch (Exception ex)
        {
            isLocalizationInProgress = false;
            UpdateLocalizationButtonState(false); // Reset button state on error
            HandleError("ローカライゼーション", ex, OperationResult.UnknownError);
        }
    }

    // Space export helper methods
    private Task<LocalizationMap?> GetLatestSpaceAsync()
    {
        try
        {
            // Get all available localization maps
            var result = localizationMapFeature.GetLocalizationMapsList(out LocalizationMap[] maps);
            
            if (result != UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success || maps == null || maps.Length == 0)
            {
                Debug.LogWarning("No localization maps found");
                return Task.FromResult<LocalizationMap?>(null);
            }

            // Return the first available map (since we can't determine timestamp without additional API)
            // In a real implementation, you might need to use additional metadata to determine the "latest"
            LocalizationMap latestMap = maps[0];
            Debug.Log($"Found {maps.Length} localization maps, using first one: {latestMap.MapUUID}");
            
            return Task.FromResult<LocalizationMap?>(latestMap);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting latest space: {ex.Message}");
            throw;
        }
    }

    private Task<(bool success, byte[] data, string errorMessage)> ExportSpaceAsync(string mapId)
    {
        try
        {
            Debug.Log($"Attempting to export space with ID: {mapId}");
            
            // Use the MagicLeapLocalizationMapFeature to export the localization map
            var result = localizationMapFeature.ExportLocalizationMap(mapId, out byte[] mapData);
            
            if (result == UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success)
            {
                if (mapData != null && mapData.Length > 0)
                {
                    Debug.Log($"Space exported successfully. Data size: {mapData.Length} bytes");
                    return Task.FromResult((true, mapData, (string)null));
                }
                else
                {
                    return Task.FromResult((false, (byte[])null, "エクスポートされたデータが空です"));
                }
            }
            else
            {
                return Task.FromResult((false, (byte[])null, $"エクスポートAPIが失敗しました: {result}"));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during space export: {ex.Message}");
            return Task.FromResult((false, (byte[])null, ex.Message));
        }
    }

    private async Task<(bool success, string errorMessage)> SaveSpaceToFileAsync(byte[] spaceData)
    {
        try
        {
            string filePath = SPACE_FILE_PATH;
            string directory = Path.GetDirectoryName(filePath);
            
            Debug.Log($"Saving space data to: {filePath}");
            Debug.Log($"Directory: {directory}");
            Debug.Log($"Data size: {spaceData?.Length ?? 0} bytes");
            
            // Ensure the parent directory exists
            if (!Directory.Exists(directory))
            {
                Debug.Log($"Creating directory: {directory}");
                Directory.CreateDirectory(directory);
            }

            // Write the space data to the file
            await Task.Run(() => File.WriteAllBytes(filePath, spaceData));
            
            // Verify the file was written correctly
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                Debug.Log($"Space file saved successfully to {filePath}. Size: {fileInfo.Length} bytes");
                return (true, null);
            }
            else
            {
                return (false, "ファイルの作成に失敗しました");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.LogError($"UnauthorizedAccessException: {ex.Message}");
            return (false, $"ファイルへの書き込み権限がありません: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            Debug.LogError($"DirectoryNotFoundException: {ex.Message}");
            return (false, $"ディレクトリが見つかりません: {ex.Message}");
        }
        catch (IOException ioEx)
        {
            Debug.LogError($"IOException: {ioEx.Message}");
            return (false, $"ファイルI/Oエラー: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"General Exception: {ex.Message}");
            return (false, $"予期しないエラー: {ex.Message}");
        }
    }

    // Space import helper methods
    private async Task<(bool success, byte[] data, string errorMessage)> ReadSpaceFromFileAsync()
    {
        try
        {
            string filePath = SPACE_FILE_PATH;
            Debug.Log($"Reading space data from: {filePath}");
            
            // Check if the space file exists
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"Space file not found at: {filePath}");
                return (false, null, $"インポート用ファイルが見つかりません: {filePath}");
            }

            // Read the space data from file
            byte[] spaceData = await Task.Run(() => File.ReadAllBytes(filePath));
            
            if (spaceData == null || spaceData.Length == 0)
            {
                return (false, null, "ファイルが空またはデータが読み込めません");
            }

            Debug.Log($"Space file read successfully from {filePath}. Size: {spaceData.Length} bytes");
            return (true, spaceData, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.LogError($"UnauthorizedAccessException during read: {ex.Message}");
            return (false, null, $"ファイルへの読み込み権限がありません: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            Debug.LogError($"DirectoryNotFoundException during read: {ex.Message}");
            return (false, null, $"ディレクトリが見つかりません: {ex.Message}");
        }
        catch (FileNotFoundException ex)
        {
            Debug.LogError($"FileNotFoundException during read: {ex.Message}");
            return (false, null, $"インポート用ファイルが見つかりません: {ex.Message}");
        }
        catch (IOException ioEx)
        {
            Debug.LogError($"IOException during read: {ioEx.Message}");
            return (false, null, $"ファイルI/Oエラー: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"General Exception during read: {ex.Message}");
            return (false, null, $"予期しないエラー: {ex.Message}");
        }
    }

    private Task<(bool success, string spaceId, string errorMessage)> ImportSpaceAsync(byte[] spaceData)
    {
        try
        {
            Debug.Log($"Attempting to import space data. Data size: {spaceData.Length} bytes");
            
            // Use the MagicLeapLocalizationMapFeature to import the localization map
            var result = localizationMapFeature.ImportLocalizationMap(spaceData, out string importedMapId);
            
            if (result == UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success)
            {
                if (!string.IsNullOrEmpty(importedMapId))
                {
                    Debug.Log($"Space imported successfully with ID: {importedMapId}");
                    return Task.FromResult((true, importedMapId, (string)null));
                }
                else
                {
                    return Task.FromResult((false, (string)null, "インポートされたSpace IDが空です"));
                }
            }
            else
            {
                return Task.FromResult((false, (string)null, $"インポートAPIが失敗しました: {result}"));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during space import: {ex.Message}");
            return Task.FromResult((false, (string)null, ex.Message));
        }
    }

    // Permission management methods
    private async Task<bool> CheckAndRequestPermissionsAsync()
    {
        UpdateProgressStatus(StatusMessages.CHECKING_PERMISSIONS);
        
        try
        {
            // Find working permission strings first
            await FindWorkingPermissionStrings();
            
            // Check if all required permissions are granted
            bool spaceManagerGranted = CheckPermissionGranted(workingSpaceManagerPermission);
            bool spaceImportExportGranted = CheckPermissionGranted(workingSpaceImportExportPermission);
            bool spatialAnchorGranted = CheckPermissionGranted(workingSpatialAnchorPermission);
            bool networkGranted = CheckPermissionGranted(workingNetworkPermission);

            Debug.Log($"[PermissionCheck] Permission Status - SPACE_MANAGER: {spaceManagerGranted}, SPACE_IMPORT_EXPORT: {spaceImportExportGranted}, SPATIAL_ANCHOR: {spatialAnchorGranted}, NETWORK: {networkGranted}");
            Debug.Log($"[PermissionCheck] Working permissions - SPACE_MANAGER: {workingSpaceManagerPermission}, SPACE_IMPORT_EXPORT: {workingSpaceImportExportPermission}, SPATIAL_ANCHOR: {workingSpatialAnchorPermission}, NETWORK: {workingNetworkPermission}");

            Debug.LogWarning($"[PermissionCheck] FORCE LOG - Individual results: SM={spaceManagerGranted}, IE={spaceImportExportGranted}, SA={spatialAnchorGranted}, NET={networkGranted}");

            if (spaceManagerGranted && spaceImportExportGranted && spatialAnchorGranted && networkGranted)
            {
                permissionsGranted = true;
                UpdateStatus(StatusMessages.PERMISSIONS_GRANTED);
                EnableSpaceFunctions();
                Debug.LogWarning("[PermissionCheck] FORCE LOG - All permissions granted, returning true");
                return true;
            }
            
            Debug.LogWarning("[PermissionCheck] FORCE LOG - Not all permissions granted, proceeding to request");

            // Request missing permissions one by one with proper waiting
            // Start with SPACE_IMPORT_EXPORT since it's working
            UpdateProgressStatus(StatusMessages.REQUESTING_PERMISSIONS);
            
            if (!spaceImportExportGranted && !string.IsNullOrEmpty(workingSpaceImportExportPermission))
            {
                ShowOperationProgress("権限要求", "SPACE_IMPORT_EXPORT権限を要求中", 1, 4);
                Debug.Log($"Requesting SPACE_IMPORT_EXPORT permission: {workingSpaceImportExportPermission}");
                Permission.RequestUserPermission(workingSpaceImportExportPermission);
                await WaitForPermissionResponse(workingSpaceImportExportPermission);
                
                // Check if permission was granted
                bool granted = CheckPermissionGranted(workingSpaceImportExportPermission);
                Debug.Log($"SPACE_IMPORT_EXPORT permission result: {granted}");
            }
            
            // Request external storage permissions explicitly
            ShowOperationProgress("権限要求", "外部ストレージアクセス権限を要求中", 2, 4);
            
            // Request WRITE_EXTERNAL_STORAGE
            if (!Permission.HasUserAuthorizedPermission("android.permission.WRITE_EXTERNAL_STORAGE"))
            {
                Debug.Log("Requesting WRITE_EXTERNAL_STORAGE permission");
                Permission.RequestUserPermission("android.permission.WRITE_EXTERNAL_STORAGE");
                await WaitForPermissionResponse("android.permission.WRITE_EXTERNAL_STORAGE");
            }
            
            // Request READ_EXTERNAL_STORAGE
            if (!Permission.HasUserAuthorizedPermission("android.permission.READ_EXTERNAL_STORAGE"))
            {
                Debug.Log("Requesting READ_EXTERNAL_STORAGE permission");
                Permission.RequestUserPermission("android.permission.READ_EXTERNAL_STORAGE");
                await WaitForPermissionResponse("android.permission.READ_EXTERNAL_STORAGE");
            }
            
            Debug.Log($"External storage permissions - Write: {Permission.HasUserAuthorizedPermission("android.permission.WRITE_EXTERNAL_STORAGE")}, Read: {Permission.HasUserAuthorizedPermission("android.permission.READ_EXTERNAL_STORAGE")}");

            if (!spaceManagerGranted && !string.IsNullOrEmpty(workingSpaceManagerPermission))
            {
                ShowOperationProgress("権限要求", "SPACE_MANAGER権限を要求中", 3, 4);
                Debug.Log($"Requesting SPACE_MANAGER permission: {workingSpaceManagerPermission}");
                
                // Try all variants for SPACE_MANAGER
                bool permissionGranted = false;
                foreach (string permission in SPACE_MANAGER_PERMISSIONS)
                {
                    Debug.Log($"Trying SPACE_MANAGER permission variant: {permission}");
                    Permission.RequestUserPermission(permission);
                    await WaitForPermissionResponse(permission);
                    
                    if (CheckPermissionGranted(permission))
                    {
                        workingSpaceManagerPermission = permission;
                        permissionGranted = true;
                        Debug.Log($"SPACE_MANAGER permission granted with: {permission}");
                        break;
                    }
                }
                
                if (!permissionGranted)
                {
                    Debug.LogWarning("SPACE_MANAGER permission not granted with any variant");
                }
            }

            if (!spatialAnchorGranted && !string.IsNullOrEmpty(workingSpatialAnchorPermission))
            {
                ShowOperationProgress("権限要求", "SPATIAL_ANCHOR権限を要求中", 4, 4);
                Debug.Log($"Requesting SPATIAL_ANCHOR permission: {workingSpatialAnchorPermission}");
                
                // Try all variants for SPATIAL_ANCHOR
                bool permissionGranted = false;
                foreach (string permission in SPATIAL_ANCHOR_PERMISSIONS)
                {
                    Debug.Log($"Trying SPATIAL_ANCHOR permission variant: {permission}");
                    Permission.RequestUserPermission(permission);
                    await WaitForPermissionResponse(permission);
                    
                    if (CheckPermissionGranted(permission))
                    {
                        workingSpatialAnchorPermission = permission;
                        permissionGranted = true;
                        Debug.Log($"SPATIAL_ANCHOR permission granted with: {permission}");
                        break;
                    }
                }
                
                if (!permissionGranted)
                {
                    Debug.LogWarning("SPATIAL_ANCHOR permission not granted with any variant");
                }
            }

            // Recheck permissions after requests
            bool allGranted = CheckPermissionGranted(workingSpaceManagerPermission) &&
                             CheckPermissionGranted(workingSpaceImportExportPermission) &&
                             CheckPermissionGranted(workingSpatialAnchorPermission);

            Debug.Log($"Final permission check - All granted: {allGranted}");

            if (allGranted)
            {
                permissionsGranted = true;
                Debug.LogWarning("[PermissionCheck] FORCE LOG - All permissions granted, updating UI");
                
                // Update status text first
                UpdateStatus(StatusMessages.PERMISSIONS_GRANTED);
                
                // Enable buttons
                EnableSpaceFunctions();
                
                Debug.LogWarning("[PermissionCheck] FORCE LOG - UI update completed");
                return true;
            }
            else
            {
                HandlePermissionDenied();
                return false;
            }
        }
        catch (System.Exception ex)
        {
            HandleError("権限チェック", ex, OperationResult.PermissionDenied);
            return false;
        }
    }
    
    // Find the correct permission strings for this device
    private async Task FindWorkingPermissionStrings()
    {
        Debug.Log("[PermissionDetection] Starting permission string detection...");
        Debug.LogWarning("[PermissionDetection] FORCE LOG - Starting permission detection process");
        
        // Find working SPACE_MANAGER permission
        if (string.IsNullOrEmpty(workingSpaceManagerPermission))
        {
            Debug.Log("[PermissionDetection] Testing SPACE_MANAGER permission variants...");
            foreach (string permission in SPACE_MANAGER_PERMISSIONS)
            {
                bool hasPermission = Permission.HasUserAuthorizedPermission(permission);
                Debug.Log($"[PermissionDetection] SPACE_MANAGER test: {permission} = {hasPermission}");
                Debug.LogWarning($"[PermissionDetection] FORCE LOG - SPACE_MANAGER {permission}: {hasPermission}");
                if (hasPermission)
                {
                    workingSpaceManagerPermission = permission;
                    Debug.Log($"[PermissionDetection] ✅ Found working SPACE_MANAGER permission: {permission}");
                    break;
                }
            }
            // If none are granted, use the first one for requesting
            if (string.IsNullOrEmpty(workingSpaceManagerPermission))
            {
                workingSpaceManagerPermission = SPACE_MANAGER_PERMISSIONS[0];
                Debug.Log($"[PermissionDetection] ❌ No granted SPACE_MANAGER found, using default: {workingSpaceManagerPermission}");
            }
        }
        else
        {
            Debug.Log($"[PermissionDetection] SPACE_MANAGER already set: {workingSpaceManagerPermission}");
        }
        
        // Find working SPACE_IMPORT_EXPORT permission
        if (string.IsNullOrEmpty(workingSpaceImportExportPermission))
        {
            Debug.Log("[PermissionDetection] Testing SPACE_IMPORT_EXPORT permission variants...");
            foreach (string permission in SPACE_IMPORT_EXPORT_PERMISSIONS)
            {
                bool hasPermission = Permission.HasUserAuthorizedPermission(permission);
                Debug.Log($"[PermissionDetection] SPACE_IMPORT_EXPORT test: {permission} = {hasPermission}");
                if (hasPermission)
                {
                    workingSpaceImportExportPermission = permission;
                    Debug.Log($"[PermissionDetection] ✅ Found working SPACE_IMPORT_EXPORT permission: {permission}");
                    break;
                }
            }
            // If none are granted, use the first one for requesting
            if (string.IsNullOrEmpty(workingSpaceImportExportPermission))
            {
                workingSpaceImportExportPermission = SPACE_IMPORT_EXPORT_PERMISSIONS[0];
                Debug.Log($"[PermissionDetection] ❌ No granted SPACE_IMPORT_EXPORT found, using default: {workingSpaceImportExportPermission}");
            }
        }
        else
        {
            Debug.Log($"[PermissionDetection] SPACE_IMPORT_EXPORT already set: {workingSpaceImportExportPermission}");
        }
        
        // Find working SPATIAL_ANCHOR permission
        if (string.IsNullOrEmpty(workingSpatialAnchorPermission))
        {
            Debug.Log("[PermissionDetection] Testing SPATIAL_ANCHOR permission variants...");
            foreach (string permission in SPATIAL_ANCHOR_PERMISSIONS)
            {
                bool hasPermission = Permission.HasUserAuthorizedPermission(permission);
                Debug.Log($"[PermissionDetection] SPATIAL_ANCHOR test: {permission} = {hasPermission}");
                if (hasPermission)
                {
                    workingSpatialAnchorPermission = permission;
                    Debug.Log($"[PermissionDetection] ✅ Found working SPATIAL_ANCHOR permission: {permission}");
                    break;
                }
            }
            // If none are granted, use the first one for requesting
            if (string.IsNullOrEmpty(workingSpatialAnchorPermission))
            {
                workingSpatialAnchorPermission = SPATIAL_ANCHOR_PERMISSIONS[0];
                Debug.Log($"[PermissionDetection] ❌ No granted SPATIAL_ANCHOR found, using default: {workingSpatialAnchorPermission}");
            }
        }
        else
        {
            Debug.Log($"[PermissionDetection] SPATIAL_ANCHOR already set: {workingSpatialAnchorPermission}");
        }

        // Find working NETWORK permission
        if (string.IsNullOrEmpty(workingNetworkPermission))
        {
            Debug.Log("[PermissionDetection] Testing NETWORK permission variants...");
            foreach (string permission in NETWORK_PERMISSIONS)
            {
                bool hasPermission = Permission.HasUserAuthorizedPermission(permission);
                Debug.Log($"[PermissionDetection] NETWORK test: {permission} = {hasPermission}");
                if (hasPermission)
                {
                    workingNetworkPermission = permission;
                    Debug.Log($"[PermissionDetection] ✅ Found working NETWORK permission: {permission}");
                    break;
                }
            }
            // If none are granted, use the first one for requesting
            if (string.IsNullOrEmpty(workingNetworkPermission))
            {
                workingNetworkPermission = NETWORK_PERMISSIONS[0];
                Debug.Log($"[PermissionDetection] ❌ No granted NETWORK found, using default: {workingNetworkPermission}");
            }
        }
        else
        {
            Debug.Log($"[PermissionDetection] NETWORK already set: {workingNetworkPermission}");
        }

        Debug.Log($"[PermissionDetection] Final results - SPACE_MANAGER: {workingSpaceManagerPermission}, SPACE_IMPORT_EXPORT: {workingSpaceImportExportPermission}, SPATIAL_ANCHOR: {workingSpatialAnchorPermission}, NETWORK: {workingNetworkPermission}");
        await Task.Delay(100); // Small delay to ensure logging is visible
    }
    
    // Helper method to check if a permission is granted
    private bool CheckPermissionGranted(string permission)
    {
        if (string.IsNullOrEmpty(permission))
            return false;
            
        return Permission.HasUserAuthorizedPermission(permission);
    }

    private async Task WaitForPermissionResponse(string permission)
    {
        // Wait for permission dialog response (simple polling approach)
        int maxWaitTime = 30; // 30 seconds timeout
        int waitTime = 0;
        
        Debug.Log($"[PermissionWait] Starting wait for permission: {permission}");
        
        while (waitTime < maxWaitTime)
        {
            await Task.Delay(1000); // Wait 1 second
            waitTime++;
            
            bool hasPermission = Permission.HasUserAuthorizedPermission(permission);
            
            if (waitTime % 5 == 0 || hasPermission) // Log every 5 seconds or when granted
            {
                Debug.Log($"[PermissionWait] Check {waitTime}/30 - {permission}: {hasPermission}");
            }
            
            if (hasPermission)
            {
                Debug.Log($"[PermissionWait] ✅ Permission granted: {permission}");
                break;
            }
        }
        
        if (waitTime >= maxWaitTime)
        {
            Debug.LogWarning($"[PermissionWait] ⏰ Permission request timeout for: {permission}");
        }
    }

    private void HandlePermissionDenied()
    {
        permissionsGranted = false;
        
        // Check which specific permissions were denied for better error messaging
        bool spaceManagerGranted = CheckPermissionGranted(workingSpaceManagerPermission);
        bool spaceImportExportGranted = CheckPermissionGranted(workingSpaceImportExportPermission);
        bool spatialAnchorGranted = CheckPermissionGranted(workingSpatialAnchorPermission);
        
        string deniedPermissions = "";
        if (!spaceManagerGranted) deniedPermissions += "SPACE_MANAGER ";
        if (!spaceImportExportGranted) deniedPermissions += "SPACE_IMPORT_EXPORT ";
        if (!spatialAnchorGranted) deniedPermissions += "SPATIAL_ANCHOR ";
        
        Debug.LogWarning($"Permissions denied: {deniedPermissions}");
        
        HandleError("権限要求", $"{ErrorMessages.PERMISSION_DENIED}\n拒否された権限: {deniedPermissions}", OperationResult.PermissionDenied);
        DisableSpaceFunctions();
        
        Debug.LogWarning("Space permissions denied. Space functionality will be disabled.");
    }

    private void EnableSpaceFunctions()
    {
        // Force UI update on main thread using Invoke
        if (this != null && gameObject != null)
        {
            Invoke(nameof(EnableSpaceFunctionsInternal), 0.1f);
        }
        else
        {
            EnableSpaceFunctionsInternal();
        }
    }
    
    private void EnableSpaceFunctionsInternal()
    {
        Debug.LogWarning("[UI Update] FORCE LOG - Starting EnableSpaceFunctions");
        
        if (exportButton != null) 
        {
            exportButton.interactable = true;
            // Enable XR Interaction as well
            var exportXRInteractable = exportButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (exportXRInteractable != null)
            {
                exportXRInteractable.enabled = true;
            }
            Debug.LogWarning($"[UI Update] FORCE LOG - Export button enabled: {exportButton.interactable}");
        }
        else
        {
            Debug.LogError("[UI Update] Export button is null!");
        }
        
        if (importButton != null) 
        {
            importButton.interactable = true;
            // Enable XR Interaction as well
            var importXRInteractable = importButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (importXRInteractable != null)
            {
                importXRInteractable.enabled = true;
            }
            Debug.LogWarning($"[UI Update] FORCE LOG - Import button enabled: {importButton.interactable}");
        }
        else
        {
            Debug.LogError("[UI Update] Import button is null!");
        }
        
        if (localizeButton != null) 
        {
            localizeButton.interactable = true;
            // Enable XR Interaction as well
            var localizeXRInteractable = localizeButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (localizeXRInteractable != null)
            {
                localizeXRInteractable.enabled = true;
            }
            Debug.LogWarning($"[UI Update] FORCE LOG - Localize button enabled: {localizeButton.interactable}");
        }
        else
        {
            Debug.LogError("[UI Update] Localize button is null!");
        }
        
        Debug.LogWarning("[UI Update] FORCE LOG - All space functions enabled successfully");
    }

    private void DisableSpaceFunctions()
    {
        if (exportButton != null) 
        {
            exportButton.interactable = false;
            // Disable XR Interaction as well
            var exportXRInteractable = exportButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (exportXRInteractable != null)
            {
                exportXRInteractable.enabled = false;
            }
        }
        
        if (importButton != null) 
        {
            importButton.interactable = false;
            // Disable XR Interaction as well
            var importXRInteractable = importButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (importXRInteractable != null)
            {
                importXRInteractable.enabled = false;
            }
        }
        
        if (localizeButton != null) 
        {
            localizeButton.interactable = false;
            // Disable XR Interaction as well
            var localizeXRInteractable = localizeButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (localizeXRInteractable != null)
            {
                localizeXRInteractable.enabled = false;
            }
        }
    }

    // Error message constants for consistent Japanese error messages
    private static class ErrorMessages
    {
        public const string PERMISSION_DENIED = "権限が拒否されました。設定から権限を有効にしてください。";
        public const string NO_SPACE_FOUND = "エクスポート可能なSpaceが見つかりません。";
        public const string FILE_NOT_FOUND = "インポート用ファイルが見つかりません。";
        public const string EXPORT_FAILED = "Spaceのエクスポートに失敗しました。";
        public const string IMPORT_FAILED = "Spaceのインポートに失敗しました。";
        public const string LOCALIZATION_FAILED = "ローカライゼーションに失敗しました。";
        public const string FEATURE_NOT_AVAILABLE = "ローカライゼーションマップ機能が利用できません。";
        public const string OPERATION_IN_PROGRESS = "処理が既に実行中です。完了をお待ちください。";
        public const string FILE_ACCESS_ERROR = "ファイルへのアクセスに失敗しました。";
        public const string NETWORK_ERROR = "ネットワークエラーが発生しました。";
        public const string UNKNOWN_ERROR = "予期しないエラーが発生しました。";
        public const string INITIALIZATION_ERROR = "初期化に失敗しました。";
        public const string API_ERROR = "APIの呼び出しに失敗しました。";
    }

    // Operation result enum for better error categorization
    public enum OperationResult
    {
        Success,
        PermissionDenied,
        NoSpaceFound,
        FileNotFound,
        APIError,
        NetworkError,
        FileAccessError,
        OperationInProgress,
        FeatureNotAvailable,
        InitializationError,
        TimeoutError,
        UnknownError
    }

    // Enhanced error handling method with categorization and detailed logging
    private void HandleError(string operation, System.Exception ex, OperationResult errorType = OperationResult.UnknownError)
    {
        string detailedErrorMessage = GetDetailedErrorMessage(operation, ex, errorType);
        string userFriendlyMessage = GetUserFriendlyErrorMessage(errorType, ex.Message);
        
        // Update UI with user-friendly message
        UpdateStatus(userFriendlyMessage);
        
        // Log detailed error information for debugging
        Debug.LogError($"SpaceTestManager Error - Operation: {operation}, Type: {errorType}, Details: {detailedErrorMessage}");
        Debug.LogError($"Exception Stack Trace: {ex.StackTrace}");
        
        // Additional logging for specific error types
        LogErrorDetails(operation, errorType, ex);
    }

    // Overloaded HandleError method for simple error messages without exceptions
    private void HandleError(string operation, string errorMessage, OperationResult errorType = OperationResult.UnknownError)
    {
        string userFriendlyMessage = GetUserFriendlyErrorMessage(errorType, errorMessage);
        
        // Update UI with user-friendly message
        UpdateStatus(userFriendlyMessage);
        
        // Log error information
        Debug.LogError($"SpaceTestManager Error - Operation: {operation}, Type: {errorType}, Message: {errorMessage}");
    }

    // Get detailed error message for logging purposes
    private string GetDetailedErrorMessage(string operation, System.Exception ex, OperationResult errorType)
    {
        return $"Operation: {operation} | Error Type: {errorType} | Exception: {ex.GetType().Name} | Message: {ex.Message}";
    }

    // Get user-friendly error message based on error type
    private string GetUserFriendlyErrorMessage(OperationResult errorType, string details = "")
    {
        string baseMessage = errorType switch
        {
            OperationResult.PermissionDenied => ErrorMessages.PERMISSION_DENIED,
            OperationResult.NoSpaceFound => ErrorMessages.NO_SPACE_FOUND,
            OperationResult.FileNotFound => ErrorMessages.FILE_NOT_FOUND,
            OperationResult.APIError => ErrorMessages.API_ERROR,
            OperationResult.NetworkError => ErrorMessages.NETWORK_ERROR,
            OperationResult.FileAccessError => ErrorMessages.FILE_ACCESS_ERROR,
            OperationResult.OperationInProgress => ErrorMessages.OPERATION_IN_PROGRESS,
            OperationResult.FeatureNotAvailable => ErrorMessages.FEATURE_NOT_AVAILABLE,
            OperationResult.InitializationError => ErrorMessages.INITIALIZATION_ERROR,
            _ => ErrorMessages.UNKNOWN_ERROR
        };

        // Add details if available and not too technical
        if (!string.IsNullOrEmpty(details) && ShouldShowDetailsToUser(errorType))
        {
            return $"{baseMessage}\n詳細: {details}";
        }

        return baseMessage;
    }

    // Determine if technical details should be shown to user
    private bool ShouldShowDetailsToUser(OperationResult errorType)
    {
        return errorType switch
        {
            OperationResult.FileNotFound => true,
            OperationResult.FileAccessError => true,
            OperationResult.NoSpaceFound => true,
            _ => false
        };
    }

    // Log additional error details based on error type
    private void LogErrorDetails(string operation, OperationResult errorType, System.Exception ex)
    {
        switch (errorType)
        {
            case OperationResult.FileAccessError:
                Debug.LogError($"File Access Error Details - Path: {SPACE_FILE_PATH}, Exception Type: {ex.GetType().Name}");
                break;
            case OperationResult.APIError:
                Debug.LogError($"API Error Details - Operation: {operation}, Feature Available: {localizationMapFeature != null}");
                break;
            case OperationResult.PermissionDenied:
                LogPermissionStatus();
                break;
        }
    }

    // Log current permission status for debugging
    private void LogPermissionStatus()
    {
        bool spaceManager = CheckPermissionGranted(workingSpaceManagerPermission);
        bool spaceImportExport = CheckPermissionGranted(workingSpaceImportExportPermission);
        bool spatialAnchor = CheckPermissionGranted(workingSpatialAnchorPermission);
        
        Debug.LogError($"Permission Status - SPACE_MANAGER ({workingSpaceManagerPermission}): {spaceManager}, SPACE_IMPORT_EXPORT ({workingSpaceImportExportPermission}): {spaceImportExport}, SPATIAL_ANCHOR ({workingSpatialAnchorPermission}): {spatialAnchor}");
    }

    // Localization helper methods
    private async Task<string> GetTargetSpaceForLocalizationAsync()
    {
        try
        {
            // First priority: use the last imported space if available
            if (!string.IsNullOrEmpty(lastImportedSpaceId))
            {
                Debug.Log($"Using last imported space for localization: {lastImportedSpaceId}");
                return lastImportedSpaceId;
            }

            // Fallback: get the latest available space
            var latestSpace = await GetLatestSpaceAsync();
            if (latestSpace.HasValue)
            {
                Debug.Log($"Using latest available space for localization: {latestSpace.Value.MapUUID}");
                return latestSpace.Value.MapUUID;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting target space for localization: {ex.Message}");
            throw;
        }
    }

    private Task<(bool success, string errorMessage)> RequestLocalizationAsync(string spaceId)
    {
        try
        {
            Debug.Log($"[LOCALIZATION] *** REQUEST *** Requesting localization to space: {spaceId}");
            
            // Check if localization feature is available
            if (localizationMapFeature == null)
            {
                Debug.LogError("[LOCALIZATION] *** ERROR *** LocalizationMapFeature is null");
                return Task.FromResult((false, "ローカライゼーション機能が初期化されていません"));
            }
            
            // Store the target for tracking
            currentLocalizationTargetId = spaceId;
            
            // Use the MagicLeapLocalizationMapFeature to request localization
            var result = localizationMapFeature.RequestMapLocalization(spaceId);
            
            Debug.Log($"[LOCALIZATION] *** REQUEST RESULT *** API Result: {result}");
            Debug.Log($"[LOCALIZATION] *** REQUEST RESULT *** Target ID stored: {currentLocalizationTargetId}");
            
            // Also try alternative approaches for debugging
            try
            {
                // Check current localization state immediately after request
                var postRequestResult = localizationMapFeature.GetLocalizationMapsList(out LocalizationMap[] postRequestMaps);
                Debug.Log($"[LOCALIZATION] *** POST-REQUEST *** Available maps after request: {postRequestResult}");
                
                if (postRequestMaps != null)
                {
                    foreach (var map in postRequestMaps)
                    {
                        Debug.Log($"[LOCALIZATION] *** POST-REQUEST *** Map: {map.MapUUID}, Type: {map.MapType}");
                    }
                }
            }
            catch (Exception checkEx)
            {
                Debug.LogError($"[LOCALIZATION] *** POST-REQUEST *** Error checking state: {checkEx.Message}");
            }
            
            if (result == UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success)
            {
                Debug.Log("[LOCALIZATION] *** REQUEST SUCCESS *** Localization request sent successfully");
                return Task.FromResult((true, (string)null));
            }
            else
            {
                Debug.LogError($"[LOCALIZATION] *** REQUEST FAILED *** API Result: {result}");
                return Task.FromResult((false, $"ローカライゼーション要求APIが失敗しました: {result}"));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOCALIZATION] *** EXCEPTION *** Exception during localization request: {ex.Message}");
            Debug.LogError($"[LOCALIZATION] *** EXCEPTION *** Stack trace: {ex.StackTrace}");
            return Task.FromResult((false, ex.Message));
        }
    }

    private void OnLocalizationChanged(LocalizationEventData eventData)
    {
        try
        {
            Debug.Log($"[LOCALIZATION] *** EVENT *** State: {eventData.State}, Map: {eventData.Map.MapUUID}");
            Debug.Log($"[LOCALIZATION] Current target: {currentLocalizationTargetId}, Event map: {eventData.Map.MapUUID}");
            
            // Check if this event is for our current localization request
            bool isRelevantEvent = !string.IsNullOrEmpty(currentLocalizationTargetId) && 
                                 eventData.Map.MapUUID.Equals(currentLocalizationTargetId, StringComparison.OrdinalIgnoreCase);
            
            Debug.Log($"[LOCALIZATION] Target: '{currentLocalizationTargetId}', Event map: '{eventData.Map.MapUUID}'");
            Debug.Log($"[LOCALIZATION] Has target: {!string.IsNullOrEmpty(currentLocalizationTargetId)}");
            Debug.Log($"[LOCALIZATION] Maps match: {eventData.Map.MapUUID.Equals(currentLocalizationTargetId, StringComparison.OrdinalIgnoreCase)}");
            
            Debug.Log($"[LOCALIZATION] Is relevant event: {isRelevantEvent}");
            
            switch (eventData.State)
            {
                case LocalizationMapState.Localized:
                    Debug.Log("[LOCALIZATION] *** SUCCESS *** State: LOCALIZED");
                    if (isRelevantEvent)
                    {
                        isLocalizationInProgress = false;
                        HandleLocalizationSuccess(eventData);
                        
                        // Notify waiting task
                        if (localizationTaskCompletionSource != null && !localizationTaskCompletionSource.Task.IsCompleted)
                        {
                            Debug.Log("[LOCALIZATION] Notifying success to waiting task");
                            localizationTaskCompletionSource.TrySetResult((true, null));
                        }
                    }
                    break;
                    
                case LocalizationMapState.LocalizationPending:
                    Debug.Log("[LOCALIZATION] State: PENDING");
                    if (isRelevantEvent)
                    {
                        UpdateProgressStatus(StatusMessages.LOCALIZATION_PROCESSING);
                        isLocalizationInProgress = true;
                    }
                    break;
                    
                case LocalizationMapState.NotLocalized:
                    Debug.Log("[LOCALIZATION] *** FAILED *** State: NOT_LOCALIZED");
                    if (isRelevantEvent && isLocalizationInProgress)
                    {
                        Debug.Log("[LOCALIZATION] Relevant NotLocalized event during active localization - treating as failure");
                        isLocalizationInProgress = false;
                        string errorMessage = "ローカライゼーションに失敗しました";
                        HandleLocalizationFailure(errorMessage);
                        
                        // Notify waiting task
                        if (localizationTaskCompletionSource != null && !localizationTaskCompletionSource.Task.IsCompleted)
                        {
                            Debug.Log("[LOCALIZATION] Notifying failure to waiting task");
                            localizationTaskCompletionSource.TrySetResult((false, errorMessage));
                        }
                    }
                    else
                    {
                        Debug.Log($"[LOCALIZATION] Ignoring NotLocalized event - isRelevant: {isRelevantEvent}, inProgress: {isLocalizationInProgress}");
                    }
                    break;
                    
                // Handle other failure states - removed LocalizationFailed as it doesn't exist
                // The NotLocalized case above should handle most failure scenarios
                    
                default:
                    Debug.LogWarning($"Unhandled localization state: {eventData.State}");
                    if (isRelevantEvent)
                    {
                        isLocalizationInProgress = false;
                        string errorMessage = $"不明なローカライゼーション状態: {eventData.State}";
                        HandleLocalizationFailure(errorMessage);
                        
                        // Notify waiting task
                        if (localizationTaskCompletionSource != null && !localizationTaskCompletionSource.Task.IsCompleted)
                        {
                            localizationTaskCompletionSource.TrySetResult((false, errorMessage));
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            isLocalizationInProgress = false;
            string errorMessage = $"ローカライゼーションイベント処理中にエラーが発生しました: {ex.Message}";
            HandleError("ローカライゼーションイベント処理", ex, OperationResult.UnknownError);
            
            // Notify waiting task of the error
            if (localizationTaskCompletionSource != null && !localizationTaskCompletionSource.Task.IsCompleted)
            {
                localizationTaskCompletionSource.TrySetResult((false, errorMessage));
            }
        }
    }

    private void HandleLocalizationSuccess(LocalizationEventData eventData)
    {
        try
        {
            // Don't clear currentLocalizationTargetId - just overwrite on next request
            
            string mapName = "名前なし"; // LocalizationMapにMapNameプロパティがない場合
            Debug.Log($"Localization successful for map: {eventData.Map.MapUUID}, Name: {mapName}");
            
            // Get the map origin pose
            var poseResult = GetMapOriginPose();
            if (poseResult.success)
            {
                string poseInfo = $"デバイス位置: ({poseResult.position.x:F2}, {poseResult.position.y:F2}, {poseResult.position.z:F2})\n" +
                                 $"デバイス回転: ({poseResult.rotation.eulerAngles.x:F1}°, {poseResult.rotation.eulerAngles.y:F1}°, {poseResult.rotation.eulerAngles.z:F1}°)";
                
                ShowOperationSuccess("ローカライゼーション", $"成功しました！\nMap: {mapName}\nSpace原点からの相対位置:\n{poseInfo}");
                Debug.Log($"Device pose relative to Space origin - Position: {poseResult.position}, Rotation: {poseResult.rotation.eulerAngles}");
            }
            else
            {
                ShowOperationSuccess("ローカライゼーション", $"成功しました！\nMap: {mapName}\n(マップ原点ポーズの取得に失敗)");
                Debug.LogWarning("Localization successful but failed to get map origin pose");
            }
        }
        catch (Exception ex)
        {
            HandleError("ローカライゼーション成功後のポーズ取得", ex, OperationResult.UnknownError);
            Debug.LogError($"Error handling localization success: {ex.Message}");
        }
    }

    private void HandleLocalizationFailure(string reason)
    {
        HandleError("ローカライゼーション", reason, OperationResult.APIError);
        Debug.LogWarning($"Localization failed: {reason}");
    }

    private (bool success, Vector3 position, Quaternion rotation) GetMapOriginPose()
    {
        try
        {
            // Get the current device (camera) position relative to the Space origin
            // The Space origin (0,0,0) is a fixed point in physical space
            // The device position shows where we are relative to that origin
            
            if (Camera.main != null)
            {
                // Get current device position and rotation in the localized space
                var devicePosition = Camera.main.transform.position;
                var deviceRotation = Camera.main.transform.rotation;
                
                Debug.Log($"Device position relative to Space origin: {devicePosition}");
                Debug.Log($"Device rotation relative to Space origin: {deviceRotation.eulerAngles}");
                
                // Return the device's current pose relative to the Space origin
                // This should be different each time you localize from a different position
                return (true, devicePosition, deviceRotation);
            }
            else
            {
                Debug.LogWarning("Main camera not found");
                return (false, Vector3.zero, Quaternion.identity);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception getting device pose relative to map origin: {ex.Message}");
            return (false, Vector3.zero, Quaternion.identity);
        }
    }

    // Cleanup method to unsubscribe from events
    private void OnDestroy()
    {
        // Unsubscribe from events
        try
        {
            MagicLeapLocalizationMapFeature.OnLocalizationChangedEvent -= OnLocalizationChanged;
            Debug.Log("[LOCALIZATION] *** CLEANUP *** Static event unsubscribed");
            
            // Disable localization events
            if (localizationMapFeature != null)
            {
                var disableResult = localizationMapFeature.EnableLocalizationEvents(false);
                Debug.Log($"[LOCALIZATION] *** CLEANUP *** Disabled localization events: {disableResult}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOCALIZATION] *** CLEANUP *** Error during event cleanup: {ex.Message}");
        }
    }

    // Status message constants for consistent Japanese status messages
    private static class StatusMessages
    {
        public const string INITIALIZING = "Space Test Manager初期化中...";
        public const string CHECKING_PERMISSIONS = "権限をチェック中...";
        public const string REQUESTING_PERMISSIONS = "必要な権限を要求中...";
        public const string PERMISSIONS_GRANTED = "権限が付与されました";
        public const string PERMISSIONS_READY = "権限が付与されました。Space機能が利用可能です。";
        public const string EXPORTING_SPACE = "Spaceをエクスポート中...";
        public const string EXPORT_SUCCESS = "Spaceのエクスポートが完了しました";
        public const string IMPORTING_SPACE = "Spaceをインポート中...";
        public const string IMPORT_SUCCESS = "Spaceのインポートが完了しました";
        public const string LOCALIZING = "ローカライゼーションを開始中...";
        public const string LOCALIZATION_REQUESTING = "ローカライゼーション要求を送信しました。結果を待機中...";
        public const string LOCALIZATION_PROCESSING = "ローカライゼーション処理中...";
        public const string LOCALIZATION_SUCCESS = "ローカライゼーション成功!";
        public const string READING_FILE = "Spaceファイルを読み込みました。インポート中...";
        public const string READY = "準備完了。操作を選択してください。";
    }

    // Enhanced status update method with progress indication and timestamp
    private void UpdateStatus(string message, bool isProgress = false, bool isError = false)
    {
        Debug.LogWarning($"[UI Update] FORCE LOG - UpdateStatus called: {message}");
        
        if (statusText != null)
        {
            // Add timestamp for important status updates
            string timestampedMessage = isError || !isProgress ? 
                $"[{System.DateTime.Now:HH:mm:ss}] {message}" : message;
            
            statusText.text = timestampedMessage;
            Debug.LogWarning($"[UI Update] FORCE LOG - Status text updated to: {timestampedMessage}");
            
            // Change text color based on message type (if Text component supports it)
            if (statusText.color != null)
            {
                statusText.color = isError ? Color.red : (isProgress ? Color.yellow : Color.white);
            }
        }
        else
        {
            Debug.LogError("[UI Update] Status text is null!");
        }
        
        // Log with appropriate level
        if (isError)
        {
            Debug.LogError($"SpaceTestManager Status (ERROR): {message}");
        }
        else if (isProgress)
        {
            Debug.Log($"SpaceTestManager Status (PROGRESS): {message}");
        }
        else
        {
            Debug.Log($"SpaceTestManager Status: {message}");
        }
    }

    // Overloaded method to maintain backward compatibility
    private void UpdateStatus(string message)
    {
        UpdateStatus(message, false, false);
    }

    // Method to update status with progress indication
    private void UpdateProgressStatus(string message)
    {
        UpdateStatus(message, true, false);
    }

    // Method to update status with error indication
    private void UpdateErrorStatus(string message)
    {
        UpdateStatus(message, false, true);
    }

    // Method to show operation completion with details
    private void ShowOperationSuccess(string operation, string details = "")
    {
        string successMessage = string.IsNullOrEmpty(details) ? 
            $"{operation}が完了しました" : 
            $"{operation}が完了しました\n{details}";
        
        UpdateStatus(successMessage);
        Debug.Log($"Operation Success - {operation}: {details}");
    }

    // Method to show operation progress with step information
    private void ShowOperationProgress(string operation, string step, int currentStep = 0, int totalSteps = 0)
    {
        string progressMessage = totalSteps > 0 ? 
            $"{operation} ({currentStep}/{totalSteps}): {step}" : 
            $"{operation}: {step}";
        
        UpdateProgressStatus(progressMessage);
    }
    
    // Wait for localization result with timeout
        // Polling-based localization detection as fallback
    private async Task<(bool success, string errorMessage)> CreatePollingTask(string targetSpaceId, int timeoutMs)
    {
        Debug.Log($"[LOCALIZATION] *** POLLING *** Starting polling task for space: {targetSpaceId}");
        
        var startTime = DateTime.Now;
        var pollInterval = 1000; // Poll every 1 second
        var lastKnownState = "unknown";
        
        while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
        {
            try
            {
                // Check if localization has succeeded by examining current state
                var result = localizationMapFeature.GetLocalizationMapsList(out LocalizationMap[] maps);
                
                if (result == UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success && maps != null)
                {
                    var currentState = $"Maps: {maps.Length}";
                    if (currentState != lastKnownState)
                    {
                        Debug.Log($"[LOCALIZATION] *** POLLING *** State changed: {currentState}");
                        lastKnownState = currentState;
                    }
                    
                    // Check if we're localized to the target space
                    // Note: This is a simplified check - in reality, you might need to check 
                    // additional state or use different APIs to determine localization success
                    foreach (var map in maps)
                    {
                        if (map.MapUUID.Equals(targetSpaceId, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[LOCALIZATION] *** POLLING *** Found target map in list: {map.MapUUID}");
                            // For now, assume if the target map is in the list, localization might be working
                            // In a real implementation, you'd need additional checks for localization state
                        }
                    }
                }
                
                // Wait before next poll
                await Task.Delay(pollInterval);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LOCALIZATION] *** POLLING *** Error during polling: {ex.Message}");
                await Task.Delay(pollInterval);
            }
        }
        
        Debug.Log($"[LOCALIZATION] *** POLLING *** Polling task timed out after {timeoutMs}ms");
        return (false, "Polling task timed out - no localization state change detected");
    }
    
private async Task<(bool success, string errorMessage)> WaitForLocalizationResultAsync(string targetSpaceId, int timeoutMs)
    {
        Debug.Log($"[LOCALIZATION] Starting to wait for localization result for space: {targetSpaceId}, timeout: {timeoutMs}ms");
        
        // Clear any existing TaskCompletionSource first
        if (localizationTaskCompletionSource != null)
        {
            Debug.LogWarning("[LOCALIZATION] *** WAIT *** Clearing existing TaskCompletionSource");
            localizationTaskCompletionSource = null;
        }
        
        // Create a new TaskCompletionSource to wait for the callback
        localizationTaskCompletionSource = new TaskCompletionSource<(bool success, string errorMessage)>();
        Debug.Log($"[LOCALIZATION] *** WAIT *** TaskCompletionSource created");
        
        // Create a timeout task
        var timeoutTask = Task.Delay(timeoutMs);
        Debug.Log($"[LOCALIZATION] *** WAIT *** Timeout task created for {timeoutMs}ms");
        
        // Create a polling task as fallback (in case events don't work)
        var pollingTask = CreatePollingTask(targetSpaceId, timeoutMs);
        Debug.Log($"[LOCALIZATION] *** WAIT *** Polling task created as fallback");
        
        // Wait for either the localization to complete, polling to succeed, or timeout
        Debug.Log($"[LOCALIZATION] *** WAIT *** Starting Task.WhenAny with 3 tasks (event, polling, timeout)...");
        var completedTask = await Task.WhenAny(localizationTaskCompletionSource.Task, pollingTask, timeoutTask);
        Debug.Log($"[LOCALIZATION] *** WAIT *** Task.WhenAny completed");
        
        // Check which task completed
        if (completedTask == localizationTaskCompletionSource.Task)
        {
            Debug.Log("[LOCALIZATION] *** WAIT *** Event-based localization task completed first");
        }
        else if (completedTask == pollingTask)
        {
            Debug.Log("[LOCALIZATION] *** WAIT *** Polling-based localization task completed first");
        }
        else if (completedTask == timeoutTask)
        {
            Debug.Log("[LOCALIZATION] *** WAIT *** Timeout task completed first");
        }
        
        // Handle polling task completion
        if (completedTask == pollingTask)
        {
            Debug.Log("[LOCALIZATION] *** POLLING SUCCESS *** Localization detected via polling");
            var pollingResult = await pollingTask;
            localizationTaskCompletionSource = null;
            // Don't clear currentLocalizationTargetId - just overwrite on next request
            return pollingResult;
        }
        
        if (completedTask == timeoutTask)
        {
            Debug.LogError($"[LOCALIZATION] *** TIMEOUT *** Localization timed out after {timeoutMs}ms for space: {targetSpaceId}");
            localizationTaskCompletionSource = null;
            // Don't clear currentLocalizationTargetId - just overwrite on next request
            return (false, $"ローカライゼーションがタイムアウトしました ({timeoutMs / 1000}秒)");
        }
        
        // Localization completed (success or failure)
        var result = await localizationTaskCompletionSource.Task;
        localizationTaskCompletionSource = null;
        // Don't clear currentLocalizationTargetId - just overwrite on next request
        
        Debug.Log($"[LOCALIZATION] Wait completed. Success: {result.success}, Error: {result.errorMessage}");
        return result;
    }
    
    // Cancel ongoing localization
    public void CancelLocalization()
    {
        Debug.Log("Canceling ongoing localization...");
        
        if (isLocalizationInProgress)
        {
            isLocalizationInProgress = false;
            // Don't clear currentLocalizationTargetId - just overwrite on next request
            
            // Notify waiting task of cancellation
            if (localizationTaskCompletionSource != null && !localizationTaskCompletionSource.Task.IsCompleted)
            {
                localizationTaskCompletionSource.TrySetResult((false, "ユーザーによりローカライゼーションがキャンセルされました"));
                localizationTaskCompletionSource = null;
            }
            
            UpdateStatus("ローカライゼーションがキャンセルされました");
            UpdateLocalizationButtonState(false); // Reset button to normal state
            Debug.Log("Localization canceled by user");
        }
    }
    
    // Update localization button state (normal/cancel mode)
    private void UpdateLocalizationButtonState(bool isLocalizing)
    {
        if (localizeButton != null)
        {
            var buttonText = localizeButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = isLocalizing ? "キャンセル" : "ローカライゼーション";
            }
            
            // Update click handler
            localizeButton.onClick.RemoveAllListeners();
            if (isLocalizing)
            {
                localizeButton.onClick.AddListener(CancelLocalization);
            }
            else
            {
                localizeButton.onClick.AddListener(OnLocalizeButtonClicked);
            }
            
            // Update XR Interaction handler
            var xrInteractable = localizeButton.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveAllListeners();
                if (isLocalizing)
                {
                    xrInteractable.selectEntered.AddListener((args) => CancelLocalization());
                }
                else
                {
                    xrInteractable.selectEntered.AddListener((args) => OnLocalizeButtonClicked());
                }
            }
        }
    }

    // Get map name by Space ID (synchronous version)
    private Task<string> GetMapNameByIdAsync(string spaceId)
    {
        try
        {
            Debug.Log($"Getting map name for Space ID: {spaceId}");
            
            // Get all available localization maps using the same method as GetLatestSpaceAsync
            var result = localizationMapFeature.GetLocalizationMapsList(out LocalizationMap[] maps);
            
            if (result != UnityEngine.XR.OpenXR.NativeTypes.XrResult.Success || maps == null || maps.Length == 0)
            {
                Debug.LogWarning("No localization maps found");
                return Task.FromResult<string>(null);
            }
            
            // Find the map with matching ID
            var matchingMap = maps.FirstOrDefault(map => 
                map.MapUUID.Equals(spaceId, StringComparison.OrdinalIgnoreCase));
                
            if (matchingMap.MapUUID != null) // Check if a valid map was found
            {
                Debug.Log($"Found matching map for ID: {spaceId}");
                // LocalizationMapにMapNameがない場合は、IDの一部を表示
                return Task.FromResult($"Map-{spaceId.Substring(0, Math.Min(8, spaceId.Length))}");
            }
            
            Debug.LogWarning($"Could not find map for Space ID: {spaceId}");
            return Task.FromResult<string>(null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting map name for Space ID {spaceId}: {ex.Message}");
            return Task.FromResult<string>(null);
        }
    }

    // Helper method to check if a directory can be created
    private bool CanCreateDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                return true;
            }
            
            // Try to create the directory
            Directory.CreateDirectory(directoryPath);
            
            // Check if creation was successful
            if (Directory.Exists(directoryPath))
            {
                Debug.Log($"Successfully created directory: {directoryPath}");
                return true;
            }
            else
            {
                Debug.LogWarning($"Directory creation appeared to succeed but directory doesn't exist: {directoryPath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Cannot create directory {directoryPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Helper method to wait for a coroutine to complete in an async method
    /// </summary>



    /// <summary>
    /// Synchronously download space data from WebDAV
    /// </summary>


}