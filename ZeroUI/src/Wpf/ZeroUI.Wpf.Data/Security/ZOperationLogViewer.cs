using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Security;
using ZeroUI.Wpf.Base;

namespace ZeroUI.Wpf.Security
{
    /// <summary>
    /// Industrial operation log and audit trail viewer control in WPF for compliance tracking (21 CFR Part 11, ISA-88).
    /// Supports multi-dimensional filtering, severity highlights, and streaming CSV exports.
    /// </summary>
    public class ZOperationLogViewer : WpfControlBase
    {
        private IOperationLogger? _logger;
        private readonly ObservableCollection<OperationLogEntry> _entries = new ObservableCollection<OperationLogEntry>();
        private readonly ListView _listView = new ListView();

        public event EventHandler? Refreshed;

        public IReadOnlyList<OperationLogEntry> Entries => _entries;

        static ZOperationLogViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ZOperationLogViewer),
                new FrameworkPropertyMetadata(typeof(ZOperationLogViewer)));
        }

        public ZOperationLogViewer()
        {
            Width = 850;
            Height = 420;
            Background = new SolidColorBrush(Color.FromRgb(20, 24, 33));

            BuildUI();
            AddVisualChild(_listView);
            AddLogicalChild(_listView);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _listView;

        protected override Size MeasureOverride(Size constraint)
        {
            _listView.Measure(constraint);
            return _listView.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _listView.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private void BuildUI()
        {
            _listView.Background = Brushes.Transparent;
            _listView.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            _listView.Foreground = Brushes.White;
            _listView.ItemsSource = _entries;

            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn { Header = "Timestamp", DisplayMemberBinding = new System.Windows.Data.Binding("TimestampUtc"), Width = 140 });
            gridView.Columns.Add(new GridViewColumn { Header = "Operator", DisplayMemberBinding = new System.Windows.Data.Binding("Username"), Width = 100 });
            gridView.Columns.Add(new GridViewColumn { Header = "Action", DisplayMemberBinding = new System.Windows.Data.Binding("Action"), Width = 140 });
            gridView.Columns.Add(new GridViewColumn { Header = "Category", DisplayMemberBinding = new System.Windows.Data.Binding("Category"), Width = 90 });
            gridView.Columns.Add(new GridViewColumn { Header = "Resource", DisplayMemberBinding = new System.Windows.Data.Binding("TargetResource"), Width = 140 });
            gridView.Columns.Add(new GridViewColumn { Header = "Severity", DisplayMemberBinding = new System.Windows.Data.Binding("Level"), Width = 90 });

            _listView.View = gridView;
        }

        public async Task LoadLogsAsync(IOperationLogger logger, OperationLogFilter? filter = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var logs = await _logger.QueryLogsAsync(filter);

            _entries.Clear();
            foreach (var l in logs)
            {
                _entries.Add(l);
            }
            Refreshed?.Invoke(this, EventArgs.Empty);
        }

        public void ExportToCsv(string filePath)
        {
            using var sw = new StreamWriter(filePath, false, Encoding.UTF8);
            sw.WriteLine("TimestampUtc,Username,Action,Category,TargetResource,OldValue,NewValue,Level,Details");

            foreach (var log in _entries)
            {
                sw.WriteLine($"\"{log.TimestampUtc:O}\",\"{log.Username}\",\"{log.Action}\",\"{log.Category}\",\"{log.TargetResource}\",\"{log.OldValue}\",\"{log.NewValue}\",\"{log.Level}\",\"{log.Details}\"");
            }
        }
    }
}
