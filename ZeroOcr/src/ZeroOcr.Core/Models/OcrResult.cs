using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroOcr.Core.Models;

/// <summary>
/// Represents the comprehensive result of an OCR recognition pipeline.
/// </summary>
public sealed class OcrResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string Text { get; }
    public IReadOnlyList<OcrBlock> Blocks { get; }
    public IReadOnlyList<OcrLine> Lines { get; }
    public IReadOnlyList<OcrWord> Words { get; }
    public TimeSpan Elapsed { get; }
    public string? Language { get; }
    public float MeanConfidence { get; }

    private OcrResult(
        bool success,
        string? errorMessage,
        string text,
        IReadOnlyList<OcrBlock> blocks,
        IReadOnlyList<OcrLine> lines,
        IReadOnlyList<OcrWord> words,
        TimeSpan elapsed,
        string? language,
        float meanConfidence)
    {
        Success = success;
        ErrorMessage = errorMessage;
        Text = text;
        Blocks = blocks;
        Lines = lines;
        Words = words;
        Elapsed = elapsed;
        Language = language;
        MeanConfidence = meanConfidence;
    }

    public static OcrResult Empty => new(
        success: true,
        errorMessage: null,
        text: string.Empty,
        blocks: Array.Empty<OcrBlock>(),
        lines: Array.Empty<OcrLine>(),
        words: Array.Empty<OcrWord>(),
        elapsed: TimeSpan.Zero,
        language: null,
        meanConfidence: 1.0f);

    public static OcrResult Failed(string errorMessage, TimeSpan elapsed = default) => new(
        success: false,
        errorMessage: errorMessage,
        text: string.Empty,
        blocks: Array.Empty<OcrBlock>(),
        lines: Array.Empty<OcrLine>(),
        words: Array.Empty<OcrWord>(),
        elapsed: elapsed,
        language: null,
        meanConfidence: 0.0f);

    public static OcrResult Create(
        IReadOnlyList<OcrLine> lines,
        TimeSpan elapsed,
        string? language = null)
    {
        var allWords = new List<OcrWord>();
        float confSum = 0f;
        int wordCount = 0;

        foreach (var line in lines)
        {
            foreach (var word in line.Words)
            {
                allWords.Add(word);
                confSum += word.Confidence;
                wordCount++;
            }
        }

        var block = new OcrBlock(lines);
        var blocks = new[] { block };
        string text = string.Join(Environment.NewLine, lines.Select(l => l.Text));
        float meanConf = wordCount > 0 ? (confSum / wordCount) : 1.0f;

        return new OcrResult(
            success: true,
            errorMessage: null,
            text: text,
            blocks: blocks,
            lines: lines,
            words: allWords,
            elapsed: elapsed,
            language: language,
            meanConfidence: meanConf);
    }

    public static OcrResult Create(
        IReadOnlyList<OcrBlock> blocks,
        TimeSpan elapsed,
        string? language = null)
    {
        var allLines = new List<OcrLine>();
        var allWords = new List<OcrWord>();
        float confSum = 0f;
        int wordCount = 0;

        foreach (var block in blocks)
        {
            foreach (var line in block.Lines)
            {
                allLines.Add(line);
                foreach (var word in line.Words)
                {
                    allWords.Add(word);
                    confSum += word.Confidence;
                    wordCount++;
                }
            }
        }

        string text = string.Join(Environment.NewLine + Environment.NewLine, blocks.Select(b => b.Text));
        float meanConf = wordCount > 0 ? (confSum / wordCount) : 1.0f;

        return new OcrResult(
            success: true,
            errorMessage: null,
            text: text,
            blocks: blocks,
            lines: allLines,
            words: allWords,
            elapsed: elapsed,
            language: language,
            meanConfidence: meanConf);
    }

    /// <summary>
    /// Searches for words matching the given predicate.
    /// </summary>
    public IEnumerable<OcrWord> FindWords(Func<OcrWord, bool> predicate) =>
        Words.Where(predicate);

    /// <summary>
    /// Searches for lines matching the given predicate.
    /// </summary>
    public IEnumerable<OcrLine> FindLines(Func<OcrLine, bool> predicate) =>
        Lines.Where(predicate);

    public override string ToString() =>
        Success
            ? $"OCR Result: {Lines.Count} lines, {Words.Count} words, Conf: {MeanConfidence:P0}, Time: {Elapsed.TotalMilliseconds:F1}ms"
            : $"OCR Failed: {ErrorMessage}";
}
