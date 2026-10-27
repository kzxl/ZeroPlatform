using System;
using System.Collections.Generic;
using ZeroReports.Data;

namespace ZeroReports.Engine
{
    /// <summary>
    /// Hierarchical Master-Detail report band equivalent to DevExpress DetailReportBand.
    /// Embeds a nested sub-table or child collection (e.g. Invoices -> InvoiceItems -> SerialNumbers).
    /// </summary>
    public class DetailReportBand : ReportBand
    {
        public string DataMember { get; set; } = string.Empty;
        public IReportDataSource? DataSource { get; set; }

        public ReportHeaderBand? Header { get; set; }
        public DetailBand Detail { get; set; } = new DetailBand(20f);
        public List<GroupHeaderBand> GroupHeaders { get; } = new List<GroupHeaderBand>();
        public List<GroupFooterBand> GroupFooters { get; } = new List<GroupFooterBand>();
        public ReportFooterBand? Footer { get; set; }

        public DetailReportBand()
        {
            Height = 0f; // Height is driven by child bands
        }

        public DetailReportBand(string dataMember) : this()
        {
            DataMember = dataMember;
        }

        public DetailReportBand SetDataSource<T>(IEnumerable<T> childItems)
        {
            DataSource = ReportDataSource.FromList(childItems);
            return this;
        }
    }
}
