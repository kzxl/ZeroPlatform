namespace ZeroUI.Core.Compliance {
    using System;
    public class SignatureResult {
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsValid { get; set; }
    }
}