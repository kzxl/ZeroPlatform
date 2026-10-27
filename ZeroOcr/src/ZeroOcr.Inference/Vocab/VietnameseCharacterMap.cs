using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroOcr.Inference.Vocab;

/// <summary>
/// Provides comprehensive Unicode vocabulary mapping for Vietnamese and industrial OCR.
/// Encodes the complete set of 134 Vietnamese accented glyphs, ASCII alphanumeric characters,
/// and industrial measurement symbols.
/// </summary>
public sealed class VietnameseCharacterMap
{
    private readonly char[] _indexToChar;
    private readonly Dictionary<char, int> _charToIndex;

    public int Count => _indexToChar.Length;

    public VietnameseCharacterMap(IReadOnlyList<char> characters)
    {
        if (characters == null) throw new ArgumentNullException(nameof(characters));

        _indexToChar = new char[characters.Count];
        _charToIndex = new Dictionary<char, int>(characters.Count);

        for (int i = 0; i < characters.Count; i++)
        {
            char c = characters[i];
            _indexToChar[i] = c;
            _charToIndex[c] = i;
        }
    }

    /// <summary>
    /// Gets the character corresponding to the token index (0 is blank token for CTC).
    /// </summary>
    public char GetChar(int index)
    {
        if (index >= 0 && index < _indexToChar.Length)
            return _indexToChar[index];
        return ' ';
    }

    /// <summary>
    /// Gets the token index for a given character.
    /// </summary>
    public int GetIndex(char c)
    {
        return _charToIndex.TryGetValue(c, out int idx) ? idx : -1;
    }

    /// <summary>
    /// Loads a dictionary file containing one character per line (or continuous string).
    /// </summary>
    public static VietnameseCharacterMap FromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Vocabulary file not found.", filePath);

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        var chars = new List<char>(lines.Length + 2);

        // Index 0 in CTC sequence modeling is reserved for the blank token
        chars.Add(' ');

        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line))
            {
                chars.Add(line[0]);
            }
        }

        return new VietnameseCharacterMap(chars);
    }

    private static VietnameseCharacterMap? _defaultMap;
    private static readonly object _syncLock = new();

    /// <summary>
    /// Gets the singleton default Vietnamese and industrial vocabulary map.
    /// </summary>
    public static VietnameseCharacterMap Default
    {
        get
        {
            if (_defaultMap == null)
            {
                lock (_syncLock)
                {
                    _defaultMap ??= BuildDefaultMap();
                }
            }
            return _defaultMap;
        }
    }

    private static VietnameseCharacterMap BuildDefaultMap()
    {
        const string baseChars =
            // CTC Blank symbol placeholder at index 0 (handled separately in CTC decoder, but index 0 in character list)
            " " +
            "0123456789" +
            "abcdefghijklmnopqrstuvwxyz" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            // Vietnamese lowercase vowels & accents
            "àáảãạăằắẳẵặâầấẩẫậ" +
            "èéẻẽẹêềếểễệ" +
            "ìíỉĩị" +
            "òóỏõọôồốổỗộơờớởỡợ" +
            "ùúủũụưừứửữự" +
            "ỳýỷỹỵ" +
            "đ" +
            // Vietnamese uppercase vowels & accents
            "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬ" +
            "ÈÉẺẼẸÊỀẾỂỄỆ" +
            "ÌÍỈĨỊ" +
            "ÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢ" +
            "ÙÚỦŨỤƯỪỨỬỮỰ" +
            "ỲÝỶỸỴ" +
            "Đ" +
            // Industrial symbols, currency, and punctuation
            "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~" +
            "°±×÷₫€$¥£%‰§©®™";

        var list = new List<char>(baseChars.Length);
        foreach (char c in baseChars)
        {
            if (!list.Contains(c))
                list.Add(c);
        }

        return new VietnameseCharacterMap(list);
    }
}
