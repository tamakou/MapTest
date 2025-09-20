https://chatgpt.com/s/t_68ca1fd6c8c881919d25aff9696378bb

# 基本要件

- 現在の codebase に、WebDAV を使った、スペースファイルの共有機能を追加する
- 現状の実装の、スペースファイルを端末のローカルストレージに保存する部分を、WebDAV　に保存するように変更する
- WebDAV のサーバーは設定済み、接続情報は固定であり、下記の設定情報をコードに埋め込む
- 下記に実装の骨子の部分のサンプルを示す。このサンプルを参考に、WebDAV の実装を 現在の codebase  に反映する

# 実装方針

- 本アプリは 技術の Feasibility テスト用の一時利用用であり、セキュリティやエラー対応を最小限にとどめ、Feasibility を最短の時間で確認できるように、シンプルに実装する
- 既存の codebase の変更は最小限にとどめ、既存の実装を予め分析し、最大限活用する

# 実装のための最小サンプル

- 前提：`INTERNET` と `ACCESS_NETWORK_STATE` の権限（AndroidManifestで付与）、Magic Leap側は既に `SPACE_MANAGER` / `SPACE_IMPORT_EXPORT` を設定済み。
- WebDAVは **ベーシック認証**で接続します（接続ID + アプリパスワード）。

```csharp
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.OpenXR;
using MagicLeap.OpenXR.Features.LocalizationMaps;

public class SpaceShareSample : MonoBehaviour
{
    [Header("InfiniCLOUD WebDAV")]
    [SerializeField] string webdavBase = "https://XXXX.infini-cloud.net/dav/"; // マイページのURL（末尾 /dav/ を含む）
    [SerializeField] string webdavUser = "<接続ID>";        // マイページに表示の「接続ID」
    [SerializeField] string webdavAppPassword = "<アプリパスワード>"; // 同「アプリパスワード」
    [SerializeField] string remoteFolder = "spaces";         // DAV上の保存先フォルダ（存在しなくてもPUT可）

    MagicLeapLocalizationMapFeature feature;

    void Awake()
    {
        feature = OpenXRSettings.Instance.GetFeature<MagicLeapLocalizationMapFeature>();
        if (feature == null || !feature.enabled)
            Debug.LogError("LocalizationMaps feature is disabled.");
    }

    // 1) Export → 暗号化は省略（まずはプレーンで確認）→ WebDAVにPUT
    public void ExportAndUpload(string mapId)
    {
        if (!feature.ExportLocalizationMap(mapId, out byte[] mapData))
        {
            Debug.LogError("Export failed.");
            return;
        }
        // 実運用では mapData をAES-GCM暗号化してからPUT推奨
        StartCoroutine(PutBytes($"{remoteFolder}/space.bin", mapData));
        // 付随メタ
        var meta = JsonUtility.ToJson(new Meta
        {
            mapId = mapId,
            createdAt = DateTime.UtcNow.ToString("o"),
            size = mapData.Length,
        });
        StartCoroutine(PutBytes($"{remoteFolder}/meta.json", Encoding.UTF8.GetBytes(meta), "application/json"));
    }

    // 2) WebDAVからGET → Import → Localize
    public void DownloadImportAndLocalize()
    {
        StartCoroutine(GetBytes($"{remoteFolder}/space.bin", (data) =>
        {
            if (data == null) { Debug.LogError("Download failed."); return; }

            // 復号が必要ならここで（今回はプレーン想定）
            if (!feature.ImportLocalizationMap(data, out string importedMapId))
            {
                Debug.LogError("Import failed.");
                return;
            }
            Debug.Log("Imported: " + importedMapId);
            feature.RequestMapLocalization(importedMapId);
        }));
    }

    // --- WebDAVヘルパ ---
    string AuthHeader() {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{webdavUser}:{webdavAppPassword}"));
        return $"Basic {token}";
    }

    IEnumerator PutBytes(string relativePath, byte[] body, string contentType = "application/octet-stream")
    {
        var url = CombineDavUrl(webdavBase, relativePath);
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
        req.uploadHandler = new UploadHandlerRaw(body) { contentType = contentType };
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", AuthHeader());
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"PUT error: {req.responseCode} {req.error}");
        else
            Debug.Log($"PUT OK: {url}");
    }

    IEnumerator GetBytes(string relativePath, Action<byte[]> onDone)
    {
        var url = CombineDavUrl(webdavBase, relativePath);
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", AuthHeader());
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"GET error: {req.responseCode} {req.error}");
            onDone?.Invoke(null);
        }
        else onDone?.Invoke(req.downloadHandler.data);
    }

    static string CombineDavUrl(string baseUrl, string relative)
    {
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return baseUrl + relative.TrimStart('/');
    }

    [Serializable]
    class Meta { public string mapId; public string createdAt; public long size; }
}

```

# WebDAV接続情報

WebDAV接続URL： https://soya.infini-cloud.net/dav/

接続ID:：teragroove

アプリパスワード：bR6RxjGW4cukpmDy

# 参考情報

### 使い方（最短）

1. InfiniCLOUDに登録 → **マイページ→外部アプリ接続を許可** → 表示された
    
    **WebDAV接続URL / 接続ID / アプリパスワード** を控える。([The most versatile cloud, InfiniCLOUD](https://infini-cloud.net/ja/support_account_login-settings_apps.html?utm_source=chatgpt.com))
    
2. 上記スクリプトを `SpaceShareSample.cs` として配置し、GameObjectにアタッチ。
    - `webdavBase` に **接続URL**（例：`https://xxxx.infini-cloud.net/dav/`）
    - `webdavUser` に **接続ID**
    - `webdavAppPassword` に **アプリパスワード** を設定。([The most versatile cloud, InfiniCLOUD](https://infini-cloud.net/ja/use_usage-apps.html?utm_source=chatgpt.com))
3. 端末Aで `ExportAndUpload(<mapId>)` を呼ぶ（UIボタンからでもOK）。
4. 端末Bで `DownloadImportAndLocalize()` を呼ぶ。**Localized** になれば共有成功。

> 注意：最初はプレーン（非暗号）で疎通確認 → OKになったらAES-GCM暗号化とSHA-256検証を追加してください。URL/パスワードはログに出さない運用で。
> 

---

# 参考（公式ドキュメント）

- WebDAVのURL・外部アプリ接続の流れ（**接続URL/ID/アプリパスワード**）([The most versatile cloud, InfiniCLOUD](https://infini-cloud.net/ja/support_account_login-settings_apps.html?utm_source=chatgpt.com))
- WebDAV URLの例と案内（`.../dav/`、ユーザーごとに異なる）([The most versatile cloud, InfiniCLOUD](https://infini-cloud.net/ja/use_usage-apps.html?utm_source=chatgpt.com))
- 無料20GBの案内（トップページ）([The most versatile cloud, InfiniCLOUD](https://infini-cloud.net/?utm_source=chatgpt.com))

このサンプルでまず**エクスポート→PUT／GET→インポート**の往復を固めましょう。うまくいったら、**QRでURL共有**・**期限付き共有リンク**・**暗号化**・**メタJSONの検証**を段階的に足していけば、そのままPoC〜実運用に伸ばせます。