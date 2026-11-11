using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Server.Implementations.StudySubtitles;
using MediaBrowser.Model.MediaInfo;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.StudySubtitles;

public class StudySubtitlesTests
{
    private const string SmallDictionary = """
        # comment
        研究 研究 [yan2 jiu1] /research/to study/
        研究生 研究生 [yan2 jiu1 sheng1] /graduate student/
        生命 生命 [sheng1 ming4] /life/
        命 命 [ming4] /life/fate/
        我們 我们 [wo3 men5] /we; us/
        了 了 [le5] /(completed action marker)/
        了 了 [liao3] /to finish/
        了解 了解 [liao3 jie3] /to understand/
        """;

    private static ChineseDictionary Small => ChineseDictionary.Parse(new StringReader(SmallDictionary));

    [Theory]
    [InlineData("zhong1 wen2", "zhōng wén")]
    [InlineData("lu:4", "lǜ")]
    [InlineData("nv3", "nǚ")]
    [InlineData("gou3", "gǒu")]
    [InlineData("gui4", "guì")]
    [InlineData("liu2", "liú")]
    [InlineData("Xiao3 Hei1", "Xiǎo Hēi")]
    [InlineData("le5", "le")]
    [InlineData("r5", "r")]
    [InlineData("ng2", "ng")]
    public void ToToneMarks_ConvertsNumberedPinyin(string numbered, string expected)
        => Assert.Equal(expected, Pinyin.ToToneMarks(numbered));

    [Fact]
    public void Segment_PrefersEvenSplit()
    {
        var tokens = ChineseSegmenter.Segment("我们研究生命。", Small);
        Assert.Equal(new[] { "我们", "研究", "生命", "。" }, tokens.Select(t => t.Text));
        Assert.Equal(new[] { true, true, true, false }, tokens.Select(t => t.IsHan));
    }

    [Fact]
    public void Segment_KeepsUnknownCharactersAndLatinText()
    {
        var tokens = ChineseSegmenter.Segment("OK 我们好", Small);
        Assert.Equal(new[] { "OK ", "我们", "好" }, tokens.Select(t => t.Text));
    }

    [Fact]
    public void BuildWords_UsesCommonReadingForStandaloneCharacter()
    {
        var words = StudySubtitleBuilder.BuildWords("了解了", Small);
        Assert.Equal(new[] { "liǎo jiě", "le" }, words.Select(w => w.Pinyin));
        Assert.Equal(2, words[1].Entries.Count);
    }

    [Fact]
    public void FromTracks_MatchesEnglishByOverlapAndSplitsBilingualLines()
    {
        var chinese = new[]
        {
            Event(0, 20, "{\\an8}我们研究生命"),
            Event(30, 40, "了解\\NUnderstood"),
            Event(30, 40, "了解\\NUnderstood"), // duplicate layer
            Event(50, 60, "<i>♪</i>")
        };
        var english = new[] { Event(1, 21, "We study life."), Event(100, 110, "Unrelated") };

        var lines = StudySubtitleBuilder.FromTracks(chinese, english, Small);

        Assert.Equal(2, lines.Count);
        Assert.Equal("我们研究生命", lines[0].Text);
        Assert.Equal("We study life.", lines[0].Translation);
        Assert.Equal("了解", lines[1].Text);
        Assert.Equal("Understood", lines[1].Translation);
    }

    [Fact]
    public void FromSidecar_KeepsEnrichedReadingsAndFillsGaps()
    {
        const string Json = """
            {
              "resolution": { "width": 1920, "height": 1080 },
              "crop_y": 906,
              "subtitles": [
                {
                  "start": "00:00:37.500", "end": "00:00:39.000", "text": "我们了解了。",
                  "translation": "We understood.", "grammar_notes": "",
                  "words": [
                    { "word": "我们", "pinyin": "", "english": "we" },
                    { "word": "了解", "pinyin": "liǎojiě", "english": "understood" },
                    { "word": "了", "pinyin": "le", "english": "(completed)" },
                    { "word": "。", "pinyin": "", "english": "." }
                  ]
                }
              ]
            }
            """;
        var sidecar = JsonSerializer.Deserialize<LensSidecar>(Json)!;

        var line = Assert.Single(StudySubtitleBuilder.FromSidecar(sidecar, Small));

        Assert.Equal(375_000_000, line.StartPositionTicks);
        Assert.Equal("We understood.", line.Translation);
        Assert.Null(line.Notes);
        Assert.Equal(new[] { "wǒ men", "liǎojiě", "le", null }, line.Words.Select(w => w.Pinyin));
        Assert.Equal("understood", line.Words[1].Gloss);
        Assert.False(line.Words[3].IsWord);
        Assert.Equal("le", line.Words[2].Entries[0].Pinyin);
    }

    [Fact]
    public void EmbeddedDictionary_LoadsAndSegmentsEverydaySpeech()
    {
        var words = StudySubtitleBuilder.BuildWords("你在干什么呢？我们走吧。", ChineseDictionary.Embedded);
        Assert.Equal(
            new[] { "你", "在", "干什么", "呢", "？", "我们", "走", "吧", "。" },
            words.Select(w => w.Text));
        Assert.Equal("ne", words[3].Pinyin);
        Assert.Equal("ba", words[7].Pinyin);
    }

    [Fact]
    public void EmbeddedDictionary_DoesNotMergeSlangEntries()
    {
        // 我去 is in CC-CEDICT only as slang ("oh my god!"); here it is 我 + 去.
        var words = StudySubtitleBuilder.BuildWords("我去上学了！", ChineseDictionary.Embedded);
        Assert.Equal(new[] { "我", "去", "上学", "了", "！" }, words.Select(w => w.Text));
    }

    [Fact]
    public void EmbeddedDictionary_HidesVariantOnlyEntries()
    {
        var word = Assert.Single(StudySubtitleBuilder.BuildWords("吃", ChineseDictionary.Embedded));
        Assert.All(word.Entries, e => Assert.DoesNotContain(e.Definitions, d => d.StartsWith("variant of", System.StringComparison.Ordinal)));
    }

    private static SubtitleTrackEvent Event(long start, long end, string text)
        => new(start.ToString(System.Globalization.CultureInfo.InvariantCulture), text) { StartPositionTicks = start, EndPositionTicks = end };
}
