# 実装計画

## フェーズ1: WebDAV基本機能の実装

- [x] 1. WebDAVSpaceManagerクラスの作成
  - Assets/Scripts/WebDAVSpaceManager.csファイルを作成する
  - MonoBehaviourを継承したクラスを定義する
  - WebDAV接続設定フィールドを実装する（埋め込み認証情報）
  - _要件: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 1.1 クラスの基本構造
  ```csharp
  - クラス定義: public class WebDAVSpaceManager : MonoBehaviour
  - 接続設定フィールド:
    [SerializeField] private string webdavBaseUrl = "https://soya.infini-cloud.net/dav/";
    [SerializeField] private string webdavUser = "teragroove";
    [SerializeField] private string webdavAppPassword = "bR6RxjGW4cukpmDy";
    [SerializeField] private string remoteFolder = "spaces";
  ```
  - MagicLeapLocalizationMapFeatureへの参照フィールドを追加
  - _実装注意: 認証情報はハードコードで埋め込み_

- [x] 1.2 認証ヘッダー生成メソッド
  ```csharp
  private string GetAuthHeader()
  {
      var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{webdavUser}:{webdavAppPassword}"));
      return $"Basic {token}";
  }
  ```
  - System.Text.Encodingのusing追加が必要
  - _実装注意: Basic認証のみ（セキュリティは最小限）_

- [x] 1.3 URL結合ヘルパーメソッド
  ```csharp
  private string CombineWebDAVUrl(string baseUrl, string relativePath)
  {
      if (!baseUrl.EndsWith("/")) baseUrl += "/";
      return baseUrl + relativePath.TrimStart('/');
  }
  ```
  - _実装注意: スラッシュの重複を防ぐ処理が重要_

- [x] 1.4 PUTメソッド実装
  ```csharp
  private IEnumerator PutBytes(string relativePath, byte[] body, string contentType = "application/octet-stream")
  {
      var url = CombineWebDAVUrl(webdavBaseUrl, relativePath);
      using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
      req.uploadHandler = new UploadHandlerRaw(body) { contentType = contentType };
      req.downloadHandler = new DownloadHandlerBuffer();
      req.SetRequestHeader("Authorization", GetAuthHeader());
      yield return req.SendWebRequest();

      if (req.result != UnityWebRequest.Result.Success)
          Debug.LogError($"PUT error: {req.responseCode} {req.error}");
      else
          Debug.Log($"PUT OK: {url}");
  }
  ```
  - UnityEngine.Networkingのusing追加が必要
  - _実装注意: エラーハンドリングは最小限（ログ出力のみ）_

- [x] 1.5 GETメソッド実装
  ```csharp
  private IEnumerator GetBytes(string relativePath, Action<byte[]> onDone)
  {
      var url = CombineWebDAVUrl(webdavBaseUrl, relativePath);
      using var req = UnityWebRequest.Get(url);
      req.SetRequestHeader("Authorization", GetAuthHeader());
      yield return req.SendWebRequest();

      if (req.result != UnityWebRequest.Result.Success)
      {
          Debug.LogError($"GET error: {req.responseCode} {req.error}");
          onDone?.Invoke(null);
      }
      else
          onDone?.Invoke(req.downloadHandler.data);
  }
  ```
  - System.Actionのusing追加が必要
  - _実装注意: コールバックパターンでデータを返す_

## フェーズ2: スペースデータ管理機能の実装

- [x] 2. メタデータとアップロード/ダウンロード機能

- [x] 2.1 メタデータクラスの定義
  ```csharp
  [System.Serializable]
  public class SpaceMetadata
  {
      public string mapId;
      public string createdAt;
      public long size;
  }
  ```
  - _実装注意: 最小限のフィールドのみ実装（deviceId, versionは将来拡張用）_
  - JsonUtility.ToJson/FromJsonで動作確認

- [x] 2.2 スペースデータアップロードメソッド
  ```csharp
  public IEnumerator UploadSpaceData(byte[] spaceData, string mapId)
  {
      // space.binのアップロード
      yield return PutBytes($"{remoteFolder}/space.bin", spaceData);

      // メタデータの作成とアップロード
      var meta = new SpaceMetadata
      {
          mapId = mapId,
          createdAt = DateTime.UtcNow.ToString("o"),
          size = spaceData.Length
      };
      var metaJson = JsonUtility.ToJson(meta);
      yield return PutBytes($"{remoteFolder}/meta.json",
                           Encoding.UTF8.GetBytes(metaJson), "application/json");
  }
  ```
  - _実装注意: 2つのファイルを順次アップロード_

- [x] 2.3 スペースデータダウンロードメソッド
  ```csharp
  public IEnumerator DownloadSpaceData(Action<byte[]> onComplete)
  {
      yield return GetBytes($"{remoteFolder}/space.bin", onComplete);
  }
  ```
  - _実装注意: シンプルなコールバックパターン_

- [x] 2.4 WebDAVSpaceManagerの初期化
  - MagicLeapLocalizationMapFeatureへの参照を取得
  ```csharp
  void Awake()
  {
      var feature = OpenXRSettings.Instance.GetFeature<MagicLeapLocalizationMapFeature>();
      if (feature == null || !feature.enabled)
          Debug.LogError("LocalizationMaps feature is disabled.");
  }
  ```
  - _実装注意: 既存のSpaceTestManagerと同じ初期化パターン_

## フェーズ3: SpaceTestManager統合

- [x] 3. 既存コードへの統合

- [x] 3.1 WebDAVSpaceManager参照の追加
  - SpaceTestManagerクラスに追加:
  ```csharp
  [Header("WebDAV Integration")]
  [SerializeField] private WebDAVSpaceManager webdavManager;
  ```
  - Start()メソッドで初期化確認:
  ```csharp
  if (webdavManager == null)
      webdavManager = GetComponent<WebDAVSpaceManager>();
  ```
  - _実装注意: 同じGameObjectにアタッチされることを想定_

- [x] 3.2 OnExportButtonClicked()の修正
  - 既存のローカル保存処理（SaveSpaceToFileAsync）の後に追加:
  ```csharp
  // WebDAVへのアップロード（webdavManagerが存在する場合のみ）
  if (webdavManager != null)
  {
      ShowOperationProgress("Spaceエクスポート", "WebDAVにアップロード中", 4, 4);
      yield return webdavManager.UploadSpaceData(exportResult.data, latestSpace.Value.MapUUID);
      ShowOperationSuccess("Spaceエクスポート",
          $"エクスポート完了\nローカル: {SPACE_FILE_PATH}\nWebDAV: 成功");
  }
  ```
  - _実装注意: 既存処理を壊さないように最後に追加_

- [x] 3.3 OnImportButtonClicked()の修正
  - ReadSpaceFromFileAsync()の代わりにWebDAVから取得:
  ```csharp
  if (webdavManager != null)
  {
      ShowOperationProgress("Spaceインポート", "WebDAVからダウンロード中", 1, 2);
      byte[] downloadedData = null;
      yield return webdavManager.DownloadSpaceData((data) => downloadedData = data);

      if (downloadedData != null)
      {
          // 既存のImportSpaceAsync処理を使用
          var importResult = await ImportSpaceAsync(downloadedData);
          // 以降は既存処理と同じ
      }
  }
  else
  {
      // 既存のローカルファイル読み込み処理（フォールバック）
      var fileReadResult = await ReadSpaceFromFileAsync();
  }
  ```
  - _実装注意: WebDAVが有効な場合のみ使用、無効時は既存処理_

- [x] 3.4 Unity Editorでのシーン設定
  1. ML2basic.unityシーンを開く
  2. SpaceTestManagerがアタッチされているGameObjectを選択
  3. WebDAVSpaceManagerコンポーネントを同じGameObjectに追加
  4. SpaceTestManagerのWebDAV Manager参照にWebDAVSpaceManagerを設定
  5. WebDAVSpaceManagerの接続設定が正しいことを確認
  - _実装注意: Inspectorで設定値が見えることを確認_
  - ✅ **完了**: ML2basicシーンにWebDAVSpaceManagerが統合済み

## フェーズ4: 動作確認とテスト

- [x] 4. 実装後の動作確認手順

- [x] 4.1 Magic Leap 2実機での動作確認
  1. ✅ アプリのデプロイとWebDAV権限の確認（インターネット権限追加）
  2. ✅ エクスポートボタンでのSpace Export + WebDAVアップロード
  3. ✅ WebDAVサーバー上でunity_exported_space.zipファイルの確認
  4. ✅ インポートボタンでのWebDAVダウンロード + Space Import
  5. ✅ ローカライゼーション機能の動作確認
  6. ✅ 重複実行防止機能の確認（Magic Leap 2のタッチ入力問題対応）

- [x] 4.2 WebDAVサーバー上のファイル確認
  - ✅ WebDAVサーバー（https://soya.infini-cloud.net/dav/spaces/）へのアクセス確認
  - ✅ unity_exported_space.zipファイルが正しく作成されることを確認
  - ✅ ファイルサイズと整合性の確認
  - **注意**: 最終実装では単一ZIPファイル方式（space.bin+meta.jsonから変更）

- [x] 4.3 エラーケースとパフォーマンステスト
  - ✅ ネットワーク接続テスト: 高速アップロード/ダウンロード動作を確認
  - ✅ 重複実行防止: Magic Leap 2のタッチ入力重複問題を解決
  - ✅ フォールバック動作: WebDAV無効時の既存ローカル処理確認
  - ✅ 認証とサーバー接続の安定性確認

## 実装の重要ポイント

### セキュリティ考慮事項
- 現在の実装は実現可能性テスト用のため、認証情報はハードコード
- 本番環境では暗号化とセキュアな認証情報管理が必要
- HTTPSを使用しているがデータ自体は暗号化されていない

### パフォーマンス考慮事項
- スペースファイルは数MB程度になる可能性がある
- UnityWebRequestは非同期処理のため、UIはブロックされない
- タイムアウト処理は実装していない（将来の拡張項目）

### 既存コードへの影響
- WebDAVSpaceManagerが存在しない場合は既存処理にフォールバック
- 既存のローカルファイル保存機能は残す（併用可能）
- UIや権限処理には一切変更を加えない

## フェーズ5: 実装完了と最終調整

- [x] 5. 品質改善と実装完了

- [x] 5.1 重複実行問題の解決
  - ✅ Magic Leap 2のタッチ入力による重複クリック問題を特定
  - ✅ 操作進行中フラグ（isExportInProgress, isImportInProgress）を実装
  - ✅ シンプルで効果的な重複防止ソリューション完成

- [x] 5.2 デバッグコードのクリーンアップ
  - ✅ テスト用のデバッグログとダウンロード確認処理を削除
  - ✅ プロダクション用のクリーンなコードに整理

- [x] 5.3 移植性向上のための日本語コメント追加
  - ✅ WebDAVSpaceManager.cs: 移植時の注意点と主要機能の日本語コメント
  - ✅ SpaceTestManager.cs: WebDAV統合部分と重複防止機能の日本語コメント
  - ✅ 他のアプリケーションへの移植を想定した詳細な解説

- [x] 5.4 ドキュメント更新
  - ✅ README.md: WebDAV統合機能と重複クリック防止機能を追加
  - ✅ tasks.md: 全フェーズの完了状況を更新（本更新）

## 最終実装結果

✅ **プロジェクト完了**: WebDAVを使用したMagic Leap 2スペース共有機能が完全に実装され、テスト完了

### 主要な成果
1. **WebDAV統合**: InfiniCLOUD WebDAVサーバーとの完全統合
2. **ファイル形式最適化**: unity_exported_space.zip単一ファイル方式
3. **Magic Leap 2対応**: タッチ入力の重複実行問題を解決
4. **高いパフォーマンス**: 高速アップロード/ダウンロード確認
5. **移植性確保**: 日本語コメントによる他アプリへの移植容易性
6. **後方互換性**: 既存ローカル機能との完全共存

## AndroidManifest.xmlの更新（✅ 完了済み）

Assets/Plugins/Android/AndroidManifest.xmlに以下の権限を追加済み:
```xml
<!-- WebDAV Internet Access Permissions -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```
✅ **インターネットアクセス権限は設定済みです。Magic Leap 2実機でWebDAV機能が利用可能です。**