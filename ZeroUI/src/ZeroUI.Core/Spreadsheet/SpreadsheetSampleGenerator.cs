using System;

namespace ZeroUI.Core.Spreadsheet
{
    /// <summary>
    /// Generates pre-populated factory costing BOM worksheets and Six Sigma quality inspection sheets.
    /// Used for instant demonstration, interactive testing, and automated formula verification.
    /// </summary>
    public static class SpreadsheetSampleGenerator
    {
        public static SpreadsheetWorksheet CreateFactoryCostingWorksheet()
        {
            var ws = new SpreadsheetWorksheet("Plant Costing & BOM", rowCount: 40, columnCount: 16);

            ws.SetColumnWidth(0, 110); // A: Part Code
            ws.SetColumnWidth(1, 230); // B: Description
            ws.SetColumnWidth(2, 95);  // C: Required Qty
            ws.SetColumnWidth(3, 105); // D: Unit Cost ($)
            ws.SetColumnWidth(4, 115); // E: Base Cost ($)
            ws.SetColumnWidth(5, 95);  // F: Yield %
            ws.SetColumnWidth(6, 125); // G: Adjusted Cost ($)
            ws.SetColumnWidth(7, 100); // H: Status

            // Title Banner (Row 0)
            ws.SetRowHeight(0, 32);
            var titleCell = ws.GetOrCreateCell(0, 0);
            titleCell.RawValue = "LINE 02 EXTRUDER — MANUFACTURING BILL OF MATERIALS & COSTING";
            titleCell.EvaluatedValue = titleCell.RawValue;
            titleCell.IsBold = true;
            titleCell.TextColor = 0xFFFFFFFF;
            titleCell.BackgroundColor = 0xFF0284C7; // Cyan/Blue 600

            // Subtitle (Row 1)
            var subCell = ws.GetOrCreateCell(1, 0);
            subCell.RawValue = "Work Order: WO-2026-9042  |  Product: Extruder Control Unit v4  |  Target Lot: 2,500 Sets";
            subCell.EvaluatedValue = subCell.RawValue;
            subCell.IsItalic = true;
            subCell.TextColor = 0xFF64748B;

            // Table Headers (Row 2)
            ws.SetRowHeight(2, 26);
            string[] headers = { "Part Code", "Component Description", "Required Qty", "Unit Cost ($)", "Base Cost ($)", "Yield Rate", "Adjusted Cost ($)", "Feeder Zone" };
            for (int c = 0; c < headers.Length; c++)
            {
                var hCell = ws.GetOrCreateCell(2, c);
                hCell.RawValue = headers[c];
                hCell.EvaluatedValue = headers[c];
                hCell.IsBold = true;
                hCell.TextColor = 0xFF0F172A;
                hCell.BackgroundColor = 0xFFE2E8F0;
                hCell.Alignment = (c >= 2 && c <= 6) ? SpreadsheetAlignment.Right : SpreadsheetAlignment.Left;
            }

            // Data Rows (Rows 3 to 7: 1-indexed in sheet as 4 to 8)
            var items = new (string code, string desc, double qty, double cost, double yieldRate, string zone)[]
            {
                ("RES-10K-0402", "SMD Thick Film Resistor 10k", 25000, 0.005, 0.985, "Zone A (SMT)"),
                ("CAP-100N-0603", "Ceramic Capacitor 100nF 50V", 12500, 0.012, 0.990, "Zone A (SMT)"),
                ("MCU-STM32F4", "ARM Cortex-M4 168MHz MCU", 2500, 4.250, 0.975, "Zone B (Tray)"),
                ("CONN-RJ45-1G", "Ethernet MagJack 1000Base-T", 2500, 1.850, 0.995, "Zone C (Through-Hole)"),
                ("REG-LM2596-5", "DC-DC Step Down Converter 5V", 2500, 0.950, 0.980, "Zone C (Through-Hole)")
            };

            for (int i = 0; i < items.Length; i++)
            {
                int r = 3 + i;
                int sheetRow = r + 1; // 4, 5, 6, 7, 8
                var itm = items[i];

                // Part Code
                var cCode = ws.GetOrCreateCell(r, 0);
                cCode.RawValue = itm.code;
                cCode.EvaluatedValue = itm.code;
                cCode.IsBold = true;

                // Description
                var cDesc = ws.GetOrCreateCell(r, 1);
                cDesc.RawValue = itm.desc;
                cDesc.EvaluatedValue = itm.desc;

                // Qty (C)
                var cQty = ws.GetOrCreateCell(r, 2);
                cQty.RawValue = itm.qty.ToString();
                cQty.EvaluatedValue = itm.qty;
                cQty.FormatType = SpreadsheetFormatType.Integer;
                cQty.Alignment = SpreadsheetAlignment.Right;

                // Unit Cost (D)
                var cCost = ws.GetOrCreateCell(r, 3);
                cCost.RawValue = itm.cost.ToString();
                cCost.EvaluatedValue = itm.cost;
                cCost.FormatType = SpreadsheetFormatType.Currency;
                cCost.Alignment = SpreadsheetAlignment.Right;

                // Base Cost (E): =C{sheetRow}*D{sheetRow}
                var cBase = ws.GetOrCreateCell(r, 4);
                cBase.RawValue = $"=C{sheetRow}*D{sheetRow}";
                cBase.FormatType = SpreadsheetFormatType.Currency;
                cBase.Alignment = SpreadsheetAlignment.Right;

                // Yield Rate (F)
                var cYield = ws.GetOrCreateCell(r, 5);
                cYield.RawValue = itm.yieldRate.ToString();
                cYield.EvaluatedValue = itm.yieldRate;
                cYield.FormatType = SpreadsheetFormatType.Percentage;
                cYield.Alignment = SpreadsheetAlignment.Right;

                // Adjusted Cost (G): =E{sheetRow}/F{sheetRow}
                var cAdj = ws.GetOrCreateCell(r, 6);
                cAdj.RawValue = $"=E{sheetRow}/F{sheetRow}";
                cAdj.FormatType = SpreadsheetFormatType.Currency;
                cAdj.Alignment = SpreadsheetAlignment.Right;

                // Zone (H)
                var cZone = ws.GetOrCreateCell(r, 7);
                cZone.RawValue = itm.zone;
                cZone.EvaluatedValue = itm.zone;
            }

            // Summary Row (Row 8: Sheet Row 9)
            int sumRow = 3 + items.Length;
            ws.SetRowHeight(sumRow, 26);

            var cSumLabel = ws.GetOrCreateCell(sumRow, 1);
            cSumLabel.RawValue = "TOTAL WORK ORDER BILL OF MATERIALS COST:";
            cSumLabel.EvaluatedValue = cSumLabel.RawValue;
            cSumLabel.IsBold = true;
            cSumLabel.Alignment = SpreadsheetAlignment.Right;

            // Total Qty: =SUM(C4:C8)
            var cSumQty = ws.GetOrCreateCell(sumRow, 2);
            cSumQty.RawValue = "=SUM(C4:C8)";
            cSumQty.FormatType = SpreadsheetFormatType.Integer;
            cSumQty.IsBold = true;
            cSumQty.Alignment = SpreadsheetAlignment.Right;

            // Total Base Cost: =SUM(E4:E8)
            var cSumBase = ws.GetOrCreateCell(sumRow, 4);
            cSumBase.RawValue = "=SUM(E4:E8)";
            cSumBase.FormatType = SpreadsheetFormatType.Currency;
            cSumBase.IsBold = true;
            cSumBase.Alignment = SpreadsheetAlignment.Right;
            cSumBase.BackgroundColor = 0xFFF0FDF4; // Light green

            // Average Yield: =AVERAGE(F4:F8)
            var cAvgYield = ws.GetOrCreateCell(sumRow, 5);
            cAvgYield.RawValue = "=AVERAGE(F4:F8)";
            cAvgYield.FormatType = SpreadsheetFormatType.Percentage;
            cAvgYield.IsBold = true;
            cAvgYield.Alignment = SpreadsheetAlignment.Right;

            // Total Adjusted Cost: =SUM(G4:G8)
            var cSumAdj = ws.GetOrCreateCell(sumRow, 6);
            cSumAdj.RawValue = "=SUM(G4:G8)";
            cSumAdj.FormatType = SpreadsheetFormatType.Currency;
            cSumAdj.IsBold = true;
            cSumAdj.Alignment = SpreadsheetAlignment.Right;
            cSumAdj.BackgroundColor = 0xFFFEF3C7; // Amber tint

            // Recalculate all formulas
            ws.RecalculateAllFormulas();

            return ws;
        }

        public static SpreadsheetWorksheet CreateQualitySpcWorksheet()
        {
            var ws = new SpreadsheetWorksheet("QC Six Sigma Sheet", rowCount: 30, columnCount: 10);

            ws.SetColumnWidth(0, 75);  // Sample No
            ws.SetColumnWidth(1, 120); // Target Setpoint
            ws.SetColumnWidth(2, 120); // Measured Value
            ws.SetColumnWidth(3, 110); // Deviation
            ws.SetColumnWidth(4, 110); // Inspection Result

            // Title
            var title = ws.GetOrCreateCell(0, 0);
            title.RawValue = "AOI STATISTICAL PROCESS QUALITY CONTROL SHEET";
            title.EvaluatedValue = title.RawValue;
            title.IsBold = true;
            title.TextColor = 0xFFFFFFFF;
            title.BackgroundColor = 0xFF10B981; // Emerald

            // Headers
            string[] headers = { "Sample #", "Target (mm)", "Measured (mm)", "Deviation", "Pass / Fail" };
            for (int c = 0; c < headers.Length; c++)
            {
                var h = ws.GetOrCreateCell(1, c);
                h.RawValue = headers[c];
                h.EvaluatedValue = headers[c];
                h.IsBold = true;
                h.BackgroundColor = 0xFFE2E8F0;
                h.Alignment = c > 0 ? SpreadsheetAlignment.Right : SpreadsheetAlignment.Center;
            }

            double[] measurements = { 12.002, 11.995, 12.012, 11.998, 12.005, 12.018, 11.991, 12.003, 12.007, 12.010 };
            for (int i = 0; i < measurements.Length; i++)
            {
                int r = 2 + i;
                int sheetR = r + 1; // 3, 4, ... 12

                var cNo = ws.GetOrCreateCell(r, 0);
                cNo.RawValue = $"#{i + 1}";
                cNo.EvaluatedValue = cNo.RawValue;
                cNo.Alignment = SpreadsheetAlignment.Center;

                var cTgt = ws.GetOrCreateCell(r, 1);
                cTgt.RawValue = "12.000";
                cTgt.EvaluatedValue = 12.000;
                cTgt.FormatType = SpreadsheetFormatType.Number;
                cTgt.Alignment = SpreadsheetAlignment.Right;

                var cMeas = ws.GetOrCreateCell(r, 2);
                cMeas.RawValue = measurements[i].ToString();
                cMeas.EvaluatedValue = measurements[i];
                cMeas.FormatType = SpreadsheetFormatType.Number;
                cMeas.Alignment = SpreadsheetAlignment.Right;

                // Deviation: =C{sheetR}-B{sheetR}
                var cDev = ws.GetOrCreateCell(r, 3);
                cDev.RawValue = $"=C{sheetR}-B{sheetR}";
                cDev.FormatType = SpreadsheetFormatType.Number;
                cDev.Alignment = SpreadsheetAlignment.Right;

                // Pass/Fail: =IF(C{sheetR}>12.015, "FAIL", "PASS")
                var cRes = ws.GetOrCreateCell(r, 4);
                cRes.RawValue = $"=IF(C{sheetR}>12.015, \"FAIL\", \"PASS\")";
                cRes.Alignment = SpreadsheetAlignment.Center;
            }

            // Summary Statistics
            int statR = 2 + measurements.Length;
            var cAvgLabel = ws.GetOrCreateCell(statR, 1);
            cAvgLabel.RawValue = "MEAN AVERAGE:";
            cAvgLabel.EvaluatedValue = cAvgLabel.RawValue;
            cAvgLabel.IsBold = true;

            var cAvgVal = ws.GetOrCreateCell(statR, 2);
            cAvgVal.RawValue = "=AVERAGE(C3:C12)";
            cAvgVal.FormatType = SpreadsheetFormatType.Number;
            cAvgVal.IsBold = true;

            ws.RecalculateAllFormulas();

            return ws;
        }
    }
}
