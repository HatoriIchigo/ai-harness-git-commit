# ai-harness-git-commit

> git commit のメッセージ規約を強制する ai-harness プラグイン。

`PreToolUse` で `git commit` を捕捉し、`config/ai-harness-git-commit.yml` の `rule` に従わないメッセージを **reject（deny, exit 2）** する。reject 時は `injection` のプロンプトを reason として返し、Claude が規約準拠のメッセージで commit を作り直す。

## 設定（config/ai-harness-git-commit.yml）

```yaml
rule:
  title:
    tags:        # タイトルは "<tag>:" / "<tag>(scope):" で始まる必要
      - feat
      - chor
    length: 50   # タイトルの最大文字数
  contents:      # プレースホルダ（現状未使用）
  deny_word:     # タイトル/本文に含めてはいけない文字列
    - pass
    - failed
  injection: "以下要件に従って..."   # 違反時に Claude へ返す指示
```

## 検査ルール

| 項目 | 対象 | 内容 |
|---|---|---|
| `title.tags` | タイトル（最初の `-m`） | `<tag>:` または `<tag>(scope):` で始まること。空なら無検査 |
| `title.length` | タイトル | 最大文字数。超過で違反 |
| `deny_word` | タイトル＋本文 | 指定文字列を含むと違反 |
| `injection` | — | 違反時に reason 先頭へ付与する指示プロンプト |

- 1 つでも違反すれば deny（exit 2）。reason は `injection` ＋ 具体的な違反内容のリスト。
- `git commit` に `-m`/`--message` が無い場合（エディタ起動・`-F` 等）はメッセージを事前取得できないため**検証をスキップして通す**。
- `git commit-tree` 等は対象外（`git commit` のみ）。

## ビルドと配置

```sh
dotnet build ai-harness-git-commit/ai-harness-git-commit/ai-harness-git-commit.csproj -c Release

cp ai-harness-git-commit/ai-harness-git-commit/bin/Release/net10.0/ai-harness-git-commit.dll  <配置先>/lib/
cp ai-harness-git-commit/config/ai-harness-git-commit.yml                                      <配置先>/config/

# main.yml の tools で有効化してから
<配置先>/ai-harness-main --restart
```

`lib/` には `ai-harness-git-commit.dll` のみ置く（baselib.dll は host が共有ロード）。`main.yml` の `tools` に `ai-harness-git-commit: true` を追加して有効化する。詳細は `ai-harness-main/docs/plugin-development.md` を参照。

## 構成

```
ai-harness-git-commit/
├── README.md
├── config/
│   └── ai-harness-git-commit.yml   ルール定義（配置元）
└── ai-harness-git-commit/
    ├── ai-harness-git-commit.csproj
    └── GitCommitPlugin.cs          PreToolUse で git commit メッセージを検査
```
