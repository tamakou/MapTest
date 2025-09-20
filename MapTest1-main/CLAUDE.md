# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Magic Leap 2 Unity project demonstrating Space management functionality (export/import/localization) with XR controller interaction. The project uses Unity 6000.2.0f1 with the Universal Render Pipeline (URP) and is configured for Magic Leap 2 deployment.

**Key Dependencies:**
- Magic Leap Unity SDK v2.6.0 (`com.magicleap.unitysdk`)
- Unity MCP Bridge (`com.coplaydev.unity-mcp`) - Enables Claude Code integration with Unity Editor
- Universal Render Pipeline v17.2.0
- Unity XR Interaction Toolkit
- Input System v1.14.1

## Architecture

**Project Structure:**
- `Assets/` - Main project assets
  - `Scenes/ML2basic.unity` - Main scene with XR interaction setup
  - `Scripts/SpaceTestManager.cs` - Core Space management functionality
  - `Input/XRIDefaultInputActions.inputactions` - XR controller input mappings
  - `Plugins/Android/AndroidManifest.xml` - Magic Leap permissions
- `ProjectSettings/` - Unity project settings including XR configuration
- `Packages/` - Package manifest with Magic Leap and Unity dependencies

**Core Technologies:**
- **Platform Target:** Magic Leap 2 (Android-based, x86_64 architecture)
- **Rendering:** Universal Render Pipeline (URP) with Linear color space
- **XR Framework:** OpenXR with Magic Leap 2 provider
- **Input System:** Unity XR Interaction Toolkit (XRI) with XR Ray Interactor
- **Build Target:** Android with Magic Leap 2 platform settings

## Development Commands

**Build and Deployment:**
```bash
# Check device connection
adb devices

# Clear previous installation if needed
adb uninstall com.TeraGroove.ML2starter

# Deploy to device (via Unity)
# Unity Editor -> File -> Build Settings -> Build and Run

# Manual APK installation
adb install -r build.apk

# Monitor application logs with localization filtering
adb logcat -v threadtime | grep -E "(LOCALIZATION|\*\*\*|Unity.*Error|Unity.*Exception)"
```

**Unity Editor Operations (via UnityMCP):**
- `mcp__unityMCP__read_console` - Monitor Unity console output
- `mcp__unityMCP__manage_scene` - Scene operations
- `mcp__unityMCP__manage_gameobject` - GameObject manipulation
- `mcp__unityMCP__manage_asset` - Asset operations

## Magic Leap 2 Configuration

**Build Settings:**
- Platform: Android
- Scripting Backend: IL2CPP
- Target Architecture: x86_64 (Magic Leap 2 specific)
- Graphics APIs: Vulkan only
- Minimum API Level: Android 10 (API 29)
- Company Name: TeraGrove
- Product Name: ML2starter

**OpenXR Features Enabled:**
- Magic Leap 2 Localization Maps
- Magic Leap 2 Spatial Anchor Subsystem
- Magic Leap 2 Spatial Anchors Storage
- Magic Leap 2 Support Feature

**Required Permissions (AndroidManifest.xml):**
- `com.magicleap.permission.SPACE_MANAGER`
- `com.magicleap.permission.SPACE_IMPORT_EXPORT`
- `com.magicleap.permission.SPATIAL_ANCHOR`
- `android.permission.WRITE_EXTERNAL_STORAGE`
- `android.permission.READ_EXTERNAL_STORAGE`

## Core Implementation Details

**XR Interaction Setup (ML2basic.unity):**
- XR Origin with Game Controller containing XR Ray Interactor
- Canvas configured as World Space with TrackedDeviceGraphicRaycaster
- EventSystem with XRUIInputModule
- Input Action Manager with XRIDefaultInputActions

**SpaceTestManager Functionality:**
1. **Space Export**: Exports current Space to `Application.persistentDataPath`
2. **Space Import**: Imports Space from saved file
3. **Localization**: Localizes to imported Space (with force re-localization for testing)
4. **Permission Management**: Runtime permission requests with fallback chains

**Key Implementation Patterns:**
- Uses `EnableLocalizationEvents(true)` for event-based localization tracking
- Implements both event-based and polling-based localization detection
- Simple ID overwrite strategy for tracking localization targets (no clearing)
- Shows device position relative to Space origin after localization

## Known Behaviors

1. **Localization Events**: Multiple events fire during localization (Pending → Localized)
2. **Force Re-localization**: Enabled for testing - allows re-localization to same Space
3. **Polling Continuation**: Polling task continues after success (doesn't affect functionality)
4. **Device Position Display**: Shows current device position relative to Space origin

## Working with this Codebase

When developing for this project:
1. Ensure XR Interaction Toolkit is properly configured before testing UI interaction
2. Always call `EnableLocalizationEvents(true)` when initializing localization features
3. Use `Application.persistentDataPath` for file operations on Android
4. Monitor localization events with `[LOCALIZATION]` tagged logs
5. Test with actual Magic Leap 2 device - simulator may not support all features
6. Canvas should be positioned at eye level (Y=-0.2 in anchored position)