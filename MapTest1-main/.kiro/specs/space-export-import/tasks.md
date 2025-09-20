# 実装計画

- [x] 1. プロジェクト設定とOpenXR機能の有効化
  - Magic Leap 2 Localization Maps機能をOpenXR設定で有効化
  - 必要なManifest権限（SPACE_MANAGER、SPACE_IMPORT_EXPORT、SPATIAL_ANCHOR）を追加
  - ビルド設定の確認とテスト
  - _要件: 5.1, 5.2, 5.3, 5.4_

- [x] 2. SpaceTestManagerスクリプトの基本構造作成
  - Assets/Scripts/SpaceTestManager.csファイルを作成
  - MonoBehaviourを継承した基本クラス構造を実装
  - UI要素のSerializeField宣言（Button x3, Text x1）
  - 基本的なStart()メソッドとボタンイベントハンドラーのスケルトンを実装
  - _要件: 4.1_

- [x] 3. 権限管理機能の実装
  - SpaceTestManager内に権限チェック機能を実装
  - CheckAndRequestPermissionsAsync()メソッドを実装
  - 必要な権限の定数定義と権限要求ロジックを実装
  - 権限拒否時のエラーハンドリングを実装
  - _要件: 5.1, 5.2, 5.3, 5.4_

- [x] 4. Spaceエクスポート機能の実装
  - OnExportButtonClicked()メソッドを実装
  - MagicLeapLocalizationMapFeatureを使用した最新Space取得ロジックを実装
  - ExportLocalizationMap()を使用したSpaceデータエクスポート機能を実装
  - /sdcard/Download/space.binへのファイル保存機能を実装
  - エクスポート成功・失敗時のステータス表示を実装
  - _要件: 1.1, 1.2, 1.3, 1.4_

- [x] 5. Spaceインポート機能の実装
  - OnImportButtonClicked()メソッドを実装
  - /sdcard/Download/space.binからのファイル読み込み機能を実装
  - ImportLocalizationMap()を使用したSpaceデータインポート機能を実装
  - インポート成功時の新しいSpace ID表示機能を実装
  - インポート失敗時のエラーハンドリングを実装
  - _要件: 2.1, 2.2, 2.3, 2.4_

- [x] 6. ローカライゼーション機能の実装
  - OnLocalizeButtonClicked()メソッドを実装
  - 最新インポートされたSpaceへのローカライゼーション要求機能を実装
  - OnLocalizationChangedEventイベントの購読と処理を実装
  - ローカライゼーション成功時のマップ原点ポーズ表示を実装
  - ローカライゼーション失敗時のエラー情報表示を実装
  - _要件: 3.1, 3.2, 3.3, 3.4_

- [x] 7. エラーハンドリングとステータス表示の実装
  - UpdateStatus()メソッドを実装してUI Text要素を更新
  - HandleError()メソッドを実装して統一的なエラー処理を提供
  - 各操作の進行状況と結果をユーザーに分かりやすく表示
  - 日本語エラーメッセージの定義と表示
  - _要件: 1.3, 1.4, 2.3, 2.4, 3.3, 3.4, 4.2_

- [x] 8. ML2basic.unityシーンへのUI要素追加
  - ML2basic.unityシーンを開いてCanvas GameObjectを追加
  - Export、Import、LocalizeボタンをCanvasに配置
  - ステータス表示用のTextコンポーネントを配置
  - SpaceTestManagerスクリプトを持つGameObjectを作成
  - UI要素とSpaceTestManagerスクリプトの参照を設定
  - _要件: 4.1, 4.2_

- [x] 9. PCでのビルドテストと検証
  - プロジェクトのコンパイルエラーがないことを確認
  - Android向けビルドが成功することを確認
  - UI要素が正しく配置されていることをシーンビューで確認
  - スクリプトの依存関係と参照が正しく設定されていることを確認
  - _要件: 全要件のビルド検証_

- [x] 10. Magic Leap 2での動作確認とテスト
  - Magic Leap 2デバイスにアプリをデプロイ
  - 権限要求フローが正しく動作することを確認
  - Export → Import → Localizeの完全フローをテスト
  - 各種エラーケース（権限拒否、ファイル不存在など）の動作確認
  - 実際のSpaceデータでの機能検証
  - _要件: 全要件の実機検証_