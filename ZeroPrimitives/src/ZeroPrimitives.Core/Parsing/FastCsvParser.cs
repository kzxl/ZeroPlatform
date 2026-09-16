using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Parsing
{
    /// <summary>
    /// Zero-allocation, high-throughput CSV and delimited text parser.
    /// Operates entirely over ReadOnlySpan without allocating row or cell strings.
    /// Handles standard RFC 4180 quotes ("..."), escaped quotes (""), and varied delimiters.
    /// </summary>
    public static class FastCsvParser
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RowEnumerable EnumerateRows(ReadOnlySpan<char> text)
            => new RowEnumerable(text);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CellEnumerable EnumerateCells(ReadOnlySpan<char> row, char delimiter = ',')
            => new CellEnumerable(row, delimiter);

        /// <summary>
        /// Unescapes a quoted CSV cell (replaces double quotes "" with single quote ").
        /// </summary>
        public static ReadOnlySpan<char> Unquote(ReadOnlySpan<char> cell, Span<char> buffer, out int written)
        {
            written = 0;
            if (cell.Length >= 2 && cell[0] == '"' && cell[cell.Length - 1] == '"')
            {
                cell = cell.Slice(1, cell.Length - 2);
                int outIdx = 0;
                for (int i = 0; i < cell.Length; i++)
                {
                    char c = cell[i];
                    if (c == '"' && i + 1 < cell.Length && cell[i + 1] == '"')
                    {
                        buffer[outIdx++] = '"';
                        i++; // Skip second quote
                    }
                    else
                    {
                        buffer[outIdx++] = c;
                    }
                }
                written = outIdx;
                return buffer.Slice(0, outIdx);
            }

            written = cell.Length;
            return cell;
        }

        public readonly ref struct RowEnumerable
        {
            private readonly ReadOnlySpan<char> _text;
            public RowEnumerable(ReadOnlySpan<char> text) => _text = text;
            public RowEnumerator GetEnumerator() => new RowEnumerator(_text);
        }

        public ref struct RowEnumerator
        {
            private ReadOnlySpan<char> _remaining;
            private ReadOnlySpan<char> _current;

            public RowEnumerator(ReadOnlySpan<char> text)
            {
                _remaining = text;
                _current = default;
            }

            public ReadOnlySpan<char> Current => _current;

            public bool MoveNext()
            {
                if (_remaining.IsEmpty) return false;

                int firstSpecial = _remaining.IndexOfAny('\r', '\n', '"');
                if (firstSpecial < 0)
                {
                    _current = _remaining;
                    _remaining = ReadOnlySpan<char>.Empty;
                    return true;
                }

                char hit = _remaining[firstSpecial];
                if (hit == '\r' || hit == '\n')
                {
                    _current = _remaining.Slice(0, firstSpecial);
                    if (hit == '\r' && firstSpecial + 1 < _remaining.Length && _remaining[firstSpecial + 1] == '\n')
                    {
                        firstSpecial++; // Skip \n in CRLF
                    }
                    _remaining = _remaining.Slice(firstSpecial + 1);
                    return true;
                }

                // Quoted row path
                bool inQuotes = true;
                int idx = firstSpecial + 1;

                while (idx < _remaining.Length)
                {
                    char c = _remaining[idx];
                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                    }
                    else if (!inQuotes && (c == '\r' || c == '\n'))
                    {
                        _current = _remaining.Slice(0, idx);
                        if (c == '\r' && idx + 1 < _remaining.Length && _remaining[idx + 1] == '\n')
                        {
                            idx++; // Skip \n in CRLF
                        }
                        _remaining = _remaining.Slice(idx + 1);
                        return true;
                    }
                    idx++;
                }

                _current = _remaining;
                _remaining = ReadOnlySpan<char>.Empty;
                return true;
            }
        }

        public readonly ref struct CellEnumerable
        {
            private readonly ReadOnlySpan<char> _row;
            private readonly char _delimiter;

            public CellEnumerable(ReadOnlySpan<char> row, char delimiter)
            {
                _row = row;
                _delimiter = delimiter;
            }

            public CellEnumerator GetEnumerator() => new CellEnumerator(_row, _delimiter);
        }

        public ref struct CellEnumerator
        {
            private ReadOnlySpan<char> _remaining;
            private readonly char _delimiter;
            private ReadOnlySpan<char> _current;
            private bool _isCompleted;

            public CellEnumerator(ReadOnlySpan<char> row, char delimiter)
            {
                _remaining = row;
                _delimiter = delimiter;
                _current = default;
                _isCompleted = false;
            }

            public ReadOnlySpan<char> Current => _current;

            public bool MoveNext()
            {
                if (_isCompleted) return false;

                if (_remaining.IsEmpty)
                {
                    _current = ReadOnlySpan<char>.Empty;
                    _isCompleted = true;
                    return true;
                }

                int firstSpecial = _remaining.IndexOfAny('"', _delimiter);
                if (firstSpecial < 0)
                {
                    _current = _remaining;
                    _remaining = ReadOnlySpan<char>.Empty;
                    _isCompleted = true;
                    return true;
                }

                if (_remaining[firstSpecial] == _delimiter)
                {
                    // Fast SIMD path: found delimiter without quotes before it
                    _current = _remaining.Slice(0, firstSpecial);
                    _remaining = _remaining.Slice(firstSpecial + 1);
                    return true;
                }

                // Quoted cell path: vectorize finding the matching closing quote
                int searchStart = firstSpecial + 1;
                while (searchStart < _remaining.Length)
                {
                    int quoteRelative = _remaining.Slice(searchStart).IndexOf('"');
                    if (quoteRelative < 0)
                    {
                        _current = _remaining;
                        _remaining = ReadOnlySpan<char>.Empty;
                        _isCompleted = true;
                        return true;
                    }

                    int quoteIdx = searchStart + quoteRelative;
                    // Check if it's an escaped double quote ("")
                    if (quoteIdx + 1 < _remaining.Length && _remaining[quoteIdx + 1] == '"')
                    {
                        searchStart = quoteIdx + 2;
                        continue;
                    }

                    // Found closing quote! Look for the delimiter after the closing quote
                    int delimRelative = _remaining.Slice(quoteIdx + 1).IndexOf(_delimiter);
                    if (delimRelative < 0)
                    {
                        _current = _remaining;
                        _remaining = ReadOnlySpan<char>.Empty;
                        _isCompleted = true;
                        return true;
                    }

                    int delimIdx = quoteIdx + 1 + delimRelative;
                    _current = _remaining.Slice(0, delimIdx);
                    _remaining = _remaining.Slice(delimIdx + 1);
                    return true;
                }

                _current = _remaining;
                _remaining = ReadOnlySpan<char>.Empty;
                _isCompleted = true;
                return true;
            }
        }
    }
}
