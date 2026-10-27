using System;
using System.Runtime.CompilerServices;
using ZeroUI.Core.Memory;

namespace ZeroUI.Core.Rendering
{
    /// <summary>
    /// Type of vector draw command stored in a <see cref="RenderCommandBuffer"/>.
    /// </summary>
    public enum RenderCommandType : byte
    {
        None = 0,
        FillRect = 1,
        DrawRect = 2,
        DrawLine = 3,
        DrawText = 4,
        DrawBadge = 5
    }

    /// <summary>
    /// Compact value struct representing a single vector drawing instruction.
    /// Memory footprint is 36 bytes, allowing dense cache locality during render replay passes.
    /// </summary>
    public readonly struct RenderCommand
    {
        public readonly RenderCommandType Type;
        public readonly byte Alignment;
        public readonly bool IsBold;
        public readonly byte LineWidth;
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;
        public readonly uint Color1;
        public readonly uint Color2;
        public readonly int TextOffset;
        public readonly int TextLength;

        public RenderCommand(
            RenderCommandType type,
            int x, int y, int width, int height,
            uint color1, uint color2 = 0,
            int textOffset = 0, int textLength = 0,
            byte alignment = 0, bool isBold = false, byte lineWidth = 1)
        {
            Type = type;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Color1 = color1;
            Color2 = color2;
            TextOffset = textOffset;
            TextLength = textLength;
            Alignment = alignment;
            IsBold = isBold;
            LineWidth = lineWidth;
        }
    }

    /// <summary>
    /// High-performance pooled command buffer for pre-recording drawing operations.
    /// Decouples cell layout and data virtualization passes from GDI Device Context or GPU device locking.
    /// All internal arrays are rented from <see cref="ZeroBufferPool"/> to eliminate GC allocations on frame passes.
    /// </summary>
    public sealed class RenderCommandBuffer : IDisposable
    {
        private RenderCommand[] _commands;
        private char[] _textBuffer;
        private int _commandCount;
        private int _textCount;
        private bool _isDisposed;

        public int Count => _commandCount;
        public ReadOnlySpan<RenderCommand> Commands => new ReadOnlySpan<RenderCommand>(_commands, 0, _commandCount);

        public RenderCommandBuffer(int initialCommandCapacity = 512, int initialTextCapacity = 4096)
        {
            _commands = ZeroBufferPool.Rent<RenderCommand>(Math.Max(64, initialCommandCapacity));
            _textBuffer = ZeroBufferPool.Rent<char>(Math.Max(512, initialTextCapacity));
            _commandCount = 0;
            _textCount = 0;
        }

        /// <summary>
        /// Resets the command buffer to empty without re-allocating memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _commandCount = 0;
            _textCount = 0;
        }

        /// <summary>
        /// Records a solid rectangle fill command.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFillRect(int x, int y, int width, int height, uint colorRef)
        {
            EnsureCommandCapacity(1);
            _commands[_commandCount++] = new RenderCommand(
                RenderCommandType.FillRect,
                x, y, width, height,
                color1: colorRef);
        }

        /// <summary>
        /// Records a rectangle outline drawing command.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDrawRect(int x, int y, int width, int height, uint colorRef, byte lineWidth = 1)
        {
            EnsureCommandCapacity(1);
            _commands[_commandCount++] = new RenderCommand(
                RenderCommandType.DrawRect,
                x, y, width, height,
                color1: colorRef,
                lineWidth: lineWidth);
        }

        /// <summary>
        /// Records a line drawing command.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDrawLine(int x1, int y1, int x2, int y2, uint colorRef, byte lineWidth = 1)
        {
            EnsureCommandCapacity(1);
            _commands[_commandCount++] = new RenderCommand(
                RenderCommandType.DrawLine,
                x1, y1, x2, y2,
                color1: colorRef,
                lineWidth: lineWidth);
        }

        /// <summary>
        /// Records a text rendering command. Copies the character span into the contiguous pooled text buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDrawText(
            int x, int y, int width, int height,
            ReadOnlySpan<char> text,
            uint textColorRef, uint backColorRef = 0,
            byte alignment = 0, bool isBold = false)
        {
            EnsureCommandCapacity(1);
            int textOffset = _textCount;
            int textLen = text.Length;

            if (textLen > 0)
            {
                EnsureTextCapacity(textLen);
                text.CopyTo(_textBuffer.AsSpan(_textCount));
                _textCount += textLen;
            }

            _commands[_commandCount++] = new RenderCommand(
                RenderCommandType.DrawText,
                x, y, width, height,
                color1: textColorRef,
                color2: backColorRef,
                textOffset: textOffset,
                textLength: textLen,
                alignment: alignment,
                isBold: isBold);
        }

        /// <summary>
        /// Resolves the character slice for a text draw command.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> GetTextSpan(in RenderCommand command)
        {
            if (command.TextLength <= 0 || command.TextOffset < 0 || command.TextOffset + command.TextLength > _textCount)
            {
                return ReadOnlySpan<char>.Empty;
            }
            return new ReadOnlySpan<char>(_textBuffer, command.TextOffset, command.TextLength);
        }

        private void EnsureCommandCapacity(int additional)
        {
            if (_commandCount + additional > _commands.Length)
            {
                int newCap = Math.Max(_commands.Length * 2, _commandCount + additional + 64);
                var newBuffer = ZeroBufferPool.Rent<RenderCommand>(newCap);
                Array.Copy(_commands, newBuffer, _commandCount);
                ZeroBufferPool.Return(_commands);
                _commands = newBuffer;
            }
        }

        private void EnsureTextCapacity(int additional)
        {
            if (_textCount + additional > _textBuffer.Length)
            {
                int newCap = Math.Max(_textBuffer.Length * 2, _textCount + additional + 256);
                var newBuffer = ZeroBufferPool.Rent<char>(newCap);
                Array.Copy(_textBuffer, newBuffer, _textCount);
                ZeroBufferPool.Return(_textBuffer);
                _textBuffer = newBuffer;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                ZeroBufferPool.Return(_commands);
                ZeroBufferPool.Return(_textBuffer);
                _commandCount = 0;
                _textCount = 0;
            }
        }
    }
}
