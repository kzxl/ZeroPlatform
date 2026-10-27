using System;

namespace ZeroUI.Core.Input.Masking
{
    /// <summary>
    /// High-performance, framework-independent stateful mask editing engine.
    /// Manages text buffer, caret advancement, literal skipping, and keystroke validation
    /// for MaskedTextBox controls and In-Place Grid Editors.
    /// </summary>
    public class ZeroMaskEngine
    {
        private MaskDefinition _mask;
        private char _promptChar = '_';
        private char[] _buffer;

        public MaskDefinition Mask => _mask;

        public char PromptChar
        {
            get => _promptChar;
            set
            {
                if (_promptChar != value)
                {
                    char oldPrompt = _promptChar;
                    _promptChar = value;
                    for (int i = 0; i < _buffer.Length; i++)
                    {
                        if (_mask[i].IsEditable && _buffer[i] == oldPrompt)
                        {
                            _buffer[i] = _promptChar;
                        }
                    }
                }
            }
        }

        public int Length => _buffer.Length;

        public ZeroMaskEngine(MaskDefinition mask, char promptChar = '_')
        {
            _mask = mask ?? throw new ArgumentNullException(nameof(mask));
            _promptChar = promptChar;
            _buffer = new char[_mask.Length];
            ResetBuffer();
        }

        public void ChangeMask(MaskDefinition newMask)
        {
            _mask = newMask ?? throw new ArgumentNullException(nameof(newMask));
            _buffer = new char[_mask.Length];
            ResetBuffer();
        }

        public void Clear()
        {
            ResetBuffer();
        }

        private void ResetBuffer()
        {
            for (int i = 0; i < _mask.Length; i++)
            {
                var token = _mask[i];
                _buffer[i] = token.IsEditable ? _promptChar : token.LiteralChar;
            }
        }

        /// <summary>
        /// Attempts to insert a character at the specified caret position.
        /// Automatically skips ahead over non-editable literals and advances caret.
        /// </summary>
        public bool Insert(char c, ref int caretPos)
        {
            int targetPos = _mask.FindNextEditable(caretPos);
            if (targetPos < 0) return false;

            var token = _mask[targetPos];
            if (!token.Matches(c)) return false;

            _buffer[targetPos] = c;

            // Advance caret to next editable position
            int nextPos = _mask.FindNextEditable(targetPos + 1);
            caretPos = nextPos >= 0 ? nextPos : _mask.Length;
            return true;
        }

        /// <summary>
        /// Deletes backwards (Backspace action), clearing previous editable slot to PromptChar.
        /// </summary>
        public bool DeleteBackwards(ref int caretPos)
        {
            if (caretPos <= 0) return false;

            int targetPos = _mask.FindPreviousEditable(caretPos - 1);
            if (targetPos < 0) return false;

            _buffer[targetPos] = _promptChar;
            caretPos = targetPos;
            return true;
        }

        /// <summary>
        /// Deletes forward (Delete key action), clearing current editable slot to PromptChar without moving caret.
        /// </summary>
        public bool DeleteForward(ref int caretPos)
        {
            int targetPos = _mask.FindNextEditable(caretPos);
            if (targetPos < 0) return false;

            _buffer[targetPos] = _promptChar;
            return true;
        }

        /// <summary>
        /// Populates the engine with raw unmasked input string.
        /// </summary>
        public void SetRawText(string? raw)
        {
            if (raw != null)
            {
                SetRawText(raw.AsSpan());
            }
        }

        /// <summary>
        /// Populates the engine with raw unmasked input.
        /// </summary>
        public void SetRawText(ReadOnlySpan<char> raw)
        {
            ResetBuffer();
            _mask.TryFormat(raw, _buffer, out _, _promptChar);
        }

        /// <summary>
        /// Returns the fully formatted string including literals and prompt characters.
        /// </summary>
        public string GetFormattedText()
        {
            return new string(_buffer);
        }

        /// <summary>
        /// Extracts the raw characters without literals and prompt characters.
        /// </summary>
        public string GetRawText()
        {
            Span<char> rawSpan = stackalloc char[_mask.EditableCount];
            if (_mask.TryExtractRaw(_buffer, rawSpan, out int charsWritten, _promptChar))
            {
                return new string(rawSpan.Slice(0, charsWritten).ToArray());
            }
            return string.Empty;
        }

        /// <summary>
        /// Determines whether all required positions in the mask have been filled.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < _mask.Length; i++)
                {
                    var token = _mask[i];
                    if (token.Type == MaskTokenType.DigitRequired ||
                        token.Type == MaskTokenType.LetterRequired ||
                        token.Type == MaskTokenType.AlphanumericRequired ||
                        token.Type == MaskTokenType.Hex)
                    {
                        if (_buffer[i] == _promptChar) return false;
                    }
                }
                return true;
            }
        }
    }
}
