# Magic Leap 2 Testing Guide - Space Localization Feature

## 概要
このガイドは、Magic Leap 2デバイスでのSpace localization機能（位置追跡とイベント検出）の動作確認とテストを実行するための詳細な手順を提供します。

## 前提条件

### 必要な環境
- Magic Leap 2デバイス（OS 1.4.0以降推奨）
- Unity 6000.2.0f1 with URP
- Magic Leap Unity SDK 2.6.0
- XR Interaction Toolkit（コントローラ入力用）
- Android SDK（API Level 29以降）
- USB-Cケーブル（デバイス接続用）

### 事前準備
1. Magic Leap 2デバイスが開発者モードに設定されていること
2. USBデバッグが有効になっていること
3. デバイスがPCに正しく接続されていること
4. adbコマンドでデバイスが認識されることを確認
5. OpenXR Localization Maps featureが有効になっていること

## テスト手順

### Phase 1: アプリケーションのデプロイ

#### 1.1 ビルドとデプロイ
```bash
# デバイス接続確認
adb devices

# アプリケーションのビルドとインストール
# Unity Editor -> File -> Build Settings -> Build and Run
```

**検証ポイント:**
- [ ] ビルドエラーが発生しないこと
- [ ] デバイスへのインストール（com.TeraGrove.ML2starter）が成功すること
- [ ] アプリケーションが正常に起動すること

#### 1.2 XR Interaction確認
**検証ポイント:**
- [ ] World Space CanvasがHMD内で表示されること
- [ ] コントローラでUI要素が選択できること
- [ ] Export、Import、Localize、Force Re-localizeボタンが表示されること
- [ ] ステータステキストが表示されること

### Phase 2: Localization Event Testing

#### 2.1 基本的なLocalization動作確認
**テスト手順:**
1. アプリケーションを起動
2. 「Localize」ボタンをコントローラでタップ
3. Localization状態の変化を観察

**検証ポイント:**
- [ ] Localization要求が送信されること
- [ ] イベントベースの検出が動作すること（`EnableLocalizationEvents(true)`）
- [ ] デバイス位置がSpace原点に対して表示されること
- [ ] ステータステキストが適切に更新されること

#### 2.2 Force Re-localization機能
**テスト手順:**
1. 「Force Re-localize」ボタンをタップ
2. 再Localization処理を確認

**検証ポイント:**
- [ ] 強制再Localizationが動作すること
- [ ] テスト目的での再Localizationができること
- [ ] イベント後もPollingが継続されること

### Phase 3: File Operations Testing

#### 3.1 Space Export機能のテスト
**前提条件:**
- Space Management権限が付与されていること

**テスト手順:**
1. 「Export」ボタンをコントローラでタップ
2. エクスポート処理の進行状況を確認
3. 完了メッセージを確認

**検証ポイント:**
- [ ] エクスポート処理が開始されること
- [ ] Application.persistentDataPathへのファイル保存が成功すること
- [ ] エクスポート成功時に確認メッセージが表示されること
- [ ] 実際にファイルが作成されていること

**ファイル確認コマンド:**
```bash
# persistentDataPath内のファイル確認
adb shell run-as com.TeraGrove.ML2starter ls -la /data/data/com.TeraGrove.ML2starter/files/
```

#### 3.2 Space Import機能のテスト
**テスト手順:**
1. 「Import」ボタンをコントローラでタップ
2. インポート処理の進行状況を確認
3. 完了メッセージを確認

**検証ポイント:**
- [ ] インポート処理が開始されること
- [ ] persistentDataPathからのファイル読み込みが成功すること
- [ ] インポート成功/失敗メッセージが適切に表示されること

#### 3.3 Event-based Localization Detection
**テスト手順:**
1. アプリケーション開始時の自動検出を確認
2. `EnableLocalizationEvents(true)`の動作を確認
3. 継続的なイベント検出を確認

**検証ポイント:**
- [ ] アプリ起動時にLocalizationイベント検出が開始されること
- [ ] Localization成功時にイベントが発火すること
- [ ] 継続的なイベント検出が動作すること（既知の動作）
- [ ] デバイス位置がSpace原点に対して正確に表示されること

### Phase 4: エラーケースの動作確認

#### 4.1 権限不足エラーのテスト
**テスト手順:**
1. デバイス設定でSpace Management権限を無効化
2. Export/Import機能を実行

**検証ポイント:**
- [ ] 権限不足時に適切なエラーメッセージが表示されること
- [ ] 権限再要求の案内が表示されること

#### 4.2 ファイル不存在エラーのテスト
**テスト手順:**
1. Import用ファイルが存在しない状態でImportボタンをタップ

**検証ポイント:**
- [ ] ファイル不存在エラーが適切に表示されること
- [ ] 「インポート用ファイルが見つかりません」メッセージが表示されること

#### 4.3 Localization失敗のテスト
**テスト手順:**
1. 異なる環境（Spaceが作成されていない場所）でLocalizationを実行
2. 失敗ケースの動作を確認

**検証ポイント:**
- [ ] Localization失敗時に適切なエラーメッセージが表示されること
- [ ] 失敗理由が分かりやすく表示されること

### Phase 5: 実環境でのValidation

#### 5.1 実際のSpace環境での検証
**テスト手順:**
1. Magic Leap 2で実際の空間をスキャンしてSpaceを作成
2. Export → Import → Localizationの完全フローを実行

**検証ポイント:**
- [ ] 実際のSpace環境でLocalizationが成功すること
- [ ] 位置座標が合理的な値であること
- [ ] 継続的なイベント検出が安定して動作すること

## デバッグとトラブルシューティング

### ログの確認
```bash
# アプリ固有のログ確認（推奨）
adb logcat --pid=$(adb shell pidof -s com.TeraGrove.ML2starter) | grep -E "Unity|Localization|Space"

# アプリケーション固有のログ
adb logcat | grep SpaceTestManager

# Magic Leap Localization関連ログ
adb logcat | grep -E "MagicLeap|Localization|OpenXR"
```

### 一般的な問題と解決策

#### 問題1: コントローラでUIが操作できない
**解決策:**
- XR Ray Interactor が正しく設定されているか確認
- Canvas が World Space に設定されているか確認
- TrackedDeviceGraphicRaycaster コンポーネントが Canvas にアタッチされているか確認
- EventSystem with XRUIInputModule が存在するか確認

#### 問題2: Localizationイベントが発火しない
**解決策:**
- OpenXR設定でLocalization Maps機能が有効になっているか確認
- `EnableLocalizationEvents(true)` が呼び出されているか確認
- Space Management権限が付与されているか確認

#### 問題3: ファイル操作が失敗する
**解決策:**
- `Application.persistentDataPath` へのアクセス権限を確認
- ファイルパスが正しく構築されているか確認
- Android固有のファイルアクセス制限を考慮

## テスト結果の記録

### テスト実行チェックリスト
- [ ] Phase 1: アプリケーションのデプロイ
- [ ] Phase 2: Localization Event Testing
- [ ] Phase 3: File Operations Testing
- [ ] Phase 4: エラーケースの動作確認
- [ ] Phase 5: 実環境でのValidation

## 完了基準
以下の条件がすべて満たされた場合、Space Localization機能のテストは完了とみなします：

1. ✅ Magic Leap 2デバイスにアプリが正常にデプロイされること
2. ✅ XR Interactionによるコントローラ操作が動作すること
3. ✅ Event-based Localization検出が動作すること
4. ✅ Export/Import file操作が動作すること
5. ✅ 各種エラーケースで適切なエラーハンドリングが動作すること
6. ✅ 実際のSpace環境で安定して動作すること

## 実装ノート

### 既知の動作
- **継続的なイベント検出**: Localization成功後、イベントが継続的に発火する（仕様通り）
- **Simple ID Strategy**: Localization target IDは単純な上書き方式（クリアなし）
- **persistentDataPath**: AndroidファイルアクセスのためpersistentDataPathを使用
- **Canvas配置**: Y=-0.2 位置でのWorld Space配置が推奨