using System;
using System.Collections.Generic;
using ZeroData.Core;
using ZeroReports.Data;
using ZeroReports.Engine.Pagination;
using ZeroReports.Export;

namespace ZeroReports.Engine
{
    /// <summary>
    /// Core enterprise Banded Report definition.
    /// Manages report bands, data binding, parameters, layout pagination, and multi-format exports.
    /// </summary>
    public class Report
    {
        public string Name { get; set; } = "EnterpriseReport";
        public string Title { get => Name; set => Name = value; }
        public ReportPageSettings PageSettings { get; set; } = new ReportPageSettings();

        private ReportHeaderBand? _reportHeader;
        public ReportHeaderBand ReportHeader
        {
            get => _reportHeader ??= new ReportHeaderBand();
            set => _reportHeader = value;
        }

        private PageHeaderBand? _pageHeader;
        public PageHeaderBand PageHeader
        {
            get => _pageHeader ??= new PageHeaderBand();
            set => _pageHeader = value;
        }

        public List<GroupHeaderBand> GroupHeaders { get; } = new List<GroupHeaderBand>();
        public DetailBand Detail { get; set; } = new DetailBand(24f);
        public List<GroupFooterBand> GroupFooters { get; } = new List<GroupFooterBand>();

        public GroupHeaderBand GroupHeader
        {
            get
            {
                if (GroupHeaders.Count == 0)
                {
                    var gh = new GroupHeaderBand();
                    GroupHeaders.Add(gh);
                    return gh;
                }
                return GroupHeaders[0];
            }
        }

        public GroupFooterBand GroupFooter
        {
            get
            {
                if (GroupFooters.Count == 0)
                {
                    var gf = new GroupFooterBand();
                    GroupFooters.Add(gf);
                    return gf;
                }
                return GroupFooters[0];
            }
        }

        private ReportFooterBand? _reportFooter;
        public ReportFooterBand ReportFooter
        {
            get => _reportFooter ??= new ReportFooterBand();
            set => _reportFooter = value;
        }

        private PageFooterBand? _pageFooter;
        public PageFooterBand PageFooter
        {
            get => _pageFooter ??= new PageFooterBand();
            set => _pageFooter = value;
        }

        public List<DetailReportBand> DetailReports { get; } = new List<DetailReportBand>();
        public Watermark.ReportWatermark? Watermark { get; set; }

        public IReportDataSource? DataSource { get; set; }
        public Dictionary<string, object?> Parameters { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public DetailReportBand AddDetailReport(string dataMember = "")
        {
            var dr = new DetailReportBand(dataMember);
            DetailReports.Add(dr);
            return dr;
        }

        public DetailReportBand AddDetailReport(DetailReportBand dr)
        {
            if (dr == null) throw new ArgumentNullException(nameof(dr));
            DetailReports.Add(dr);
            return dr;
        }

        public Report SetWatermark(string text, float opacity = 0.15f, float rotationAngle = 45f, float fontSize = 54f)
        {
            Watermark = new Watermark.ReportWatermark(text, opacity, rotationAngle)
            {
                FontSize = fontSize
            };
            return this;
        }

        public Report() { }

        public Report(string name)
        {
            Name = name;
        }

        #region Fluent Data Binding Helpers

        public Report SetDataSource<T>(IEnumerable<T> data)
        {
            DataSource = ReportDataSource.FromList(data);
            return this;
        }

        /// <summary>
        /// Binds directly to a ZeroData.Core columnar DataFrame with zero memory copying.
        /// </summary>
        public Report SetDataSource(DataFrame dataFrame)
        {
            DataSource = ReportDataSource.FromDataFrame(dataFrame);
            return this;
        }

        public Report SetDataSource(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
        {
            DataSource = ReportDataSource.FromDictionaries(rows);
            return this;
        }

        public Report SetParameter(string name, object? value)
        {
            Parameters[name] = value;
            return this;
        }

        #endregion

        /// <summary>
        /// Executes layout pagination and data binding, producing a multi-page rendered document.
        /// </summary>
        public RenderedDocument Generate()
        {
            return PageLayoutEngine.Generate(this);
        }

        /// <summary>
        /// Generates the report and exports directly to a vector PDF 1.4 byte array.
        /// </summary>
        public byte[] ExportToPdf()
        {
            var doc = Generate();
            return PdfReportExporter.Export(doc);
        }

        /// <summary>
        /// Generates the report and exports to a standalone HTML string for preview or web views.
        /// </summary>
        public string ExportToHtml()
        {
            var doc = Generate();
            return HtmlReportExporter.Export(doc);
        }
    }
}
