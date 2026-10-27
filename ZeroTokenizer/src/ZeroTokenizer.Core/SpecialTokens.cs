using System;
using System.Collections.Generic;

namespace ZeroTokenizer.Core
{
    /// <summary>
    /// Canonical special tokens for Agentic Small Language Models (Micro-SLM),
    /// Mixture of Experts (MoE), and ZeroPlatform Dialog Systems.
    /// </summary>
    public static class SpecialTokens
    {
        public const string Pad = "<pad>";
        public const string Bos = "<bos>";
        public const string Eos = "<eos>";
        public const string Unk = "<unk>";

        public const int PadId = 0;
        public const int BosId = 1;
        public const int EosId = 2;
        public const int UnkId = 3;

        public const string System = "<system>";
        public const string EndSystem = "</system>";

        public const string User = "<user>";
        public const string EndUser = "</user>";

        public const string Assistant = "<assistant>";
        public const string EndAssistant = "</assistant>";

        public const string Thought = "<thought>";
        public const string EndThought = "</thought>";

        public const string ToolCall = "<tool_call>";
        public const string EndToolCall = "</tool_call>";

        public const string ToolResult = "<tool_result>";
        public const string EndToolResult = "</tool_result>";

        public const string Response = "<response>";
        public const string EndResponse = "</response>";

        public const string Expert = "<expert>";
        public const string EndExpert = "</expert>";

        // Multimodal, Vision & Telemetry Special Tokens
        public const string Image = "<image>";
        public const string EndImage = "</image>";
        public const string VisionPatch = "<vision_patch>";
        public const string Roi = "<roi>";
        public const string EndRoi = "</roi>";
        public const string OcrBlock = "<ocr_block>";
        public const string EndOcrBlock = "</ocr_block>";
        public const string SensorEvent = "<sensor_event>";
        public const string EndSensorEvent = "</sensor_event>";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Pad, Bos, Eos, Unk,
            System, EndSystem,
            User, EndUser,
            Assistant, EndAssistant,
            Thought, EndThought,
            ToolCall, EndToolCall,
            ToolResult, EndToolResult,
            Response, EndResponse,
            Expert, EndExpert,
            Image, EndImage,
            VisionPatch,
            Roi, EndRoi,
            OcrBlock, EndOcrBlock,
            SensorEvent, EndSensorEvent
        };
    }
}
