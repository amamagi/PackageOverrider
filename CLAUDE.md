# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

Package Overrider は Unity Editor 拡張で、Unity Package Manager で管理されているパッケージを、ローカルディレクトリのパッケージに一時的に差し替えるツールです。開発中のパッケージをテストする際に、manifest.json を直接編集せずに GUI から簡単にパッケージソースを切り替えることができます。

## 開発環境

- **Unity バージョン**: 6000.3.2f1
- **プラットフォーム**: Unity Editor (Windows, macOS, Linux)
- **言語**: C# (Unity Editor API)

## プロジェクト構成

```
PackageOverrider/
├── Packages/
│   ├── PackageOverrider/          # 実際のパッケージコード
│   │   ├── Editor/
│   │   │   └── PackageOverrideWindow.cs  # メインのEditorWindowクラス
│   │   └── package.json           # パッケージ定義
│   └── manifest.json              # プロジェクトの依存関係
├── Assets/                        # Unity プロジェクトアセット（テスト用）
└── ProjectSettings/               # Unity プロジェクト設定
```

## アーキテクチャ

### PackageOverrideWindow クラス (核心コンポーネント)

このツールの全機能を担う単一の EditorWindow クラスです。

**主要な責務:**
1. **manifest.json の読み込みと解析** - 正規表現を使用してパッケージの依存関係を抽出
2. **ユーザー設定の永続化** - `UserSettings/PackageOverrideSettings.json` にオーバーライド設定を保存
3. **GUI の描画** - パッケージリストの表示、フィルタリング、オーバーライドパスの設定
4. **manifest.json の書き換え** - パッケージソースを `file:` プロトコルのローカルパスに置き換え

### データモデル

- **PackageOverrideEntry**: 個々のパッケージのオーバーライド情報
  - `packageName`: パッケージの識別子（例: "com.unity.inputsystem"）
  - `originalSource`: 元のパッケージソース（バージョン番号や Git URL など）
  - `overridePath`: オーバーライド先のローカルディレクトリパス
  - `isOverridden`: オーバーライドが有効かどうかのフラグ

- **PackageOverrideData**: すべてのオーバーライド設定のコンテナ

### 重要な処理フロー

1. **manifest.json の解析** (`ParseDependencies`):
   - JSON を文字列として扱い、正規表現で "dependencies" セクションを抽出
   - Unity の JsonUtility は Dictionary をサポートしないため、正規表現を使用

2. **変更の適用** (`ApplyChanges`):
   - オーバーライド有効時: パッケージソースを `file:/path/to/package` に置き換え
   - オーバーライド無効時: 元のソースに戻す
   - manifest.json を書き換え後、`PackageManager.Client.Resolve()` を呼び出して Unity にパッケージを再解決させる

3. **設定の永続化**:
   - ユーザー設定は `UserSettings/PackageOverrideSettings.json` に保存
   - これは Git に含まれないため、各開発者が個別の設定を持てる

## 開発時の注意点

### Unity Editor API の制約

- **JsonUtility の制限**: Dictionary を直接シリアライズできないため、正規表現で JSON を手動解析
- **パッケージの再解決**: manifest.json を変更した後は必ず `PackageManager.Client.Resolve()` を呼び出す必要がある

### ファイルパスの扱い

- Windows のバックスラッシュを常にスラッシュに変換 (`Replace("\\", "/")`)
- manifest.json 内のパスは `file:` プロトコルで指定

### エラーハンドリング

- package.json の存在チェックを行い、無効なパスの場合は警告を表示
- manifest.json の読み込みや書き込みに失敗した場合は、エラーログを出力

## テスト方法

Unity Editor 内でのテスト:
1. Unity で本プロジェクトを開く
2. `Window > Package Management > Package Overrider` からウィンドウを起動
3. 任意のパッケージを選択し、ローカルパッケージのパスを指定
4. "Apply Changes" をクリックして manifest.json が書き換えられることを確認
5. Unity が自動的にパッケージを再読み込みすることを確認

## 言語とコメント

- このプロジェクトは日本語のコメントと UI を使用しています
- 新しいコードを追加する際は、既存のコメントスタイルに従ってください
