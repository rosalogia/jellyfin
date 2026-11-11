using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// Orders the dictionary entries of a word so the most likely reading comes first.
/// </summary>
internal static class ReadingSelector
{
    // Everyday readings for common characters that have several readings in the dictionary. Multi-character
    // words are mostly unambiguous already, so this only matters when a character stands alone.
    private static readonly FrozenDictionary<string, string> _commonReadings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["的"] = "de5", ["了"] = "le5", ["着"] = "zhe5", ["著"] = "zhe5", ["得"] = "de5", ["地"] = "di4",
        ["还"] = "hai2", ["還"] = "hai2", ["都"] = "dou1", ["行"] = "xing2", ["长"] = "chang2", ["長"] = "chang2",
        ["重"] = "zhong4", ["为"] = "wei4", ["為"] = "wei4", ["和"] = "he2", ["只"] = "zhi3", ["种"] = "zhong3",
        ["種"] = "zhong3", ["好"] = "hao3", ["要"] = "yao4", ["会"] = "hui4", ["會"] = "hui4", ["没"] = "mei2",
        ["沒"] = "mei2", ["发"] = "fa1", ["發"] = "fa1", ["少"] = "shao3", ["教"] = "jiao1", ["看"] = "kan4",
        ["干"] = "gan4", ["幹"] = "gan4", ["当"] = "dang1", ["當"] = "dang1", ["把"] = "ba3", ["给"] = "gei3",
        ["給"] = "gei3", ["一"] = "yi1", ["不"] = "bu4", ["大"] = "da4", ["么"] = "me5", ["麼"] = "me5",
        ["吗"] = "ma5", ["嗎"] = "ma5", ["呢"] = "ne5", ["吧"] = "ba5", ["啊"] = "a5", ["呀"] = "ya5", ["哪"] = "na3",
        ["那"] = "na4", ["这"] = "zhe4", ["這"] = "zhe4", ["个"] = "ge4", ["個"] = "ge4", ["们"] = "men5",
        ["們"] = "men5", ["处"] = "chu4", ["處"] = "chu4", ["差"] = "cha4", ["数"] = "shu4", ["數"] = "shu4",
        ["便"] = "bian4", ["相"] = "xiang1", ["应"] = "ying1", ["應"] = "ying1", ["觉"] = "jue2", ["覺"] = "jue2",
        ["乐"] = "le4", ["樂"] = "le4", ["传"] = "chuan2", ["傳"] = "chuan2", ["调"] = "diao4", ["調"] = "diao4",
        ["中"] = "zhong1", ["难"] = "nan2", ["難"] = "nan2", ["假"] = "jia3", ["间"] = "jian1", ["間"] = "jian1",
        ["将"] = "jiang1", ["將"] = "jiang1", ["正"] = "zheng4", ["上"] = "shang4", ["下"] = "xia4",
        ["空"] = "kong1", ["转"] = "zhuan3", ["轉"] = "zhuan3", ["称"] = "cheng1", ["稱"] = "cheng1",
        ["朝"] = "chao2", ["冲"] = "chong1", ["衝"] = "chong1", ["系"] = "xi4", ["落"] = "luo4", ["血"] = "xue4",
        ["恶"] = "e4", ["惡"] = "e4", ["藏"] = "cang2", ["弹"] = "tan2", ["彈"] = "tan2", ["背"] = "bei4",
        ["量"] = "liang4", ["参"] = "can1", ["參"] = "can1", ["我"] = "wo3", ["你"] = "ni3",
        ["他"] = "ta1", ["她"] = "ta1", ["它"] = "ta1", ["是"] = "shi4", ["在"] = "zai4", ["有"] = "you3",
        ["说"] = "shuo1", ["說"] = "shuo1", ["什么"] = "shen2 me5", ["什麼"] = "shen2 me5"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Orders entries best-first.
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <param name="entries">The dictionary entries for the word.</param>
    /// <param name="previous">The preceding token, if any.</param>
    /// <param name="next">The following token, if any.</param>
    /// <returns>The entries, most likely reading first, with exact duplicates removed.</returns>
    public static IReadOnlyList<CedictEntry> Order(string word, IReadOnlyList<CedictEntry> entries, SegmentToken? previous, SegmentToken? next)
    {
        if (entries.Count <= 1)
        {
            return entries;
        }

        var preferred = PreferredReading(word, previous, next);
        var ordered = entries
            .DistinctBy(e => (e.NumberedPinyin, e.RawDefinitions))
            .OrderByDescending(e => Score(e, word, preferred))
            .ToList();

        // Drop entries that only point elsewhere ("variant of 吃[chi1]") when there is a real one.
        if (ordered.Any(e => e.MeaningfulDefinitionCount > 0))
        {
            ordered.RemoveAll(e => e.MeaningfulDefinitionCount == 0 && !string.Equals(e.NumberedPinyin, preferred, StringComparison.OrdinalIgnoreCase));
        }

        return ordered;
    }

    private static string? PreferredReading(string word, SegmentToken? previous, SegmentToken? next)
    {
        // 地 as an adverb marker (慢慢地走, 认真地看): follows a longer word and precedes another.
        if (word is "地" && previous is { IsHan: true, Text.Length: >= 2 } && next is { IsHan: true })
        {
            return "de5";
        }

        return _commonReadings.GetValueOrDefault(word);
    }

    private static int Score(CedictEntry entry, string word, string? preferredReading)
    {
        var score = 0;
        if (preferredReading is not null && string.Equals(entry.NumberedPinyin, preferredReading, StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }

        // Prefer an entry whose form matches the script the text is written in.
        if (string.Equals(entry.Simplified, word, StringComparison.Ordinal) && string.Equals(entry.Traditional, word, StringComparison.Ordinal))
        {
            score += 2;
        }

        var meaningful = entry.MeaningfulDefinitionCount;
        score += meaningful == 0 ? -100 : Math.Min(meaningful, 10) * 5;
        if (entry.IsProperNoun)
        {
            score -= 40;
        }

        return score;
    }
}
