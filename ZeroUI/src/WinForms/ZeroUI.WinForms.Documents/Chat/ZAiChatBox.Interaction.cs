using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Documents
{
    public partial class ZAiChatBox
    {
        #region Prompt Execution

        private void SubmitPrompt()
        {
            string text = _promptInputBox.Text.Trim();
            if (string.IsNullOrEmpty(text) || _isGenerating) return;

            _promptInputBox.Clear();
            AppendUserMessage(text);
            SendMessageRequested?.Invoke(this, text);

            if (_flowProvider != null)
            {
                _ = ExecuteFlowPromptAsync(text);
            }
        }

        #endregion

        #region Suggestions

        private void OnPromptSuggestionsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RebuildPromptSuggestions();

        public void RebuildPromptSuggestions()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(RebuildPromptSuggestions));
                }
                return;
            }

            _suggestionsPanel.SuspendLayout();
            _suggestionsPanel.Controls.Clear();

            if (_promptSuggestions == null || _promptSuggestions.Count == 0)
            {
                _suggestionsPanel.Visible = false;
                _suggestionsPanel.ResumeLayout();
                return;
            }

            bool isDark = ZeroTheme.IsDark;
            var chipBg = isDark ? Color.FromArgb(28, 38, 56) : Color.FromArgb(255, 255, 255);
            var chipFore = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(51, 65, 85);
            var chipBorder = isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225);

            var chipHoverBg = isDark ? Color.FromArgb(49, 46, 129) : Color.FromArgb(238, 242, 255);
            var chipHoverFore = isDark ? Color.White : Color.FromArgb(67, 56, 202);
            var chipHoverBorder = isDark ? Color.FromArgb(99, 102, 241) : Color.FromArgb(165, 180, 252);

            foreach (var suggestion in _promptSuggestions)
            {
                var chip = new Button
                {
                    Text = suggestion,
                    Font = new Font("Segoe UI", 8.25f),
                    ForeColor = chipFore,
                    BackColor = chipBg,
                    FlatStyle = FlatStyle.Flat,
                    AutoSize = true,
                    Height = 26,
                    Padding = new Padding(8, 2, 8, 2),
                    Margin = new Padding(0, 0, 6, 6),
                    Cursor = Cursors.Hand
                };
                chip.FlatAppearance.BorderColor = chipBorder;
                chip.MouseEnter += (s, e) =>
                {
                    chip.BackColor = chipHoverBg;
                    chip.ForeColor = chipHoverFore;
                    chip.FlatAppearance.BorderColor = chipHoverBorder;
                };
                chip.MouseLeave += (s, e) =>
                {
                    chip.BackColor = chipBg;
                    chip.ForeColor = chipFore;
                    chip.FlatAppearance.BorderColor = chipBorder;
                };
                chip.Click += (s, e) =>
                {
                    _promptInputBox.Text = suggestion;
                    _promptInputBox.Focus();
                    _promptInputBox.SelectionStart = suggestion.Length;
                };
                _suggestionsPanel.Controls.Add(chip);
            }

            _suggestionsPanel.Visible = _suggestionsPanel.Controls.Count > 0;
            _suggestionsPanel.ResumeLayout();
        }

        #endregion
    }

    #region Backward Compatibility Shims

    [Obsolete("AiChatBox is deprecated. Please migrate to ZAiChatBox instead.")]
    [ToolboxItem(false)]
    public class AiChatBox : ZAiChatBox { }

    [Obsolete("ZeroAiChatBox is deprecated. Please migrate to ZAiChatBox instead.")]
    [ToolboxItem(false)]
    public class ZeroAiChatBox : ZAiChatBox { }

    [Obsolete("ZCopilotBox is deprecated. Please migrate to ZAiChatBox instead.")]
    [ToolboxItem(false)]
    public class ZCopilotBox : ZAiChatBox { }

    #endregion
}
