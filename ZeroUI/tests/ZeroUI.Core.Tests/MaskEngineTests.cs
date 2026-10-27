using System;
using Xunit;
using ZeroUI.Core.Input.Masking;

namespace ZeroUI.Core.Tests
{
    public class MaskEngineTests
    {
        [Fact]
        public void MaskDefinition_ParsesTokensAndLiteralsCorrectly()
        {
            var def = new MaskDefinition("000.000.000.000");
            Assert.Equal(15, def.Length);
            Assert.Equal(12, def.EditableCount);

            Assert.Equal(MaskTokenType.DigitRequired, def[0].Type);
            Assert.Equal(MaskTokenType.DigitRequired, def[1].Type);
            Assert.Equal(MaskTokenType.DigitRequired, def[2].Type);
            Assert.Equal(MaskTokenType.Literal, def[3].Type);
            Assert.Equal('.', def[3].LiteralChar);
        }

        [Fact]
        public void MaskDefinition_EscapeCharacter_CreatesLiteral()
        {
            var def = new MaskDefinition(@"\000"); // '\0' is literal '0', then two required digits
            Assert.Equal(3, def.Length);
            Assert.Equal(2, def.EditableCount);
            Assert.Equal(MaskTokenType.Literal, def[0].Type);
            Assert.Equal('0', def[0].LiteralChar);
            Assert.Equal(MaskTokenType.DigitRequired, def[1].Type);
            Assert.Equal(MaskTokenType.DigitRequired, def[2].Type);
        }

        [Fact]
        public void MaskDefinition_TryFormat_ZeroAllocSpanFormatting()
        {
            var def = MaskDefinition.IpV4; // "000.000.000.000"
            Span<char> buffer = stackalloc char[def.Length];

            bool success = def.TryFormat("19216811", buffer, out int charsWritten);
            Assert.True(success);
            Assert.Equal(15, charsWritten);
            Assert.Equal("192.168.11_.___", new string(buffer.ToArray()));
        }

        [Fact]
        public void MaskDefinition_TryExtractRaw_ExtractsCleanData()
        {
            var def = MaskDefinition.MacAddress; // "HH:HH:HH:HH:HH:HH"
            Span<char> raw = stackalloc char[12];

            // Tests string overload
            string masked = "AA:BB:CC:DD:EE:FF";
            bool success = def.TryExtractRaw(masked, raw, out int charsWritten);
            Assert.True(success);
            Assert.Equal(12, charsWritten);
            Assert.Equal("AABBCCDDEEFF", new string(raw.ToArray()));

            // Tests ReadOnlySpan overload
            bool successSpan = def.TryExtractRaw(masked.AsSpan(), raw, out int charsWrittenSpan);
            Assert.True(successSpan);
            Assert.Equal(12, charsWrittenSpan);
        }

        [Fact]
        public void ZeroMaskEngine_SetRawText_StringOverload_Works()
        {
            var engine = new ZeroMaskEngine(MaskDefinition.MacAddress);
            string rawInput = "AABBCCDDEEFF";
            engine.SetRawText(rawInput);

            Assert.Equal("AA:BB:CC:DD:EE:FF", engine.GetFormattedText());
            Assert.Equal("AABBCCDDEEFF", engine.GetRawText());
            Assert.True(engine.IsComplete);
        }

        [Fact]
        public void ZeroMaskEngine_InteractiveTyping_AdvancesCaretOverLiterals()
        {
            var engine = new ZeroMaskEngine(MaskDefinition.IpV4); // "000.000.000.000"
            Assert.Equal("___.___.___.___", engine.GetFormattedText());
            Assert.False(engine.IsComplete);

            int caret = 0;
            // Type '1' at pos 0 -> advances to 1
            Assert.True(engine.Insert('1', ref caret));
            Assert.Equal(1, caret);

            // Type '9' at pos 1 -> advances to 2
            Assert.True(engine.Insert('9', ref caret));
            Assert.Equal(2, caret);

            // Type '2' at pos 2 -> advances over literal '.' to 4
            Assert.True(engine.Insert('2', ref caret));
            Assert.Equal(4, caret);
            Assert.Equal("192.___.___.___", engine.GetFormattedText());

            // Type invalid character 'A' in digit slot -> fails, caret unmoved
            Assert.False(engine.Insert('A', ref caret));
            Assert.Equal(4, caret);
        }

        [Fact]
        public void ZeroMaskEngine_Backspace_RetreatsAndClearsSlot()
        {
            var engine = new ZeroMaskEngine(MaskDefinition.IpV4);
            int caret = 0;
            engine.Insert('1', ref caret); // caret at 1
            engine.Insert('9', ref caret); // caret at 2
            engine.Insert('2', ref caret); // caret at 4 (skipped '.')

            Assert.Equal(4, caret);
            Assert.Equal("192.___.___.___", engine.GetFormattedText());

            // Backspace from pos 4: skips back over '.' and clears pos 2 ('2' -> '_'), caret moves to 2
            Assert.True(engine.DeleteBackwards(ref caret));
            Assert.Equal(2, caret);
            Assert.Equal("19_.___.___.___", engine.GetFormattedText());
        }

        [Fact]
        public void ZeroMaskEngine_SetRawText_And_IsComplete()
        {
            var engine = new ZeroMaskEngine(MaskDefinition.LotCode); // "LOT-0000-AAAA"
            Assert.False(engine.IsComplete);

            engine.SetRawText("2026PROD");
            Assert.Equal("LOT-2026-PROD", engine.GetFormattedText());
            Assert.True(engine.IsComplete);
            Assert.Equal("2026PROD", engine.GetRawText());
        }
    }
}
