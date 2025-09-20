# Magic Leap 2 Unity Space Localization Project Setup

## 0. 前提

- ML Hub / ML2 SDK / ADB / Unity 6000.2.0f1（URP テンプレート）導入済み
- ML2 は開発者モード・USB接続OK
- ML2 OS は最新

---

## 1. プロジェクト作成

Unity Hub → New → **3D(URP)**。

「Add Magic Leap Registry」は **Use Magic Leap Registry**。

---

## 2. Magic Leap Project Setup Tool

https://assetstore.unity.com/packages/tools/integration/magic-leap-setup-tool-194780

**Window > Magic Leap > Project Setup** → **Apply All**

（Android設定・OpenXR・Vulkan・Linear・IL2CPP 等を自動適用）

- OpenXR: **Magic Leap OpenXR Feature** を **Enabled**
- **Localization Map Feature** を **Enabled**（空間認識用）

**Window > XR > OpenXR > Project Validation** → **Fix All** が緑になるまで。

---

## 3. Player 設定（Unity 6 & ML2/x86_64）

**Edit > Project Settings > Player > Other Settings**

- **Application Entry Point**: **Activity** のみチェック（**GameActivityを外す**）
- **Scripting Backend**: **IL2CPP**
- **Target Architectures**: **x86_64**（※ML2向け。ARMは不要）
- **Graphics APIs (Android)**: **Vulkan** のみ
- **Minimum API Level**: **Android 10 (API 29)**
- **Color Space**: **Linear**

---

## 4. AndroidManifest.xml（Space Management権限付き）

Setup Tool が自動生成した `Assets/Plugins/Android/AndroidManifest.xml` を確認・更新。

**必要な権限**:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android" xmlns:tools="http://schemas.android.com/tools">
  <!-- Magic Leap 2 Space Management Permissions -->
  <uses-permission android:name="com.magicleap.permission.SPACE_MANAGER" />
  <uses-permission android:name="com.magicleap.permission.SPACE_IMPORT_EXPORT" />
  <uses-permission android:name="com.magicleap.permission.SPATIAL_ANCHOR" />
  <!-- Additional permissions that may be required -->
  <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
  <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
  <uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
  <!-- Alternative Magic Leap permission names -->
  <uses-permission android:name="com.magicleap.permission.SPATIAL_ANCHORS" />
  <application>
    <activity android:name="com.unity3d.player.UnityPlayerActivity" android:theme="@style/UnityThemeSelector">
      <intent-filter>
        <action android:name="android.intent.action.MAIN" />
        <category android:name="android.intent.category.LAUNCHER" />
      </intent-filter>
      <meta-data android:name="unityplayer.UnityActivity" android:value="true" />
    </activity>
  </application>
</manifest>
```

---

## 5. XR Interaction Toolkit Setup

**必要なパッケージ**:
- XR Interaction Toolkit（既にインストール済み）

**シーン構成（ML2basic.unity）**:
- **XR Origin** with **Main Camera**（Near Clip = 0.37）
- **Game Controller** with **XR Ray Interactor**
- **Canvas**（World Space）with **TrackedDeviceGraphicRaycaster**
- **EventSystem** with **XRUIInputModule**
- **Input Action Manager** with **XRIDefaultInputActions**

---

## 6. Core Scripts

**SpaceTestManager.cs** - Main script for Space localization management:
- Attached to **SpaceTestManager** GameObject in scene
- Manages localization events and polling
- Implements `EnableLocalizationEvents(true)` for event-based tracking
- Provides UI interaction via button presses
- Uses `Application.persistentDataPath` for file operations

---

## 7. Build & Run

- **Scenes In Build**: ML2basic.unity scene configured
- **Development Build** をON（調査時）
- Build And Run

署名不一致などで失敗したら旧ビルドを削除：

```bash
adb uninstall com.TeraGrove.ML2starter
```

---

## 8. デバッグ実践

### Development Build

- **Development Build**：詳細ログ・Profiler接続可
- **Script Debugging**：C#デバッガ接続用
- **Autoconnect Profiler**：Profiler即接続

### adb logcat 基本

```bash
# ログクリア
adb logcat -c
# アプリのPIDに絞って表示
adb logcat --pid=$(adb shell pidof -s com.TeraGrove.ML2starter)
# 重要ログだけ
adb logcat | grep -E "AndroidRuntime|E/Unity|OpenXR|Magic|Vulkan|ActivityManager|Crash|Localization"
```

ログ保存：

```bash
adb logcat -v time > ml2_run.log
```

手動起動：

```bash
adb shell am start -n com.TeraGrove.ML2starter/com.unity3d.player.UnityPlayerActivity
```

---

## 9. 最終チェックリスト

- [ ] Setup Tool → **Apply All**
- [ ] Player → **Entry Point=Activity**／**x86_64**／**IL2CPP**／**Vulkan**
- [ ] AndroidManifest.xml に Space Management 権限を追加
- [ ] OpenXR Validation → All Green
- [ ] **Localization Map Feature** が Enabled
- [ ] XR Interaction Toolkit 構成が完了
- [ ] Development Build ＋ adb logcat で起動ログ確認

---

## 10. UnityMCPセットアップ

1. **Unity パッケージの追加**
    - Unity を起動 → **Window > Package Manager > + > Add package from git URL…**
    - 次をそのまま貼り付け → **Add**：
        
        ```
        https://github.com/CoplayDev/unity-mcp.git?path=/UnityMcpBridge
        ```
        
    - これで **Unity側のBridgeが入る**と同時に、**ローカルにMCPサーバ（Python）が自動インストール**されます。[GitHub](https://github.com/CoplayDev/unity-mcp?tab=readme-ov-file)