# Trimx

[English](README.md) | 日本語

Windows 11 向けの軽量画像編集アプリ。`Win + Shift + S` でスクリーンショットを撮ったあと、クリップボードの画像をすぐに編集・加工・出力することに特化しています。

画面キャプチャ機能は持ちません（Windows 標準機能を利用します）。

## コンセプト

- Snipping Tool の手軽さと高速起動
- Photoshop のようなレイヤー編集（テキスト・図形は後から編集できる）
- シングルウィンドウ + タブによる複数画像の管理
- クリップボードとの連携を重視

## 主な機能

| 分類 | 内容 |
|---|---|
| 取り込み | 起動時にクリップボード画像を自動取得 / 貼り付け（Ctrl+V）/ ファイルを開く・ドラッグ＆ドロップ（PNG・JPEG・WebP・BMP・`.trimx`） |
| レイヤー | 画像・テキスト・図形（矩形・楕円・矢印・線）。表示切替、不透明度、並べ替え、複製、結合、ラスタライズ、サムネイル |
| 編集 | ペン、テキスト（太字・斜体・影・縁取り）、図形の線種・角丸、範囲選択、切り抜き、切り取り、キャンバス / 画像サイズ変更 |
| 履歴 | Undo / Redo（Ctrl+Z / Ctrl+Y）、未保存の確認 |
| 出力 | PNG / JPEG / WebP 書き出し、クリップボードへコピー、プロジェクト保存（`.trimx`） |
| 表示 | カーソル位置基準のズーム、フィット（Ctrl+0）、100%（Ctrl+1）、ダーク / ライトテーマ |

ショートカットの一覧は [仕様書](docs/plan.md)（日本語）の 7.2 を参照してください。

## インストール

[Releases](https://github.com/shin32t/Trimx/releases) からインストーラ（`Trimx-<バージョン>-win-x64-setup.exe`）またはポータブル版の zip（`Trimx-<バージョン>-win-x64.zip`）をダウンロードしてください。インストーラは管理者権限が不要で、既定では `%LOCALAPPDATA%\Programs\Trimx` にインストールされます。

> **Windows SmartScreen の警告:** Trimx はコード署名をしていないため、インストーラの実行時に「Windows によって PC が保護されました」と表示されることがあります。**詳細情報** をクリックし、**実行** を選んでください。

## 技術スタック

| 項目 | 採用技術 |
|---|---|
| 対応 OS | Windows 11（x64） |
| 言語 / ランタイム | C# / .NET 10 |
| UI | WPF |
| 描画エンジン | [SkiaSharp](https://github.com/mono/SkiaSharp) |
| 保存形式 | `.trimx`（ZIP コンテナ: `project.json` + レイヤーごとの PNG） |

NuGet パッケージは `SkiaSharp` と `SkiaSharp.Views.WPF` のみです（軽さを重視しているため）。

## プロジェクト構成

```text
Trimx.slnx
├── src/
│   ├── Trimx.Core/   WPF 非依存のコア（net10.0）
│   │   ├── Documents/   ImageDocument / Layer（Image・Text・Shape）
│   │   ├── Rendering/   SkiaSharp による描画・書き出し
│   │   ├── Commands/    Undo / Redo 用の操作コマンド
│   │   ├── Editing/     ドキュメント・レイヤーの生成
│   │   └── Services/    画像入出力・プロジェクトファイル
│   └── Trimx/        WPF UI（net10.0-windows10.0.19041.0）
│       ├── UI/          MainWindow・キャンバス・タブ・各パネル・ダイアログ
│       └── Services/    クリップボード・設定・BitmapConverter
├── docs/             仕様書（plan.md。日本語）
├── installer/        リリースのビルドスクリプトと Inno Setup スクリプト
├── .ai/              AI コーディングツール向けの共通コンテキスト
└── AGENTS.md, CLAUDE.md, GEMINI.md, .github/, .cursor/   各 AI ツールの入口
```

設計上のポイント:

- Document / Layer は WPF に依存しない。レイヤー管理と描画処理は分離する。
- UI の操作は Document へのコマンドとして実装し、Undo / Redo の対象にする。
- 起動速度を最優先し、重い初期化は遅延させる。

## ビルドと実行

必要なもの: .NET 10 SDK、Windows 11。

```powershell
dotnet build Trimx.slnx
dotnet run --project src/Trimx/Trimx.csproj
```

> `TargetFramework` を `windows7.0` にすると SkiaSharp.Views.WPF が .NET Framework 版にフォールバックするため、`net10.0-windows10.0.19041.0` を維持してください。

## 配布物の作成

`installer/build.ps1` で、発行（win-x64・.NET ランタイム同梱・ReadyToRun）→ ポータブル zip → インストーラ（Inno Setup 6 以降がある場合）を作成します。出力は `installer/output/` です。コード署名はしていないため、SmartScreen の警告が出ます。

リリースは GitHub Actions（`.github/workflows/release.yml`）で作成します。`src/Trimx/Trimx.csproj` の `<Version>` を更新してから `v0.2.0` のようなタグを push すると、インストーラと zip を添えた下書きの Release が作られます。内容を確認して公開してください。

## 保存先

`%LOCALAPPDATA%\Trimx\` に次の 2 ファイルを作成します。

- `TrimxSetting.json`: 設定画面で選ぶ項目（テーマ、ラベル表示など）
- `TrimxState.json`: アプリが自動で覚える項目（前回の画像サイズ、マイカラー、ペン・テキスト・図形の前回の見た目など）

## 開発状況

| フェーズ | 内容 | 状況 |
|---|---|---|
| Phase 1 | 基本アプリ（画像表示・ズーム・クリップボード） | 実装済み |
| Phase 2 | Document・複数タブ・レイヤー | 実装済み |
| Phase 3 | 編集機能・Undo / Redo | 実装済み |
| Phase 4 | 保存・書き出し・コピー | 実装済み |
| Phase 5 | シングルインスタンス・最適化 | 未着手 |

マウス・キーボード操作の実機確認は一部のみです。ビルド、起動、Core の挙動、ボタン操作、確認ダイアログは確認済みです。

## ドキュメント

| 場所 | 内容 | 対象 |
|---|---|---|
| [docs/plan.md](docs/plan.md) | 仕様書・開発フェーズ（日本語。仕様の一次情報） | 人間 |
| [.ai/](.ai/README.md) | AI コーディングツール向けのコンテキスト（英語） | AI |

## AI ツールで開発する

Claude Code / Codex / Gemini CLI / GitHub Copilot / Cursor で開発できるよう、共通のコンテキストを `.ai/` にまとめています（英語）。各ツールが読む入口ファイルは `.ai/` を指す薄いポインタです。詳細は [.ai/README.md](.ai/README.md) を参照してください。

## ライセンス

[MIT License](LICENSE)。依存ライブラリのライセンスは [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) に記載しています。
