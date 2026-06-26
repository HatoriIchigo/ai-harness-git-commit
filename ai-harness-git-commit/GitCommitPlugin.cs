using System.Text.RegularExpressions;
using ai_harness_baselib;

namespace ai_harness_git_commit;

/// <summary>
/// git commit のメッセージ規約を PreToolUse で検査するプラグイン。
/// 設定（rule）に従っていない場合は reject（deny）し、reason に injection プロンプトを返す。
/// Claude はその reason を読んで規約準拠のコミットメッセージで再実行する。
///
///   rule.title.tags   … タイトル先頭に必須のタグ（&lt;tag&gt;: / &lt;tag&gt;(scope): 形式）
///   rule.title.length … タイトルの最大文字数
///   rule.deny_word    … タイトル/本文に含めてはいけない文字列
///   rule.injection    … 違反時に Claude へ返す指示プロンプト
/// </summary>
public sealed partial class GitCommitPlugin : PluginBase
{
    public override string PluginName => "ai-harness-git-commit";

    /// <summary>PreToolUse の全ツールで発火し、Action 内で Bash/git commit を自己フィルタ。</summary>
    public override IReadOnlyList<string> Events => new[] { "PreToolUse" };

    public override string ConfigName => "ai-harness-git-commit.yml";

    public override IEnumerable<LogEntry> Init()
    {
        yield return LogEntry.Info("初期化");
    }

    public override IEnumerable<LogEntry> Action(HookData data, PluginResult result)
    {
        if (data.Event != HookEvent.PreToolUse || data.ToolName != "Bash")
        {
            yield break;
        }

        var command = data.ToolInput?["command"]?.GetValue<string>();
        if (command is null || !IsGitCommit(command))
        {
            yield break;
        }

        var messages = ExtractMessages(command);
        if (messages.Count == 0)
        {
            // -m が無い（エディタ起動・-F 等）はメッセージを事前検証できないため通す。
            yield return LogEntry.Debug("コミットメッセージ(-m)を取得できないため検証スキップ");
            yield break;
        }

        var title = messages[0];
        var body = string.Join("\n", messages.Skip(1));
        var rules = ReadRules();
        var violations = Validate(title, body, rules);

        if (violations.Count > 0)
        {
            foreach (var v in violations)
            {
                yield return LogEntry.Warning($"コミット規約違反: {v}");
            }
            result.ExitCode = 2;
            result.Reason = BuildReason(rules.Injection, violations);
            yield break;
        }

        yield return LogEntry.Debug("コミットメッセージ規約 OK");
    }

    /// <summary>command が git commit か（git commit-tree 等は除外）。</summary>
    private static bool IsGitCommit(string command) => GitCommitRegex().IsMatch(command);

    [GeneratedRegex(@"\bgit\s+commit(\s|$)")]
    private static partial Regex GitCommitRegex();

    /// <summary>-m / --message の引数を順に抽出（クオート対応）。複数 -m は [title, body...]。</summary>
    private static IReadOnlyList<string> ExtractMessages(string command)
    {
        var list = new List<string>();
        foreach (Match m in MessageRegex().Matches(command))
        {
            var value = m.Groups[1].Success ? m.Groups[1].Value
                : m.Groups[2].Success ? m.Groups[2].Value
                : m.Groups[3].Value;
            list.Add(value);
        }
        return list;
    }

    [GeneratedRegex("""(?:-m|--message)(?:=|\s+)(?:"([^"]*)"|'([^']*)'|(\S+))""")]
    private static partial Regex MessageRegex();

    private List<string> Validate(string title, string body, Rules rules)
    {
        var violations = new List<string>();

        // タグ: タイトル先頭が許可タグのいずれか（"<tag>:" または "<tag>("）。
        if (rules.Tags.Count > 0)
        {
            var ok = rules.Tags.Any(t =>
                title.StartsWith(t + ":", StringComparison.Ordinal) ||
                title.StartsWith(t + "(", StringComparison.Ordinal));
            if (!ok)
            {
                violations.Add($"タイトルが許可タグで始まっていない（許可: {string.Join(", ", rules.Tags)}）");
            }
        }

        // 文字数
        if (rules.TitleLength is { } max && title.Length > max)
        {
            violations.Add($"タイトルが {max} 文字を超過（現在 {title.Length} 文字）");
        }

        // 禁止語（タイトル/本文両方）
        foreach (var word in rules.DenyWords)
        {
            if (word.Length == 0)
            {
                continue;
            }
            if (title.Contains(word, StringComparison.Ordinal) || body.Contains(word, StringComparison.Ordinal))
            {
                violations.Add($"禁止語を含む: '{word}'");
            }
        }

        return violations;
    }

    private static string BuildReason(string? injection, IReadOnlyList<string> violations)
    {
        var head = string.IsNullOrWhiteSpace(injection)
            ? "コミットメッセージが規約に従っていません。"
            : injection;
        return head + "\n\n違反内容:\n- " + string.Join("\n- ", violations);
    }

    // ---- 設定読み取り（YamlDotNet 既定: ネストマップ=IDictionary<object,object>, 配列=IList<object>） ----

    private readonly record struct Rules(
        IReadOnlyList<string> Tags, int? TitleLength, IReadOnlyList<string> DenyWords, string? Injection);

    private Rules ReadRules()
    {
        var rule = AsMap(Config.TryGetValue("rule", out var r) ? r : null);
        var title = AsMap(Get(rule, "title"));

        var tags = AsStringList(Get(title, "tags"));
        var length = ParseInt(Get(title, "length"));
        var denyWords = AsStringList(Get(rule, "deny_word"));
        var injection = Get(rule, "injection")?.ToString();

        return new Rules(tags, length, denyWords, injection);
    }

    private static IDictionary<object, object>? AsMap(object? o) => o as IDictionary<object, object>;

    private static object? Get(IDictionary<object, object>? map, string key) =>
        map is not null && map.TryGetValue(key, out var v) ? v : null;

    private static IReadOnlyList<string> AsStringList(object? o)
    {
        if (o is not System.Collections.IEnumerable seq || o is string)
        {
            return Array.Empty<string>();
        }
        var list = new List<string>();
        foreach (var item in seq)
        {
            var s = item?.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                list.Add(s.Trim());
            }
        }
        return list;
    }

    private static int? ParseInt(object? o) =>
        int.TryParse(o?.ToString(), out var n) ? n : null;
}
