---
paths:
  - .claude/harness/config/ai-harness-git-commit.yml
---

## 概要

ai-harness-git-commit は `PreToolUse`（Bash）で `git commit` を捕捉し、設定 `rule` に従わない
コミットメッセージを reject（deny）する。違反時は `injection` プロンプトを reason として返し、
Claude に規約準拠のメッセージで作り直させる。

- `rule.title.tags` … タイトル先頭に必須のタグ（`<tag>:` / `<tag>(scope):` 形式）
- `rule.title.length` … タイトルの最大文字数
- `rule.deny_word` … タイトル／本文に含めてはいけない文字列
- `rule.injection` … 違反時に Claude へ返す指示プロンプト

## 設定ファイル

`.claude/harness/config/ai-harness-git-commit.yml`

```yaml
rule:
  title:
    # タイトルは "<tag>:" または "<tag>(scope):" で始まる必要がある
    tags:
      - feat
      - chor
    # タイトルの最大文字数
    length: 50
  # タイトル／本文に含めてはいけない文字列
  deny_word:
    - pass
    - failed
  # 違反時に Claude へ返す指示プロンプト
  injection: "許可タグ（feat / chor）で始め、50 文字以内。pass / failed は使用しないこと。"
```
