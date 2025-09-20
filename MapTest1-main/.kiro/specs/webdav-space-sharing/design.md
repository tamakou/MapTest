# 設計書

## 概要

この設計書では、既存のMagic Leap 2 UnityアプリケーションにWebDAVベースのスペース共有機能を統合する方法を定義します。現在のローカルファイルベースのスペース管理システムを、WebDAVクラウドストレージを使用するように拡張し、デバイス間でのスペース共有を実現します。

## アーキテクチャ

### 高レベルアーキテクチャ

**複数デバイス間共有（最終目標）**:
```
[Magic Leap 2 Device A] ←→ [WebDAV Server] ←→ [Magic Leap 2 Device B]
         ↓                        ↓                        ↓
   [SpaceTestManager]      [InfiniCLOUD]         [SpaceTestManager]
         ↓                        ↓                        ↓
   [WebDAVSpaceManager]    [spaces/space.bin]    [WebDAVSpaceManager]
         ↓                   [spaces/meta.json]           ↓
   [ML2 Localization API]                        [ML2 Localization API]
```

**単一デバイステスト（開発・テスト段階）**:
```
[Magic Leap 2 Device] ←→ [WebDAV Server]
         ↓                        ↓
   [SpaceTestManager]      [InfiniCLOUD]
         ↓                        ↓
   [WebDAVSpaceManager]    [spaces/space.bin]
         ↓                   [spaces/meta.json]
   [ML2 Localization API]
         ↓
   [Export → Upload → Download → Import → Localize]
```

### コンポーネント構成

1. **既存コンポーネント（変更最小限）**
   - `SpaceTestManager`: 既存のUI制御とワークフロー管理
   - `MagicLeapLocalizationMapFeature`: Magic Leap 2のスペースAPI

2. **新規コンポーネント**
   - `WebDAVSpaceManager`: WebDAV操作の中核クラス
   - `WebDAVConfig`: 接続設定の管理
   - `SpaceMetadata`: スペースメタデータの構造

## コンポーネントと インターフェース

### WebDAVSpaceManager クラス

```csharp
public class WebDAVSpaceManager : MonoBehaviour
{
    // WebDAV設定
    [Header("WebDAV Configuration")]
    [SerializeField] private string webdavBaseUrl = "https://soya.infini-cloud.net/dav/";
    [SerializeField] private string webdavUser = "teragroove";
    [SerializeField] private string webdavAppPassword = "bR6RxjGW4cukpmDy";
    [SerializeField] private string remoteFolder = "spaces";

    // 公開メソッド
    public IEnumerator UploadSpaceData(byte[] spaceData, string mapId);
    public IEnumerator DownloadSpaceData(System.Action<byte[]> onComplete);
    public IEnumerator UploadMetadata(SpaceMetadata metadata);
    public IEnumerator DownloadMetadata(System.Action<SpaceMetadata> onComplete);

    // プライベートヘルパーメソッド
    private string GetAuthHeader();
    private IEnumerator PutBytes(string relativePath, byte[] data, string contentType);
    private IEnumerator GetBytes(string relativePath, System.Action<byte[]> onComplete);
    private string CombineWebDAVUrl(string baseUrl, string relativePath);
}
```

### SpaceMetadata 構造

```csharp
[System.Serializable]
public class SpaceMetadata
{
    public string mapId;
    public string createdAt;
    public long size;
    public string deviceId;
    public string version;
}
```

### WebDAVConfig 設定クラス

```csharp
[System.Serializable]
public class WebDAVConfig
{
    public string baseUrl = "https://soya.infini-cloud.net/dav/";
    public string username = "teragroove";
    public string appPassword = "bR6RxjGW4cukpmDy";
    public string remoteFolder = "spaces";
    public int timeoutSeconds = 30;
}
```

## データモデル

### ファイル構造（WebDAVサーバー上）

```
/spaces/
├── space.bin          # バイナリスペースデータ
├── meta.json          # スペースメタデータ
└── [将来の拡張用]
    ├── space_v2.bin   # 暗号化されたスペースデータ
    └── checksum.sha256 # データ整合性検証
```

### メタデータJSON構造

```json
{
  "mapId": "12345678-1234-1234-1234-123456789abc",
  "createdAt": "2024-01-15T10:30:00.000Z",
  "size": 1048576,
  "deviceId": "ML2-Device-001",
  "version": "1.0"
}
```

## エラーハンドリング

### エラー分類と対応

1. **ネットワークエラー**
   - HTTP 4xx/5xx レスポンス
   - 接続タイムアウト
   - DNS解決失敗

2. **認証エラー**
   - HTTP 401 Unauthorized
   - HTTP 403 Forbidden

3. **データエラー**
   - 空のレスポンス
   - 破損したデータ
   - サイズ不一致

4. **Magic Leap APIエラー**
   - エクスポート失敗
   - インポート失敗
   - ローカライゼーション失敗

### エラー処理戦略

```csharp
public enum WebDAVOperationResult
{
    Success,
    NetworkError,
    AuthenticationError,
    ServerError,
    DataError,
    TimeoutError,
    UnknownError
}

public class WebDAVOperationException : System.Exception
{
    public WebDAVOperationResult ResultType { get; }
    public int HttpStatusCode { get; }
    
    public WebDAVOperationException(WebDAVOperationResult resultType, string message, int httpStatusCode = 0) 
        : base(message)
    {
        ResultType = resultType;
        HttpStatusCode = httpStatusCode;
    }
}
```

## テスト戦略

### 単体テスト

1. **WebDAVSpaceManager テスト**
   - URL構築の正確性
   - 認証ヘッダーの生成
   - エラーハンドリングの動作

2. **SpaceMetadata テスト**
   - JSON シリアライゼーション/デシリアライゼーション
   - データ検証

### 統合テスト

1. **WebDAV接続テスト**
   - 実際のWebDAVサーバーへの接続
   - アップロード/ダウンロード操作
   - エラーシナリオの検証

2. **Magic Leap統合テスト**
   - スペースエクスポート → WebDAVアップロード
   - WebDAVダウンロード → スペースインポート
   - エンドツーエンドのスペース共有

### テストシナリオ

1. **単一デバイステスト（開発・実現可能性確認）**
   - 同一デバイスでスペースエクスポート → WebDAVアップロード
   - 同一デバイスでWebDAVダウンロード → スペースインポート → ローカライゼーション
   - WebDAV接続とデータ転送の基本動作確認

2. **複数デバイステスト（最終目標）**
   - デバイスAでスペースエクスポート → WebDAVアップロード
   - デバイスBでWebDAVダウンロード → スペースインポート → ローカライゼーション

3. **異常系テスト**
   - ネットワーク切断時の動作
   - 認証失敗時の動作
   - 破損データの処理

## 実装フェーズ

### フェーズ1: 基本WebDAV機能

1. WebDAVSpaceManagerクラスの実装
2. 基本的なPUT/GET操作
3. 認証機能
4. エラーハンドリング

### フェーズ2: SpaceTestManager統合

1. 既存のエクスポート機能の拡張（WebDAVアップロード追加）
2. 既存のインポート機能の拡張（WebDAVダウンロード追加）
3. UI更新（WebDAV操作の進捗表示、エラー表示）

### フェーズ3: メタデータとバリデーション

1. SpaceMetadataの実装
2. メタデータのアップロード/ダウンロード
3. データ整合性チェック

### フェーズ4: 複数デバイス対応と最適化

1. 複数デバイス間でのテスト
2. パフォーマンス最適化
3. エラー処理の改善
4. ログ記録の強化

## セキュリティ考慮事項

### 現在の実装（最小限）

1. **認証**: WebDAVベーシック認証のみ
2. **暗号化**: なし（プレーンデータ転送）
3. **検証**: なし

### 将来の拡張対応

1. **データ暗号化**: AES-GCM暗号化の準備
2. **整合性検証**: SHA-256チェックサムの準備
3. **認証強化**: より安全な認証方式への移行準備

## パフォーマンス考慮事項

### 最適化ポイント

1. **非同期処理**: UnityのCoroutineを使用した非ブロッキング操作
2. **進捗表示**: 大きなファイルのアップロード/ダウンロード進捗
3. **タイムアウト管理**: 適切なタイムアウト設定
4. **メモリ管理**: 大きなバイナリデータの効率的な処理

### 制約事項

1. **ファイルサイズ**: WebDAVサーバーの制限に依存
2. **ネットワーク速度**: モバイルネットワークでの転送時間
3. **Magic Leap 2リソース**: デバイスのメモリとCPU制約

## 設定管理

### 開発時設定

```csharp
[System.Serializable]
public class WebDAVSettings
{
    [Header("Development Settings")]
    public bool enableDebugLogging = true;
    public bool useTestCredentials = true;
    public int connectionTimeoutSeconds = 30;
    public int uploadTimeoutSeconds = 120;
    public int downloadTimeoutSeconds = 120;
}
```

### 本番環境への準備

1. **設定の外部化**: ScriptableObjectまたは設定ファイル
2. **認証情報の保護**: 暗号化された設定ファイル
3. **環境別設定**: 開発/テスト/本番環境の切り替え

## 監視とログ記録

### ログレベル

1. **DEBUG**: 詳細な操作ログ
2. **INFO**: 正常な操作の記録
3. **WARNING**: 回復可能なエラー
4. **ERROR**: 重大なエラー

### 監視項目

1. **操作成功率**: アップロード/ダウンロードの成功率
2. **応答時間**: WebDAV操作の応答時間
3. **エラー頻度**: エラータイプ別の発生頻度
4. **データサイズ**: 転送されるスペースデータのサイズ分布