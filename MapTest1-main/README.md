# Magic Leap 2 Space Localization Test Application

Magic Leap 2でSpace（ローカライゼーションマップ）のエクスポート・インポート・ローカライゼーション機能を実証するテストアプリケーションです。Unity 6000.2.0f1、Universal Render Pipeline (URP)、Magic Leap Unity SDKを使用しています。

## 🚀 プロジェクト概要

このプロジェクトは、Magic Leap 2のSpace API機能を検証するための実証アプリケーションです。単一デバイスでSpaceをファイルにエクスポートし、そのファイルをインポートして元に戻すことで、Spaceデータ転送ワークフローの実現可能性を検証します。

### 主要な機能

- 🗺️ **Space Export**: 最新のSpaceを自動的に対象としてバイナリファイルにエクスポート
- 📥 **Space Import**: エクスポートしたSpaceファイルを読み込み、新しいSpaceエントリを作成
- 📍 **Localization**: インポートしたSpaceに対してローカライゼーションを実行
- 🎮 **XR Interaction**: Magic Leap 2コントローラでのUI操作対応
- ⚡ **Event-based Detection**: リアルタイムlocalizationイベント検出
- ☁️ **WebDAV Integration**: WebDAVサーバーを介した複数デバイス間でのSpace共有機能（新機能）
- 🔄 **重複クリック防止**: Magic Leap 2のタッチ入力の重複実行を防止

## 📋 必要な環境

### 前提条件

- **Magic Leap Hub** - 最新版
- **Magic Leap 2 SDK** - 最新版
- **ADB (Android Debug Bridge)** - 設定済み
- **Unity 6000.2.0f1** - URPテンプレート
- **Magic Leap 2デバイス** - 開発者モード有効、USB接続可能
- **Magic Leap 2 OS** - 最新版

### 主要な依存関係

- Magic Leap Unity SDK v2.6.0 (`com.magicleap.unitysdk`)
- XR Interaction Toolkit（コントローラ入力用）
- Unity MCP Bridge (`com.coplaydev.unity-mcp`)
- Universal Render Pipeline v17.2.0
- Input System v1.14.1
- Unity Test Framework v1.5.1

### 必要な権限

以下のAndroid権限が`AndroidManifest.xml`で設定されています：
- `com.magicleap.permission.SPACE_MANAGER` - Space操作用
- `com.magicleap.permission.SPACE_IMPORT_EXPORT` - エクスポート・インポート用
- `com.magicleap.permission.SPATIAL_ANCHOR` - Spatial Anchor用

## 🛠️ セットアップ手順

詳細なセットアップ手順については、[SETUP.md](SETUP.md)を参照してください。

### クイックスタート

1. **Unityでプロジェクトを開く**
   - Unity Hub → Open → プロジェクトフォルダを選択

2. **Magic Leap設定の適用**
   - Window > Magic Leap > Project Setup → Apply All

3. **OpenXR設定の確認**
   - Window > XR > OpenXR > Project Validation → Fix All
   - **Localization Map Feature**が有効化されていることを確認

4. **ビルド＆デプロイ**
   - File > Build Settings → Build And Run
   - Magic Leap 2デバイスが接続されていることを確認

## 📁 プロジェクト構造

```
Assets/
├── Scenes/                 # Unityシーン
│   ├── ML2basic.unity     # メインシーン（Space UI + WebDAV統合）
│   └── SampleScene.unity  # サンプルシーン
├── Scripts/               # アプリケーションスクリプト
│   ├── SpaceTestManager.cs     # Space機能の統合管理
│   ├── WebDAVSpaceManager.cs   # WebDAVファイル共有機能（新規）
│   ├── PermissionDebugger.cs   # 権限デバッグ用
│   ├── PreDeploymentValidator.cs # デプロイ前検証
│   ├── ConfigurationTest.cs    # 設定テスト用
│   └── Editor/            # エディタ拡張スクリプト
├── Input/                 # XR Interaction設定
│   └── XRIDefaultInputActions.inputactions # XRコントローラ入力定義
├── Settings/              # URP レンダリング設定
│   ├── PC_RPAsset.asset   # PC用レンダーパイプライン
│   ├── Mobile_RPAsset.asset # Mobile用レンダーパイプライン
│   └── DefaultVolumeProfile.asset # ボリュームプロファイル
├── XR/                    # OpenXR関連設定
│   ├── Settings/          # OpenXR設定ファイル
│   ├── Loaders/           # XRローダー設定
│   └── Resources/         # XRシミュレーション設定
├── XRI/                   # XR Interaction Toolkit設定
│   └── Settings/          # XRIエディタ設定とレイヤー設定
├── TutorialInfo/          # チュートリアル用スクリプト
│   ├── Scripts/Readme.cs  # README表示スクリプト
│   └── Editor/            # エディタ拡張
├── Resources/             # リソースフォルダ
└── Plugins/               # プラットフォーム固有プラグイン
    └── Android/           # Android設定
        └── AndroidManifest.xml # Magic Leap権限 + インターネット権限
ProjectSettings/           # Unity プロジェクト設定
Packages/                  # パッケージマニフェスト（Magic Leap SDK含む）
```

### 核となるコンポーネント

- **SpaceTestManager.cs**: Space Export/Import/Localization機能の統合管理、WebDAV統合制御
- **WebDAVSpaceManager.cs**: WebDAVサーバーを使用したスペースファイル共有機能（新規）
- **ML2basic.unity**: XR Origin、UI Canvas、コントローラ操作設定、WebDAV統合UIを含むメインシーン
- **AndroidManifest.xml**: Space Management権限 + インターネットアクセス権限設定

## 🎮 アプリケーションの使用方法

### 基本操作フロー

1. **アプリ起動**: Magic Leap 2でアプリケーションを起動
2. **権限許可**: Space Management権限の要求に対して「許可」を選択
3. **Space Export**: 「Export」ボタンをコントローラでタップして最新のSpaceをエクスポート
4. **Space Import**: 「Import」ボタンでエクスポートしたファイルをインポート
5. **Localization**: 「Localize」ボタンでインポートしたSpaceにローカライズ
6. **Force Re-localize**: テスト用の強制再ローカライゼーション

### UI Components

- **Export Button**: 最新のSpaceを`Application.persistentDataPath`にエクスポート
- **Import Button**: エクスポートしたSpaceファイルを読み込んでインポート
- **Localize Button**: Spaceローカライゼーションを実行
- **Force Re-localize Button**: テスト用の強制再ローカライゼーション
- **Status Text**: 操作結果とデバイス位置情報を表示

## 🔧 開発者向け情報

### ビルド設定

- **パッケージ名**: com.TeraGrove.ML2starter
- **プラットフォーム**: Android (Magic Leap 2)
- **アーキテクチャ**: x86_64
- **Unity**: 6000.2.0f1 with URP
- **最小APIレベル**: Android 10 (API 29)

### デバッグ

#### Development Build
```bash
# アプリ固有のログ表示（推奨）
adb logcat --pid=$(adb shell pidof -s com.TeraGrove.ML2starter) | grep -E "Unity|Localization|Space"

# Space関連ログ
adb logcat | grep SpaceTestManager

# persistentDataPathの確認
adb shell run-as com.TeraGrove.ML2starter ls -la /data/data/com.TeraGrove.ML2starter/files/
```

#### 手動アプリ起動
```bash
adb shell am start -n com.TeraGrove.ML2starter/com.unity3d.player.UnityPlayerActivity
```

### 技術的詳細

- **Event-based Detection**: `EnableLocalizationEvents(true)`でリアルタイムローカライゼーション検出
- **File Operations**: Android互換性のため`Application.persistentDataPath`を使用
- **XR Interaction**: XR Ray Interactorによるコントローラ操作対応
- **Canvas Configuration**: World Space Canvas（Y=-0.2）での3D UI表示

### Unity MCP Bridge

Claude Codeとの統合により、以下の操作が可能です：

- `mcp__unityMCP__read_console` - Unityコンソール出力の監視
- `mcp__unityMCP__manage_scene` - シーン操作（読み込み、保存、作成）
- `mcp__unityMCP__manage_gameobject` - GameObjectの操作
- `mcp__unityMCP__manage_asset` - アセット操作
- `mcp__unityMCP__manage_editor` - エディタ状態制御

## 🧪 テストガイド

詳細なテスト手順については以下のドキュメントを参照してください：
- **[DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md)** - デプロイメント検証チェックリスト
- **[MAGIC_LEAP_2_TESTING_GUIDE.md](MAGIC_LEAP_2_TESTING_GUIDE.md)** - 実機での詳細テスト手順

### テスト段階

1. **Phase 1**: アプリケーションのデプロイと基本動作確認
2. **Phase 2**: Localization Event Detection機能のテスト
3. **Phase 3**: File Operations（Export/Import）のテスト
4. **Phase 4**: エラーケースの動作確認
5. **Phase 5**: 実環境でのValidation

## 📚 プロジェクトドキュメント

- **[SETUP.md](SETUP.md)** - プロジェクトセットアップの詳細手順
- **[CLAUDE.md](CLAUDE.md)** - Claude Code統合ガイド
- **[DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md)** - デプロイメント検証チェックリスト
- **[MAGIC_LEAP_2_TESTING_GUIDE.md](MAGIC_LEAP_2_TESTING_GUIDE.md)** - Magic Leap 2での詳細テストガイド

### 外部リソース

- [Magic Leap Unity API Reference](https://developer-docs.magicleap.cloud/docs/unity-api/api/)
- [Magic Leap Localization Maps API](https://developer-docs.magicleap.cloud/docs/unity-api/api/MagicLeap/OpenXR/Features/LocalizationMaps/)
- [Unity XR Interaction Toolkit](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest/)

## 📋 実装完了事項

このプロジェクトでは以下の要件が実装・検証済みです：

- ✅ **要件1**: Space Export機能 - 最新Spaceの自動エクスポート
- ✅ **要件2**: Space Import機能 - ファイルからのSpaceインポート
- ✅ **要件3**: Localization機能 - インポートしたSpaceへのローカライゼーション
- ✅ **要件4**: シンプルUI - コントローラ操作対応のSpace操作UI
- ✅ **要件5**: 権限処理 - Space Management権限の適切な管理

全10タスクが完了し、Magic Leap 2デバイスでの動作確認済みです。

## 🏢 プロジェクト情報

- **プロジェクト名**: ML2starter (Space Localization Test App)
- **会社**: TeraGrove  
- **パッケージ名**: com.TeraGrove.ML2starter
- **Unity バージョン**: 6000.2.0f1 with URP
- **Magic Leap SDK**: v2.6.0
- **対象デバイス**: Magic Leap 2専用

---

**注意**: このアプリケーションは、Magic Leap 2のSpace API機能を検証するための実証目的で作成されています。実際のSpaceを作成・スキャンした環境でのテストを推奨します。