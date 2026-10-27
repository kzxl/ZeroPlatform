using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZeroPrimitives;
using ZeroReports.Barcodes;
using ZeroReports.Data;
using ZeroReports.Engine.Table;
using ZeroReports.Engine.Watermark;

namespace ZeroReports.Engine.Pagination
{
    /// <summary>
    /// Multi-page layout and pagination engine for Banded Reports.
    /// Handles smart page breaks, band flow, token resolution, data binding, and two-pass page numbering.
    /// </summary>
    public static class PageLayoutEngine
    {
        public static RenderedDocument Render(Report report) => Generate(report);

        public static RenderedDocument Generate(Report report)
        {
            var ps = report.PageSettings;
            var doc = new RenderedDocument
            {
                Title = report.Name,
                PageWidth = ps.Width,
                PageHeight = ps.Height
            };

            float pageFooterHeight = (report.PageFooter != null && report.PageFooter.Visible) ? report.PageFooter.Height : 0f;
            float maxPageY = ps.Height - ps.MarginBottom - pageFooterHeight;

            var ds = report.DataSource;
            int totalRecords = ds?.RecordCount ?? 0;

            // Partition records into groups
            List<DataGroup> groups;
            if (report.GroupHeaders.Count > 0 && ds != null && totalRecords > 0)
            {
                string grpField = report.GroupHeaders[0].GroupField;
                bool asc = report.GroupHeaders[0].SortAscending;
                groups = ReportDataSource.GroupRecords(ds, grpField, asc);
            }
            else
            {
                var singleGroup = new DataGroup(string.Empty, null);
                if (totalRecords > 0)
                {
                    for (int i = 0; i < totalRecords; i++) singleGroup.RecordIndices.Add(i);
                }
                else
                {
                    // For unbound reports (no data source specified), render Detail band once
                    singleGroup.RecordIndices.Add(-1);
                }
                groups = new List<DataGroup> { singleGroup };
            }

            var allIndices = new List<int>();
            for (int i = 0; i < totalRecords; i++) allIndices.Add(i);

            // Page collection
            var pages = new List<RenderedPage>();
            RenderedPage currentPage = new RenderedPage(1, ps.Width, ps.Height);
            pages.Add(currentPage);

            float currentY = ps.MarginTop;

            void StartNewPage()
            {
                currentPage = new RenderedPage(pages.Count + 1, ps.Width, ps.Height);
                pages.Add(currentPage);
                currentY = ps.MarginTop;

                // Render PageHeader at top of new page
                if (report.PageHeader != null && report.PageHeader.Visible)
                {
                    RenderBandControls(report.PageHeader, currentPage, ps.MarginLeft, currentY, ds, -1, null, allIndices, report.Parameters);
                    currentY += report.PageHeader.Height;
                }
            }

            // 1. Report Header on first page
            if (report.ReportHeader != null && report.ReportHeader.Visible)
            {
                RenderBandControls(report.ReportHeader, currentPage, ps.MarginLeft, currentY, ds, -1, null, allIndices, report.Parameters);
                currentY += report.ReportHeader.Height;
            }

            // 2. Page Header on first page
            if (report.PageHeader != null && report.PageHeader.Visible && report.PageHeader.PrintOnFirstPage)
            {
                RenderBandControls(report.PageHeader, currentPage, ps.MarginLeft, currentY, ds, -1, null, allIndices, report.Parameters);
                currentY += report.PageHeader.Height;
            }

            // 3. Process Groups and Detail Records
            for (int g = 0; g < groups.Count; g++)
            {
                var grp = groups[g];

                // GroupHeader
                if (report.GroupHeaders.Count > 0)
                {
                    var gh = report.GroupHeaders[0];
                    if (gh.PageBreakBefore && currentY > ps.MarginTop + 10f)
                    {
                        StartNewPage();
                    }
                    else if (currentY + gh.Height > maxPageY)
                    {
                        StartNewPage();
                    }

                    RenderBandControls(gh, currentPage, ps.MarginLeft, currentY, ds, -1, grp, allIndices, report.Parameters);
                    currentY += gh.Height;
                }

                // Detail Rows
                foreach (int rowIdx in grp.RecordIndices)
                {
                    if (currentY + report.Detail.Height > maxPageY)
                    {
                        StartNewPage();
                    }

                    RenderBandControls(report.Detail, currentPage, ps.MarginLeft, currentY, ds, rowIdx, grp, allIndices, report.Parameters);
                    currentY += report.Detail.Height;

                    // Master-Detail Sub-Reports (DevExpress DetailReportBand parity)
                    foreach (var dr in report.DetailReports)
                    {
                        if (!dr.Visible) continue;

                        IReportDataSource? subDs = dr.DataSource;
                        if (subDs == null && !string.IsNullOrEmpty(dr.DataMember) && ds != null)
                        {
                            object? childObj = ds.GetValue(rowIdx, dr.DataMember);
                            if (childObj is IEnumerable childEnumerable)
                            {
                                subDs = ReportDataSource.FromEnumerable(childEnumerable);
                            }
                        }

                        if (subDs != null && subDs.RecordCount > 0)
                        {
                            var subIndices = new List<int>();
                            for (int si = 0; si < subDs.RecordCount; si++) subIndices.Add(si);

                            if (dr.Header != null && dr.Header.Visible)
                            {
                                if (currentY + dr.Header.Height > maxPageY) StartNewPage();
                                RenderBandControls(dr.Header, currentPage, ps.MarginLeft + 15f, currentY, subDs, -1, null, subIndices, report.Parameters);
                                currentY += dr.Header.Height;
                            }

                            for (int cr = 0; cr < subDs.RecordCount; cr++)
                            {
                                if (currentY + dr.Detail.Height > maxPageY) StartNewPage();
                                RenderBandControls(dr.Detail, currentPage, ps.MarginLeft + 15f, currentY, subDs, cr, null, subIndices, report.Parameters);
                                currentY += dr.Detail.Height;
                            }

                            if (dr.Footer != null && dr.Footer.Visible)
                            {
                                if (currentY + dr.Footer.Height > maxPageY) StartNewPage();
                                RenderBandControls(dr.Footer, currentPage, ps.MarginLeft + 15f, currentY, subDs, -1, null, subIndices, report.Parameters);
                                currentY += dr.Footer.Height;
                            }
                        }
                    }
                }

                // GroupFooter
                if (report.GroupFooters.Count > 0)
                {
                    var gf = report.GroupFooters[0];
                    if (currentY + gf.Height > maxPageY)
                    {
                        StartNewPage();
                    }

                    RenderBandControls(gf, currentPage, ps.MarginLeft, currentY, ds, -1, grp, allIndices, report.Parameters);
                    currentY += gf.Height;

                    if (gf.PageBreakAfter && g < groups.Count - 1)
                    {
                        StartNewPage();
                    }
                }
            }

            // 4. Report Footer (Grand Totals / Summary)
            if (report.ReportFooter != null && report.ReportFooter.Visible)
            {
                if (currentY + report.ReportFooter.Height > maxPageY)
                {
                    StartNewPage();
                }

                RenderBandControls(report.ReportFooter, currentPage, ps.MarginLeft, currentY, ds, -1, null, allIndices, report.Parameters);
                currentY += report.ReportFooter.Height;
            }

            // 5. Two-Pass Watermark & Page Footer Resolution
            int totalPageCount = pages.Count;
            for (int p = 0; p < pages.Count; p++)
            {
                var page = pages[p];
                int pageNumber = p + 1;

                // Add Watermark in background (index 0) if enabled
                if (report.Watermark != null && report.Watermark.Enabled && !string.IsNullOrEmpty(report.Watermark.Text))
                {
                    page.Items.Insert(0, new RenderedWatermarkItem
                    {
                        X = ps.Width * 0.5f,
                        Y = ps.Height * 0.5f,
                        Width = ps.Width,
                        Height = ps.Height,
                        Text = report.Watermark.Text,
                        FontSize = report.Watermark.FontSize,
                        RotationAngle = report.Watermark.RotationAngle,
                        Opacity = report.Watermark.Opacity,
                        Color = report.Watermark.Color
                    });
                }

                if (report.PageFooter != null && report.PageFooter.Visible)
                {
                    float footerY = ps.Height - ps.MarginBottom - report.PageFooter.Height;
                    RenderBandControls(report.PageFooter, page, ps.MarginLeft, footerY, ds, -1, null, allIndices, report.Parameters, pageNumber, totalPageCount);
                }

                doc.Pages.Add(page);
            }

            return doc;
        }

        private static void RenderBandControls(
            ReportBand band,
            RenderedPage page,
            float bandX,
            float bandY,
            IReportDataSource? dataSource,
            int recordIndex,
            DataGroup? currentGroup,
            IReadOnlyList<int> allIndices,
            IReadOnlyDictionary<string, object?> parameters,
            int pageNumber = 1,
            int totalPages = 1)
        {
            foreach (var ctrl in band.Controls)
            {
                if (!ctrl.Visible) continue;

                float x = bandX + ctrl.Left;
                float y = bandY + ctrl.Top;

                if (ctrl is LabelControl lbl)
                {
                    var textColor = lbl.TextColor;
                    var bgColor = lbl.BackgroundColor;
                    bool isBold = lbl.IsBold;
                    bool visible = lbl.Visible;

                    foreach (var rule in lbl.FormattingRules)
                    {
                        if (rule.Evaluate(dataSource, recordIndex))
                        {
                            if (rule.TextColor.HasValue) textColor = rule.TextColor.Value;
                            if (rule.BackgroundColor.HasValue) bgColor = rule.BackgroundColor.Value;
                            if (rule.IsBold.HasValue) isBold = rule.IsBold.Value;
                            if (rule.Visible.HasValue) visible = rule.Visible.Value;
                        }
                    }

                    if (!visible) continue;

                    if (bgColor.HasValue)
                    {
                        page.Items.Add(new RenderedBoxItem
                        {
                            X = x,
                            Y = y,
                            Width = ctrl.Width,
                            Height = ctrl.Height,
                            Fill = true,
                            Stroke = false,
                            FillColor = bgColor.Value
                        });
                    }

                    string text = ResolveTokens(lbl.Text, lbl.DataField, lbl.Format, dataSource, recordIndex, currentGroup, parameters, pageNumber, totalPages);
                    page.Items.Add(new RenderedTextItem
                    {
                        X = x,
                        Y = y,
                        Width = ctrl.Width,
                        Height = ctrl.Height,
                        Text = text,
                        FontSize = lbl.FontSize,
                        IsBold = isBold,
                        Alignment = lbl.Alignment,
                        Color = textColor
                    });
                }
                else if (ctrl is ReportTable tbl)
                {
                    float rowY = y;
                    foreach (var row in tbl.Rows)
                    {
                        float cellX = x;
                        int colIdx = 0;

                        foreach (var cell in row.Cells)
                        {
                            float cellW = 0f;
                            for (int c = 0; c < cell.ColSpan; c++)
                            {
                                int targetCol = colIdx + c;
                                if (targetCol < tbl.ColumnWidths.Count)
                                {
                                    cellW += tbl.ColumnWidths[targetCol];
                                }
                                else
                                {
                                    cellW += 50f;
                                }
                            }
                            colIdx += cell.ColSpan;

                            var cellTextColor = cell.TextColor;
                            var cellBgColor = cell.BackgroundColor;
                            bool cellIsBold = cell.IsBold;

                            foreach (var rule in cell.FormattingRules)
                            {
                                if (rule.Evaluate(dataSource, recordIndex))
                                {
                                    if (rule.TextColor.HasValue) cellTextColor = rule.TextColor.Value;
                                    if (rule.BackgroundColor.HasValue) cellBgColor = rule.BackgroundColor.Value;
                                    if (rule.IsBold.HasValue) cellIsBold = rule.IsBold.Value;
                                }
                            }

                            if (cellBgColor.HasValue)
                            {
                                page.Items.Add(new RenderedBoxItem
                                {
                                    X = cellX,
                                    Y = rowY,
                                    Width = cellW,
                                    Height = row.Height,
                                    Fill = true,
                                    Stroke = false,
                                    FillColor = cellBgColor.Value
                                });
                            }

                            if (cell.Borders != CellBorders.None)
                            {
                                if (cell.Borders.HasFlag(CellBorders.Top))
                                    page.Items.Add(new RenderedLineItem { X = cellX, Y = rowY, X2 = cellX + cellW, Y2 = rowY, LineWidth = cell.BorderWidth, Color = cell.BorderColor });
                                if (cell.Borders.HasFlag(CellBorders.Bottom))
                                    page.Items.Add(new RenderedLineItem { X = cellX, Y = rowY + row.Height, X2 = cellX + cellW, Y2 = rowY + row.Height, LineWidth = cell.BorderWidth, Color = cell.BorderColor });
                                if (cell.Borders.HasFlag(CellBorders.Left))
                                    page.Items.Add(new RenderedLineItem { X = cellX, Y = rowY, X2 = cellX, Y2 = rowY + row.Height, LineWidth = cell.BorderWidth, Color = cell.BorderColor });
                                if (cell.Borders.HasFlag(CellBorders.Right))
                                    page.Items.Add(new RenderedLineItem { X = cellX + cellW, Y = rowY, X2 = cellX + cellW, Y2 = rowY + row.Height, LineWidth = cell.BorderWidth, Color = cell.BorderColor });
                            }

                            string cellText = ResolveTokens(cell.Text, cell.DataField, cell.Format, dataSource, recordIndex, currentGroup, parameters, pageNumber, totalPages);
                            if (!string.IsNullOrEmpty(cellText))
                            {
                                float pad = 3f;
                                page.Items.Add(new RenderedTextItem
                                {
                                    X = cellX + pad,
                                    Y = rowY + Math.Max(0f, (row.Height - cell.FontSize) * 0.5f),
                                    Width = cellW - (pad * 2f),
                                    Height = row.Height,
                                    Text = cellText,
                                    FontSize = cell.FontSize,
                                    IsBold = cellIsBold,
                                    Alignment = cell.Alignment,
                                    Color = cellTextColor
                                });
                            }

                            cellX += cellW;
                        }

                        rowY += row.Height;
                    }
                }
                else if (ctrl is ExpressionControl expr)
                {
                    var evalIndices = (expr.Scope == ExpressionScope.Group && currentGroup != null) ? currentGroup.RecordIndices : allIndices;
                    string valStr = AggregateEvaluator.EvaluateExpression(expr.Expression, dataSource!, evalIndices, expr.Format);
                    valStr = ResolveTokens(valStr, string.Empty, string.Empty, dataSource, recordIndex, currentGroup, parameters, pageNumber, totalPages);

                    var textColor = expr.TextColor;
                    bool isBold = expr.IsBold;
                    bool visible = expr.Visible;

                    foreach (var rule in expr.FormattingRules)
                    {
                        if (rule.Evaluate(dataSource, recordIndex))
                        {
                            if (rule.TextColor.HasValue) textColor = rule.TextColor.Value;
                            if (rule.IsBold.HasValue) isBold = rule.IsBold.Value;
                            if (rule.Visible.HasValue) visible = rule.Visible.Value;
                        }
                    }

                    if (!visible) continue;

                    page.Items.Add(new RenderedTextItem
                    {
                        X = x,
                        Y = y,
                        Width = ctrl.Width,
                        Height = ctrl.Height,
                        Text = valStr,
                        FontSize = expr.FontSize,
                        IsBold = isBold,
                        Alignment = expr.Alignment,
                        Color = textColor
                    });
                }
                else if (ctrl is PageNumberControl pnc)
                {
                    string text = pnc.Format
                        .Replace("{PageNumber}", pageNumber.ToString())
                        .Replace("{TotalPages}", totalPages.ToString());

                    page.Items.Add(new RenderedTextItem
                    {
                        X = x,
                        Y = y,
                        Width = ctrl.Width,
                        Height = ctrl.Height,
                        Text = text,
                        FontSize = pnc.FontSize,
                        IsBold = pnc.IsBold,
                        Alignment = pnc.Alignment,
                        Color = pnc.TextColor
                    });
                }
                else if (ctrl is LineControl line)
                {
                    page.Items.Add(new RenderedLineItem
                    {
                        X = x,
                        Y = y,
                        X2 = x + ctrl.Width,
                        Y2 = y,
                        LineWidth = line.LineWidth,
                        Color = line.LineColor
                    });
                }
                else if (ctrl is BoxControl box)
                {
                    page.Items.Add(new RenderedBoxItem
                    {
                        X = x,
                        Y = y,
                        Width = ctrl.Width,
                        Height = ctrl.Height,
                        Fill = box.Fill,
                        Stroke = box.Stroke,
                        BorderWidth = box.BorderWidth,
                        FillColor = box.FillColor,
                        BorderColor = box.BorderColor
                    });
                }
                else if (ctrl is BarcodeControl bc)
                {
                    string rawData = !string.IsNullOrEmpty(bc.DataField) && recordIndex >= 0 && dataSource != null
                        ? dataSource.GetValue(recordIndex, bc.DataField)?.ToString() ?? string.Empty
                        : ResolveTokens(bc.Data, string.Empty, string.Empty, dataSource, recordIndex, currentGroup, parameters, pageNumber, totalPages);

                    var item = new RenderedBarcodeItem
                    {
                        X = x,
                        Y = y,
                        Width = ctrl.Width,
                        Height = ctrl.Height,
                        Symbology = bc.Symbology,
                        Text = rawData
                    };

                    if (bc.Symbology == BarcodeSymbology.Code128)
                    {
                        item.Result1D = Code128Encoder.Encode(rawData, bc.ModuleWidth);
                    }
                    else if (bc.Symbology == BarcodeSymbology.Ean13)
                    {
                        item.Result1D = Ean13Encoder.Encode(rawData, bc.ModuleWidth);
                    }
                    else if (bc.Symbology == BarcodeSymbology.QrCode)
                    {
                        item.Result2D = QrCodeEncoder.Encode(rawData);
                    }

                    page.Items.Add(item);
                }
                else if (ctrl is ImageControl img && img.ImageData != null && img.ImageData.Length > 0)
                {
                    page.Items.Add(new RenderedImageItem
                    {
                        X = x,
                        Y = y,
                        Width = ctrl.Width,
                        Height = ctrl.Height,
                        ImageData = img.ImageData
                    });
                }
                else if (ctrl is ChartReportControl chart && chart.ChartImageData != null && chart.ChartImageData.Length > 0)
                {
                    page.Items.Add(new RenderedImageItem
                    {
                        X = x,
                        Y = y,
                        Width = ctrl.Width,
                        Height = ctrl.Height,
                        ImageData = chart.ChartImageData
                    });
                }
            }
        }

        private static string ResolveTokens(
            string template,
            string dataField,
            string format,
            IReportDataSource? dataSource,
            int recordIndex,
            DataGroup? currentGroup,
            IReadOnlyDictionary<string, object?> parameters,
            int pageNumber,
            int totalPages)
        {
            if (!string.IsNullOrEmpty(dataField) && recordIndex >= 0 && dataSource != null)
            {
                object? val = dataSource.GetValue(recordIndex, dataField);
                if (val == null) return string.Empty;

                if (!string.IsNullOrEmpty(format))
                {
                    return string.Format(CultureInfo.CurrentCulture, "{0:" + format + "}", val);
                }
                return FastConvert.AsString(val);
            }

            if (string.IsNullOrEmpty(template)) return string.Empty;

            string text = template;

            // Substitute Group Key
            if (currentGroup != null)
            {
                string grpKeyStr = currentGroup.Key?.ToString() ?? string.Empty;
                text = text.Replace("{GroupValue}", grpKeyStr);
                text = text.Replace("[GroupValue]", grpKeyStr);
                if (!string.IsNullOrEmpty(currentGroup.FieldName))
                {
                    text = text.Replace("{" + currentGroup.FieldName + "}", grpKeyStr);
                    text = text.Replace("[" + currentGroup.FieldName + "]", grpKeyStr);
                }
            }

            // Substitute Parameters
            foreach (var kv in parameters)
            {
                string pVal = kv.Value?.ToString() ?? string.Empty;
                text = text.Replace("{" + kv.Key + "}", pVal);
                text = text.Replace("[" + kv.Key + "]", pVal);
            }

            // Substitute record fields if row index is active
            if (recordIndex >= 0 && dataSource != null)
            {
                foreach (var fn in dataSource.FieldNames)
                {
                    object? fVal = dataSource.GetValue(recordIndex, fn);
                    string rawStr = FastConvert.AsString(fVal);

                    // Plain tokens: {Field} and [Field]
                    text = text.Replace("{" + fn + "}", rawStr);
                    text = text.Replace("[" + fn + "]", rawStr);

                    // Formatted tokens: [Field:Format]
                    string prefixBracket = "[" + fn + ":";
                    int bIdx = text.IndexOf(prefixBracket, StringComparison.OrdinalIgnoreCase);
                    while (bIdx >= 0)
                    {
                        int closeIdx = text.IndexOf(']', bIdx);
                        if (closeIdx > bIdx)
                        {
                            string fmt = text.Substring(bIdx + prefixBracket.Length, closeIdx - (bIdx + prefixBracket.Length));
                            string formatted = (fVal != null)
                                ? string.Format(CultureInfo.CurrentCulture, "{0:" + fmt + "}", fVal)
                                : string.Empty;
                            text = text.Substring(0, bIdx) + formatted + text.Substring(closeIdx + 1);
                            bIdx = text.IndexOf(prefixBracket, StringComparison.OrdinalIgnoreCase);
                        }
                        else break;
                    }

                    // Formatted tokens: {Field:Format}
                    string prefixBrace = "{" + fn + ":";
                    int cIdx = text.IndexOf(prefixBrace, StringComparison.OrdinalIgnoreCase);
                    while (cIdx >= 0)
                    {
                        int closeIdx = text.IndexOf('}', cIdx);
                        if (closeIdx > cIdx)
                        {
                            string fmt = text.Substring(cIdx + prefixBrace.Length, closeIdx - (cIdx + prefixBrace.Length));
                            string formatted = (fVal != null)
                                ? string.Format(CultureInfo.CurrentCulture, "{0:" + fmt + "}", fVal)
                                : string.Empty;
                            text = text.Substring(0, cIdx) + formatted + text.Substring(closeIdx + 1);
                            cIdx = text.IndexOf(prefixBrace, StringComparison.OrdinalIgnoreCase);
                        }
                        else break;
                    }
                }
            }

            // Page tokens
            text = text.Replace("{PageNumber}", pageNumber.ToString());
            text = text.Replace("[PageNumber]", pageNumber.ToString());
            text = text.Replace("{TotalPages}", totalPages.ToString());
            text = text.Replace("[TotalPages]", totalPages.ToString());

            return text;
        }
    }
}
