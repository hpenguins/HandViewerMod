using System;
using System.Collections.Generic;
using Xunit;

namespace HandViewerMod.Tests
{
    public class StatusTextLayoutTests
    {
        static string RemoveSpaces(string value)
        {
            var sb = new System.Text.StringBuilder(value.Length);
            foreach (char c in value) if (c != ' ' && c != '\n' && c != '\r' && c != '\t') sb.Append(c);
            return sb.ToString();
        }

        static void AssertFits(StatusTextLayout layout, float width, float height)
        {            foreach (var line in layout.Lines)
                Assert.True(StatusTextLayout.Measure(line, layout.FontSize) <= width + 0.5f,
                    $"line '{line}' is wider than {width}");
            float needed = layout.Lines.Count * StatusTextLayout.LineHeight(layout.FontSize);
            Assert.True(needed <= height + 0.5f, $"layout height {needed} exceeds {height}");
        }

        [Fact]
        public void ShortMessageKeepsLargestFontSize()
        {
            var layout = StatusTextLayout.Fit("已采样", 280f, 40f, 18, 10);
            Assert.Equal(18, layout.FontSize);
            Assert.Single(layout.Lines);
            Assert.Equal("已采样", layout.Text);
            Assert.False(layout.Truncated);
        }

        [Fact]
        public void LongServerMessageWrapsInsteadOfOverflowingTheBox()
        {
            const string message = "查询失败 HTTP 503：查询忙，请稍后重试。（稍后重试）";
            var layout = StatusTextLayout.Fit(message, 280f, 60f, 18, 10);
            Assert.True(layout.Lines.Count > 1);
            Assert.True(layout.Lines.Count < message.Length);
            AssertFits(layout, 280f, 60f);
            // 折行后所有文字都还在, 一个字都没丢(断行处的空格会被吃掉)
            Assert.Equal(RemoveSpaces(message), RemoveSpaces(layout.Text));
        }

        [Fact]
        public void ShrinksFontSizeUntilTheMessageFitsTheHeight()
        {
            var layout = StatusTextLayout.Fit("查询失败 HTTP 503：查询忙，请稍后重试。", 280f, 30f, 18, 10);
            Assert.True(layout.FontSize < 18);
            AssertFits(layout, 280f, 30f);
        }

        [Fact]
        public void TruncatesWhenEvenTheSmallestFontDoesNotFit()
        {
            string message = new string('长', 400);
            var layout = StatusTextLayout.Fit(message, 200f, 30f, 18, 10);
            Assert.True(layout.Truncated);
            AssertFits(layout, 200f, 30f);
            Assert.EndsWith("…", layout.Lines[layout.Lines.Count - 1]);
        }

        [Fact]
        public void KeepsExplicitLineBreaks()
        {
            var layout = StatusTextLayout.Fit("当前模式 4 暂不支持\n观战服务仅支持 4 / 12", 280f, 80f, 18, 10);
            Assert.Equal(2, layout.Lines.Count);
            Assert.Equal("当前模式 4 暂不支持", layout.Lines[0]);
            Assert.Equal("观战服务仅支持 4 / 12", layout.Lines[1]);
        }

        [Fact]
        public void BreaksLatinWordsOnSpaces()
        {
            const string message = "query failed HTTP 503 server busy";
            var layout = StatusTextLayout.Fit(message, 120f, 80f, 12, 8);
            Assert.True(layout.Lines.Count > 1);
            foreach (var line in layout.Lines) Assert.Equal(line.Trim(), line);
            Assert.Equal(RemoveSpaces(message), RemoveSpaces(layout.Text));
            AssertFits(layout, 120f, 80f);
        }

        [Fact]
        public void NormalizeRemovesControlCharactersAndUnifiesNewlines()
        {
            Assert.Equal("a b\nc", StatusTextLayout.Normalize("a\tb\r\nc"));
            Assert.Equal("a b", StatusTextLayout.Normalize("a\u0001b"));
        }

        [Fact]
        public void HandlesEmptyAndInvalidInput()
        {
            var empty = StatusTextLayout.Fit("", 280f, 40f, 18, 10);
            Assert.Empty(empty.Lines);
            var invalid = StatusTextLayout.Fit("文本", 0f, 0f, 18, 10);
            Assert.Empty(invalid.Lines);
        }
    }
}
