# Magic Leap 2 Space Localization - Deployment Checklist

## Pre-Deployment Verification

### ✅ Project Configuration
- [x] Magic Leap Unity SDK 2.6.0 installed
- [x] OpenXR Localization Maps feature enabled
- [x] Space Management permissions in AndroidManifest.xml
- [x] SpaceTestManager.cs implemented with localization events
- [x] XR Interaction Toolkit configured for controller input

### ✅ Build Settings  
- [x] Target Platform: Android (Magic Leap 2)
- [x] Package Name: com.TeraGrove.ML2starter
- [x] Unity 6000.2.0f1 with URP
- [x] XR Plugin Management: OpenXR + Magic Leap provider
- [x] Minimum API Level: 29

### ✅ Scene Configuration (ML2basic.unity)
- [x] XR Origin with Main Camera (Near Clip: 0.37)  
- [x] Game Controller with XR Ray Interactor
- [x] World Space Canvas with TrackedDeviceGraphicRaycaster
- [x] EventSystem with XRUIInputModule
- [x] SpaceTestManager GameObject with script attached

## Device Testing

### Phase 1: Basic Deployment
- [ ] Connect Magic Leap 2 device via USB
- [ ] Enable Developer Mode and USB Debugging  
- [ ] Build and deploy application successfully
- [ ] Verify app launches and UI is visible in world space
- [ ] Test controller interaction with UI buttons

### Phase 2: Localization Testing
- [ ] **Basic Localization:**
  - [ ] Tap Localize button via controller
  - [ ] Verify localization events are triggered
  - [ ] Check status display updates
  - [ ] Observe device position relative to Space origin

- [ ] **Event-Based Detection:**
  - [ ] Monitor console for localization events
  - [ ] Verify `EnableLocalizationEvents(true)` is working
  - [ ] Test both successful and failed localization

- [ ] **Force Re-localization:**
  - [ ] Use Force Re-localize button for testing
  - [ ] Verify localization can be triggered on demand
  - [ ] Check that polling continues after events

### Phase 3: File Operations Testing  
- [ ] **Export Functionality:**
  - [ ] Export Space data using Export button
  - [ ] Verify file creation at Application.persistentDataPath
  - [ ] Check success/error messages in UI

- [ ] **Import Functionality:**
  - [ ] Import Space data using Import button
  - [ ] Verify file reading from persistentDataPath
  - [ ] Test with valid and invalid Space files

### Phase 4: Error Handling Testing
- [ ] **Permission Errors:**
  - [ ] Test functions without required permissions
  - [ ] Verify appropriate error messages in UI

- [ ] **File System Errors:**
  - [ ] Test Import with missing Space files  
  - [ ] Verify "file not found" error handling

- [ ] **API Errors:**
  - [ ] Test Export with no available Spaces
  - [ ] Test Localize in unscanned environment
  - [ ] Verify error message clarity

## Debugging Commands

```bash
# Check device connection
adb devices

# Monitor Unity logs with localization focus  
adb logcat --pid=$(adb shell pidof -s com.TeraGrove.ML2starter) | grep -E "Unity|Localization|Space"

# App-specific logs
adb logcat | grep SpaceTestManager

# Check persistent data path
adb shell run-as com.TeraGrove.ML2starter ls -la /data/data/com.TeraGrove.ML2starter/files/
```

## Success Criteria

Deployment is successful when:

1. ✅ Application deploys and runs on Magic Leap 2
2. ✅ XR controller interaction with UI works
3. ✅ Localization events are triggered and detected
4. ✅ Export/Import file operations complete
5. ✅ Status messages provide clear feedback
6. ✅ No unhandled exceptions in console
7. ✅ Event-based localization detection functions

## Implementation Notes

- **Known Behavior**: Localization events trigger continuously after initial detection
- **File Operations**: Uses `Application.persistentDataPath` for Android compatibility  
- **Event System**: Both event-based and polling-based localization detection implemented
- **UI Interaction**: Requires XR Ray Interactor for controller-based button presses
- **Canvas Position**: World Space canvas positioned at Y=-0.2 for visibility