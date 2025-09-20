# アプリの説明

１台の デバイスで、Space の Export → Import を APIで行うテストアプリ

# 基本仕様

## 前提条件

**前提条件**

- **Magic Leap Hub** - 最新版
- **Magic Leap 2 SDK** - 最新版
- **ADB (Android Debug Bridge)** - 設定済み
- **Unity 6000.2.0f1** - URPテンプレート
- **Magic Leap 2デバイス** - 開発者モード有効、USB接続可能
- **Magic Leap 2 OS** - 最新版

**主要な依存関係**

- Magic Leap Unity SDK v2.6.0 (`com.magicleap.unitysdk`)
- Unity MCP Bridge (`com.coplaydev.unity-mcp`)
- Universal Render Pipeline v17.2.0
- Input System v1.14.1
- Unity Test Framework v1.5.1

開発方法

- kiro で要件定義・設計・実装
- UnityMCP経由で、Unity上のビルドを実行
- 動作確認は、Magic Leap2 本体で行う

## 基本機能

- １台の デバイスで、Space の Export → Import を APIで行うテストアプリ
    - adb コマンドを使って 同じ動作は確認済み
- フィージビリティ確認のためのテストアプリのため、できるだけシンプルなコードで実装する
- Unity上の UI　もできるだけシンプルにして、Space の Export　→ Import 機能の実装にフォーカスする

## 実装の骨子

## Magic Leap Space API の概要（Localization Maps）

Magic Leap 2 における **Space（空間マップ）** は、特徴点・メッシュ・アンカーなどを含む環境データで、デバイスが自己位置を推定し CG を正しく配置する基盤です。

Unity + OpenXR 環境では、`MagicLeapLocalizationMapFeature` を通じて Space を操作できます。

主な API 機能：

- **Space 一覧取得**: `GetLocalizationMapsList(out LocalizationMap[] maps)`
- **ローカライズ要求**: `RequestMapLocalization(string mapId)`
- **最新ローカライズ状態取得**: `GetLatestLocalizationMapData()`
- **イベント購読**: `OnLocalizationChangedEvent` でローカライズ結果を受け取る
- **インポート／エクスポート**: Space をバイナリとして保存／読み込み

> 注意: プロジェクト設定で Magic Leap 2 Localization Maps を有効化し、SPACE_MANAGER 権限が必要です 。
> 

---

## 2. Space データのエクスポート／インポート

### エクスポート

ある端末で作成した Space をファイルに書き出します。

事前に `SPACE_IMPORT_EXPORT` 権限を取得してください。

```csharp
var feature = OpenXRSettings.Instance.GetFeature<MagicLeapLocalizationMapFeature>();
if (feature != null && feature.enabled)
{
    string mapID = "<EXPORTするSpaceのUUID>";
    if (Permissions.CheckPermission("com.magicleap.permission.SPACE_IMPORT_EXPORT"))
    {
        feature.ExportLocalizationMap(mapID, out byte[] mapData);
        File.WriteAllBytes("/sdcard/Download/space.bin", mapData);
    }
}

```

### インポート

他端末でファイルを読み込み、新しい Space として保存します。

```csharp
var feature = OpenXRSettings.Instance.GetFeature<MagicLeapLocalizationMapFeature>();
byte[] mapData = File.ReadAllBytes("/sdcard/Download/space.bin");
if (Permissions.CheckPermission("com.magicleap.permission.SPACE_IMPORT_EXPORT"))
{
    feature.ImportLocalizationMap(mapData, out string importedMapId);
    Debug.Log("Imported Space: " + importedMapId);
}

```

---

## 3. インポート後のローカライゼーション処理

インポートした Space を利用するには、その Space にローカライズする必要があります。

```csharp
feature.RequestMapLocalization(importedMapId);

```

成功すると `OnLocalizationChangedEvent` で `State = Localized` が返り、`GetMapOrigin()` で Space の座標系の原点（Pose）が取得できます。

これにより、複数端末が同じ座標系で CG を共有できます。

## 5. Unity 設定と必要なパーミッション

- **OpenXR Features 有効化**
    - Localization Maps
    - Spatial Anchors Subsystem
    - Spatial Anchors Storage
- **マニフェスト権限**
    - `SPACE_MANAGER`（Space 操作）
    - `SPACE_IMPORT_EXPORT`（インポート／エクスポート）
    - `SPATIAL_ANCHOR`（アンカー利用）

> これらは Unity の Magic Leap Manifest Settings から設定できます。
> 

## kiroに考えて欲しいこと

- アプリの全体構成、できるだけシンプルに
- シンプルな UI の設計

# 参考にすべきマニュアル

- https://developer-docs.magicleap.cloud/docs/unity-api/api/
- https://developer-docs.magicleap.cloud/docs/unity-api/api/MagicLeap/OpenXR/Features/LocalizationMaps/