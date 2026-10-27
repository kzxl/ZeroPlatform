using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Pdf
{
    /// <summary>
    /// Generates multi-page vector CAD schematics, SOP work instructions, and QC certificates.
    /// Enables instant offline demonstration, automated test validation, and vector fidelity benchmarks.
    /// </summary>
    public static class PdfSampleGenerator
    {
        public static PdfDocumentModel CreateIndustrialCadAndSopDocument()
        {
            var doc = new PdfDocumentModel
            {
                Title = "Industrial Automated Plant CAD & SOP Package",
                Author = "ZeroUI Engineering & Automation Systems",
                Subject = "Line 02 Extrusion & Electrical Schematics",
                Producer = "ZeroUI Vector PDF Engine v1.0",
                Version = "1.7"
            };

            doc.Pages.Add(CreateCadSchematicPage(0));
            doc.Pages.Add(CreateSopPage(1));
            doc.Pages.Add(CreateQcCertificatePage(2));

            // Document Outline Bookmarks
            var bm1 = new PdfBookmarkModel("1. Electrical CAD Schematic", 0);
            bm1.Children.Add(new PdfBookmarkModel("1.1 Power Incomer & Breakers", 0));
            bm1.Children.Add(new PdfBookmarkModel("1.2 3-Phase Extrusion Motor M1", 0));
            doc.Bookmarks.Add(bm1);

            var bm2 = new PdfBookmarkModel("2. Extrusion Standard Operating Procedure", 1);
            bm2.Children.Add(new PdfBookmarkModel("2.1 Pre-Start Safety Inspection", 1));
            bm2.Children.Add(new PdfBookmarkModel("2.2 Die Heating & Temperature Curves", 1));
            doc.Bookmarks.Add(bm2);

            var bm3 = new PdfBookmarkModel("3. Quality Compliance Certificate", 2);
            bm3.Children.Add(new PdfBookmarkModel("3.1 ISO 9001 Batch Inspection", 2));
            bm3.Children.Add(new PdfBookmarkModel("3.2 Quality Engineer Sign-off", 2));
            doc.Bookmarks.Add(bm3);

            return doc;
        }

        #region Page 1: Electrical CAD Schematic (Landscape: 842 x 595 pt)

        private static PdfPageModel CreateCadSchematicPage(int pageIndex)
        {
            var page = new PdfPageModel
            {
                PageIndex = pageIndex,
                Width = 841.89,  // A4 Landscape Width
                Height = 595.28  // A4 Landscape Height
            };

            // 1. Drawing Outer Border
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 25,
                Y = 25,
                Width = 791.89,
                Height = 545.28,
                StrokeColor = 0xFF0F172A,
                StrokeWidth = 2.0,
                IsStroked = true
            });

            // Inner margin border
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 35,
                Y = 35,
                Width = 771.89,
                Height = 525.28,
                StrokeColor = 0xFF334155,
                StrokeWidth = 1.0,
                IsStroked = true
            });

            // Grid coordinate markings (A..D / 1..8)
            string[] cols = { "1", "2", "3", "4", "5", "6", "7", "8" };
            double colW = 771.89 / 8.0;
            for (int i = 0; i < cols.Length; i++)
            {
                double cx = 35 + i * colW + colW / 2 - 4;
                AddText(page, cols[i], cx, 26, 9, 0xFF64748B, true);
                AddText(page, cols[i], cx, 563, 9, 0xFF64748B, true);
            }

            string[] rows = { "A", "B", "C", "D" };
            double rowH = 525.28 / 4.0;
            for (int i = 0; i < rows.Length; i++)
            {
                double ry = 35 + i * rowH + rowH / 2 - 4;
                AddText(page, rows[i], 27, ry, 9, 0xFF64748B, true);
                AddText(page, rows[i], 809, ry, 9, 0xFF64748B, true);
            }

            // 2. Title Block (Bottom Right)
            double tbX = 540, tbY = 460, tbW = 266.89, tbH = 100.28;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = tbX,
                Y = tbY,
                Width = tbW,
                Height = tbH,
                StrokeColor = 0xFF0F172A,
                FillColor = 0xFFF8FAFC,
                StrokeWidth = 1.5,
                IsStroked = true,
                IsFilled = true
            });

            // Title Block internal grids
            AddLine(page, tbX, tbY + 25, tbX + tbW, tbY + 25, 0xFF334155, 1.0);
            AddLine(page, tbX, tbY + 50, tbX + tbW, tbY + 50, 0xFF334155, 1.0);
            AddLine(page, tbX, tbY + 75, tbX + tbW, tbY + 75, 0xFF334155, 1.0);
            AddLine(page, tbX + 130, tbY + 50, tbX + 130, tbY + tbH, 0xFF334155, 1.0);

            AddText(page, "ZERO-UI AUTOMATION & SCADA SYSTEMS", tbX + 8, tbY + 7, 10, 0xFF0F172A, true);
            AddText(page, "TITLE: LINE 02 EXTRUDER 400V MOTOR DRIVE & I/O", tbX + 8, tbY + 32, 9, 0xFF1E293B, true);
            AddText(page, "DWG NO: EL-2026-EX02-01", tbX + 8, tbY + 58, 8.5, 0xFF475569);
            AddText(page, "REV: 04", tbX + 138, tbY + 58, 8.5, 0xFF475569, true);
            AddText(page, "DATE: 2026-09-07", tbX + 8, tbY + 83, 8.5, 0xFF475569);
            AddText(page, "PAGE: 01 / 03", tbX + 138, tbY + 83, 8.5, 0xFF475569, true);

            // 3. 3-Phase Main Power Bus (L1, L2, L3, PE)
            double busY = 70;
            AddText(page, "400V 3~ 50Hz MAIN FEEDER", 60, busY - 14, 9, 0xFF0284C7, true);
            AddLine(page, 50, busY, 780, busY, 0xFFDC2626, 1.5); // L1 Brown/Red
            AddText(page, "L1", 52, busY - 10, 8, 0xFFDC2626, true);

            AddLine(page, 50, busY + 14, 780, busY + 14, 0xFF2563EB, 1.5); // L2 Black/Blue
            AddText(page, "L2", 52, busY + 4, 8, 0xFF2563EB, true);

            AddLine(page, 50, busY + 28, 780, busY + 28, 0xFF65A30D, 1.5); // L3 Grey/Green
            AddText(page, "L3", 52, busY + 18, 8, 0xFF65A30D, true);

            AddDashedLine(page, 50, busY + 42, 780, busY + 42, 0xFF16A34A, 1.5, new float[] { 6, 4 }); // PE Protective Earth
            AddText(page, "PE", 52, busY + 32, 8, 0xFF16A34A, true);

            // 4. Circuit Breaker Q1 (Drops from Bus)
            double q1X = 140;
            AddLine(page, q1X, busY, q1X, 150, 0xFFDC2626, 1.2);
            AddLine(page, q1X + 16, busY + 14, q1X + 16, 150, 0xFF2563EB, 1.2);
            AddLine(page, q1X + 32, busY + 28, q1X + 32, 150, 0xFF65A30D, 1.2);

            // Breaker symbol box
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = q1X - 10,
                Y = 150,
                Width = 52,
                Height = 36,
                StrokeColor = 0xFF0F172A,
                FillColor = 0xFFFFFFFF,
                StrokeWidth = 1.2,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "-Q1", q1X + 8, 155, 9, 0xFF0F172A, true);
            AddText(page, "32A", q1X + 8, 168, 8, 0xFF475569);

            // 5. Variable Frequency Drive (VFD) Inverter Box
            double vfdX = 120, vfdY = 220, vfdW = 120, vfdH = 140;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = vfdX,
                Y = vfdY,
                Width = vfdW,
                Height = vfdH,
                StrokeColor = 0xFF0284C7,
                FillColor = 0xFFF0F9FF,
                StrokeWidth = 1.5,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "-U1 VFD INVERTER", vfdX + 12, vfdY + 8, 9.5, 0xFF0369A1, true);
            AddText(page, "PowerFlex 525 15kW", vfdX + 12, vfdY + 24, 8, 0xFF475569);
            AddText(page, "R / S / T (In)", vfdX + 14, vfdY + 44, 8, 0xFF64748B);
            AddText(page, "U / V / W (Out)", vfdX + 14, vfdY + 115, 8, 0xFF64748B);
            AddText(page, "Modbus RS-485", vfdX + 14, vfdY + 80, 8, 0xFFD97706, true);

            // Lines entering VFD
            AddLine(page, q1X, 186, q1X, vfdY, 0xFFDC2626, 1.2);
            AddLine(page, q1X + 16, 186, q1X + 16, vfdY, 0xFF2563EB, 1.2);
            AddLine(page, q1X + 32, 186, q1X + 32, vfdY, 0xFF65A30D, 1.2);

            // 6. 3-Phase Induction Motor (M1)
            double mX = 175, mY = 410, mR = 24;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Ellipse,
                X = mX - mR,
                Y = mY - mR,
                Width = mR * 2,
                Height = mR * 2,
                StrokeColor = 0xFF0F172A,
                FillColor = 0xFFF8FAFC,
                StrokeWidth = 1.5,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "M", mX - 6, mY - 9, 12, 0xFF0F172A, true);
            AddText(page, "3 ~", mX - 8, mY + 4, 8, 0xFF475569);
            AddText(page, "-M1 Extruder Screw", mX - 45, mY + 30, 8.5, 0xFF1E293B, true);
            AddText(page, "15 kW  1460 RPM", mX - 35, mY + 42, 8, 0xFF64748B);

            // Lines from VFD to Motor
            AddLine(page, q1X, vfdY + vfdH, q1X, mY - mR, 0xFFDC2626, 1.2);
            AddLine(page, q1X + 16, vfdY + vfdH, q1X + 16, mY - mR, 0xFF2563EB, 1.2);
            AddLine(page, q1X + 32, vfdY + vfdH, q1X + 32, mY - mR, 0xFF65A30D, 1.2);

            // 7. Modbus PLC Communication Loop
            double plcX = 340, plcY = 160, plcW = 160, plcH = 190;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = plcX,
                Y = plcY,
                Width = plcW,
                Height = plcH,
                StrokeColor = 0xFF7C3AED,
                FillColor = 0xFFFAF5FF,
                StrokeWidth = 1.5,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "-KF1 PLC CPU 1515F", plcX + 14, plcY + 8, 10, 0xFF6D28D9, true);
            AddText(page, "Fail-Safe Safety PLC", plcX + 14, plcY + 24, 8, 0xFF64748B);

            // I/O Terminals on PLC
            for (int i = 0; i < 6; i++)
            {
                double termY = plcY + 45 + i * 22;
                AddLine(page, plcX, termY, plcX + 16, termY, 0xFF7C3AED, 1.0);
                AddText(page, $"DI.{(i + 1):D2}", plcX + 20, termY - 4, 7.5, 0xFF475569);

                AddLine(page, plcX + plcW - 16, termY, plcX + plcW, termY, 0xFF7C3AED, 1.0);
                AddText(page, $"DQ.{(i + 1):D2}", plcX + plcW - 48, termY - 4, 7.5, 0xFF475569);
            }

            // 8. Emergency Stop Circuit (SIL-3 Relay)
            double esX = 340, esY = 380, esW = 160, esH = 90;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = esX,
                Y = esY,
                Width = esW,
                Height = esH,
                StrokeColor = 0xFFE11D48,
                FillColor = 0xFFFFF1F2,
                StrokeWidth = 1.5,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "-SF1 SAFETY INTERLOCK", esX + 12, esY + 8, 9.5, 0xFFBE123C, true);
            AddText(page, "Dual-Channel E-Stop & Light Curtain", esX + 12, esY + 24, 8, 0xFF881337);
            AddText(page, "Channel A: S11-S12 (Active)", esX + 12, esY + 44, 8, 0xFF16A34A, true);
            AddText(page, "Channel B: S21-S22 (Active)", esX + 12, esY + 60, 8, 0xFF16A34A, true);

            return page;
        }

        #endregion

        #region Page 2: Standard Operating Procedure (SOP) (Portrait: 595 x 842 pt)

        private static PdfPageModel CreateSopPage(int pageIndex)
        {
            var page = new PdfPageModel
            {
                PageIndex = pageIndex,
                Width = 595.28,
                Height = 841.89
            };

            // Header Banner
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 40,
                Y = 35,
                Width = 515.28,
                Height = 65,
                StrokeColor = 0xFF0284C7,
                FillColor = 0xFFF0F9FF,
                StrokeWidth = 1.2,
                IsStroked = true,
                IsFilled = true
            });

            AddText(page, "STANDARD OPERATING PROCEDURE (SOP)", 52, 45, 14, 0xFF0369A1, true);
            AddText(page, "Line 02 High-Precision Polymer Extruder — Startup, Calibration & Operation", 52, 65, 9.5, 0xFF334155);
            AddText(page, "DOC NO: SOP-PRD-2026-088  |  REV: 4.2  |  EFFECTIVE: 2026-09-01  |  PAGE: 02 / 03", 52, 82, 8, 0xFF64748B);

            // Mandatory Safety & PPE Notice Card
            double ppeY = 115;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 40,
                Y = ppeY,
                Width = 515.28,
                Height = 52,
                StrokeColor = 0xFFF59E0B,
                FillColor = 0xFFFFFBEB,
                StrokeWidth = 1.0,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "⚠ MANDATORY SAFETY PPE & HAZARD PRECAUTION", 52, ppeY + 8, 10, 0xFFB45309, true);
            AddText(page, "• Heat-resistant safety gloves (rated 250°C) must be worn during barrel purge.", 52, ppeY + 24, 8.5, 0xFF78350F);
            AddText(page, "• Eye protection safety goggles and optical laser-safe shields required at all times.", 52, ppeY + 36, 8.5, 0xFF78350F);

            // Section 1: Pre-Startup Inspection Checklist
            double chkY = 180;
            AddText(page, "1. PRE-STARTUP INSPECTION CHECKLIST", 40, chkY, 11.5, 0xFF0F172A, true);

            string[] steps =
            {
                "Verify cooling water chiller circulation loop pressure is between 2.8 and 3.5 Bar.",
                "Check raw polymer resin hopper level; ensure desiccant dryer dew point < -40°C.",
                "Inspect emergency stop pull-cords and optoelectronic safety light curtains.",
                "Preheat barrel heating zones to target setpoints according to Material Recipe Matrix.",
                "Zero calibrator on melt pressure transducer PT-1049 prior to screw rotation."
            };

            for (int i = 0; i < steps.Length; i++)
            {
                double sy = chkY + 22 + i * 26;
                // Checkbox
                page.Elements.Add(new PdfVectorElement
                {
                    ElementType = PdfVectorElementType.Rectangle,
                    X = 46,
                    Y = sy + 1,
                    Width = 13,
                    Height = 13,
                    StrokeColor = 0xFF10B981,
                    FillColor = 0xFFECFDF5,
                    StrokeWidth = 1.0,
                    IsStroked = true,
                    IsFilled = true
                });
                AddText(page, "✔", 48, sy + 1, 9, 0xFF059669, true);
                AddText(page, $"Step 1.{(i + 1)}: {steps[i]}", 68, sy + 2, 8.5, 0xFF334155);
            }

            // Section 2: Temperature Zone Parameter Matrix
            double tblY = 340;
            AddText(page, "2. BARREL THERMAL CONTROL ZONE SETPOINTS", 40, tblY, 11.5, 0xFF0F172A, true);

            // Table Header
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 40,
                Y = tblY + 18,
                Width = 515.28,
                Height = 22,
                StrokeColor = 0xFF334155,
                FillColor = 0xFFE2E8F0,
                StrokeWidth = 1.0,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "ZONE ID", 50, tblY + 23, 8.5, 0xFF0F172A, true);
            AddText(page, "ZONE FUNCTION", 120, tblY + 23, 8.5, 0xFF0F172A, true);
            AddText(page, "SETPOINT (°C)", 240, tblY + 23, 8.5, 0xFF0F172A, true);
            AddText(page, "TOLERANCE", 350, tblY + 23, 8.5, 0xFF0F172A, true);
            AddText(page, "STATUS", 460, tblY + 23, 8.5, 0xFF0F172A, true);

            // Table Rows
            string[,] rows =
            {
                { "Zone 1", "Feed Throat Pre-Heat", "180.0 °C", "± 3.0 °C", "NORMAL" },
                { "Zone 2", "Compression Transition", "210.0 °C", "± 2.5 °C", "NORMAL" },
                { "Zone 3", "Metering & Homogenization", "225.0 °C", "± 2.0 °C", "NORMAL" },
                { "Zone 4", "Die Head & Adapter Flange", "230.0 °C", "± 1.5 °C", "NORMAL" }
            };

            for (int r = 0; r < 4; r++)
            {
                double ry = tblY + 40 + r * 22;
                page.Elements.Add(new PdfVectorElement
                {
                    ElementType = PdfVectorElementType.Rectangle,
                    X = 40,
                    Y = ry,
                    Width = 515.28,
                    Height = 22,
                    StrokeColor = 0xFFCBD5E1,
                    FillColor = (r % 2 == 1) ? 0xFFF8FAFC : 0xFFFFFFFF,
                    StrokeWidth = 0.5,
                    IsStroked = true,
                    IsFilled = true
                });
                AddText(page, rows[r, 0], 50, ry + 5, 8.5, 0xFF334155);
                AddText(page, rows[r, 1], 120, ry + 5, 8.5, 0xFF334155);
                AddText(page, rows[r, 2], 240, ry + 5, 8.5, 0xFF0284C7, true);
                AddText(page, rows[r, 3], 350, ry + 5, 8.5, 0xFF64748B);
                AddText(page, rows[r, 4], 460, ry + 5, 8.5, 0xFF10B981, true);
            }

            // Section 3: Emergency Shutdown & Quality Check
            double sec3Y = 460;
            AddText(page, "3. EMERGENCY SHUTDOWN & INTERLOCK TRIP PROCEDURE", 40, sec3Y, 11.5, 0xFF0F172A, true);
            AddText(page, "In event of thermal runaway (Zone Temp > 255°C) or melt pressure spike (PT-1049 > 280 Bar):", 40, sec3Y + 18, 8.5, 0xFF475569);
            AddText(page, "1. Press immediate Red Mushroom E-Stop button on control console.", 55, sec3Y + 34, 8.5, 0xFFDC2626, true);
            AddText(page, "2. High-speed hydraulic safety valve EV-104 opens automatically to dump line pressure.", 55, sec3Y + 48, 8.5, 0xFF334155);
            AddText(page, "3. Log malfunction event code into MES Work Order Station before reset.", 55, sec3Y + 62, 8.5, 0xFF334155);

            // Operator Sign-Off Box at bottom
            double sigY = 680;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 40,
                Y = sigY,
                Width = 515.28,
                Height = 85,
                StrokeColor = 0xFF94A3B8,
                FillColor = 0xFFF8FAFC,
                StrokeWidth = 1.0,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "OPERATOR AUTHORIZATION & SHIFT HANDOVER SIGN-OFF", 50, sigY + 8, 9, 0xFF0F172A, true);
            AddText(page, "Primary Line Lead:  Nguyen Van An (ID: OP-4029)", 50, sigY + 28, 8.5, 0xFF334155);
            AddText(page, "QC Supervisor:      Tran Thi Bich (ID: QC-8192)", 50, sigY + 44, 8.5, 0xFF334155);
            AddText(page, "Shift Handover Time: 2026-09-07 14:00:00 (All interlocks OK)", 50, sigY + 60, 8.5, 0xFF10B981, true);

            return page;
        }

        #endregion

        #region Page 3: QC & Calibration Certificate (Portrait: 595 x 842 pt)

        private static PdfPageModel CreateQcCertificatePage(int pageIndex)
        {
            var page = new PdfPageModel
            {
                PageIndex = pageIndex,
                Width = 595.28,
                Height = 841.89
            };

            // Certificate Outer Gold Border
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 35,
                Y = 35,
                Width = 525.28,
                Height = 771.89,
                StrokeColor = 0xFFD97706, // Amber/Gold
                StrokeWidth = 2.0,
                IsStroked = true
            });

            // Certificate Inner Border
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 43,
                Y = 43,
                Width = 509.28,
                Height = 755.89,
                StrokeColor = 0xFFF59E0B,
                StrokeWidth = 0.8,
                IsStroked = true
            });

            // Header
            AddText(page, "CERTIFICATE OF CONFORMANCE & CALIBRATION", 110, 65, 14.5, 0xFF0F172A, true);
            AddText(page, "ISO 9001:2015 & ISO/IEC 17025 ACCREDITED LABORATORY", 130, 85, 9, 0xFF64748B, true);
            AddText(page, "CERTIFICATE NO: CAL-2026-9042-QC  |  NIST TRACEABLE", 145, 100, 8.5, 0xFFD97706, true);

            // Unit Under Test Information Box
            double uutY = 125;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 60,
                Y = uutY,
                Width = 475.28,
                Height = 85,
                StrokeColor = 0xFFCBD5E1,
                FillColor = 0xFFF8FAFC,
                StrokeWidth = 1.0,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "INSTRUMENT UNDER TEST (EUT) SPECIFICATION", 72, uutY + 8, 9.5, 0xFF1E293B, true);
            AddText(page, "Asset Description:   Pressure Transmitter (Piezoresistive)", 72, uutY + 26, 8.5, 0xFF334155);
            AddText(page, "Tag Identifier:      PT-1049  |  Serial No: SN-2026-9042B", 72, uutY + 40, 8.5, 0xFF334155);
            AddText(page, "Calibrated Range:    0.0 to 300.0 Bar  |  Output: 4.0 - 20.0 mA", 72, uutY + 54, 8.5, 0xFF334155);
            AddText(page, "Calibration Date:    2026-09-07  |  Next Due: 2027-09-07", 72, uutY + 68, 8.5, 0xFF0284C7, true);

            // Multi-Point Calibration Table
            double calY = 230;
            AddText(page, "NIST-TRACEABLE MULTI-POINT PRESSURE CALIBRATION DATA", 60, calY, 10, 0xFF0F172A, true);

            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 60,
                Y = calY + 16,
                Width = 475.28,
                Height = 22,
                StrokeColor = 0xFF334155,
                FillColor = 0xFFE2E8F0,
                StrokeWidth = 1.0,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "NOMINAL (Bar)", 72, calY + 21, 8.5, 0xFF0F172A, true);
            AddText(page, "EXPECTED (mA)", 160, calY + 21, 8.5, 0xFF0F172A, true);
            AddText(page, "MEASURED (mA)", 260, calY + 21, 8.5, 0xFF0F172A, true);
            AddText(page, "DEV ERROR (%)", 360, calY + 21, 8.5, 0xFF0F172A, true);
            AddText(page, "PASS / FAIL", 455, calY + 21, 8.5, 0xFF0F172A, true);

            string[,] calData =
            {
                { "0.0 Bar (0%)",    "4.000 mA",  "4.001 mA",  "+0.006%", "PASSED" },
                { "75.0 Bar (25%)",  "8.000 mA",  "8.002 mA",  "+0.012%", "PASSED" },
                { "150.0 Bar (50%)", "12.000 mA", "11.998 mA", "-0.010%", "PASSED" },
                { "225.0 Bar (75%)", "16.000 mA", "16.003 mA", "+0.018%", "PASSED" },
                { "300.0 Bar (100%)","20.000 mA", "20.002 mA", "+0.010%", "PASSED" }
            };

            for (int r = 0; r < 5; r++)
            {
                double ry = calY + 38 + r * 20;
                page.Elements.Add(new PdfVectorElement
                {
                    ElementType = PdfVectorElementType.Rectangle,
                    X = 60,
                    Y = ry,
                    Width = 475.28,
                    Height = 20,
                    StrokeColor = 0xFFE2E8F0,
                    FillColor = (r % 2 == 1) ? 0xFFF8FAFC : 0xFFFFFFFF,
                    StrokeWidth = 0.5,
                    IsStroked = true,
                    IsFilled = true
                });
                AddText(page, calData[r, 0], 72, ry + 4, 8.5, 0xFF334155);
                AddText(page, calData[r, 1], 160, ry + 4, 8.5, 0xFF64748B);
                AddText(page, calData[r, 2], 260, ry + 4, 8.5, 0xFF0284C7, true);
                AddText(page, calData[r, 3], 360, ry + 4, 8.5, 0xFF10B981);
                AddText(page, calData[r, 4], 455, ry + 4, 8.5, 0xFF10B981, true);
            }

            // Statistical Six Sigma Box
            double spcY = 370;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Rectangle,
                X = 60,
                Y = spcY,
                Width = 475.28,
                Height = 58,
                StrokeColor = 0xFF10B981,
                FillColor = 0xFFECFDF5,
                StrokeWidth = 1.0,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "STATISTICAL PROCESS CAPABILITY & TOLERANCE STATEMENT", 72, spcY + 8, 9.5, 0xFF065F46, true);
            AddText(page, "Process Capability Index: Cpk = 1.84 (Highly Capable, Six Sigma Tier 1)", 72, spcY + 24, 8.5, 0xFF047857, true);
            AddText(page, "Total Expanded Uncertainty: U = ±0.035% of Full Scale Span (Coverage Factor k=2, 95% Confidence)", 72, spcY + 38, 8.0, 0xFF065F46);

            // Official Electronic Verification Seal
            double sealX = 390, sealY = 620, sealR = 48;
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Ellipse,
                X = sealX - sealR,
                Y = sealY - sealR,
                Width = sealR * 2,
                Height = sealR * 2,
                StrokeColor = 0xFF0369A1,
                FillColor = 0xFFF0F9FF,
                StrokeWidth = 2.0,
                IsStroked = true,
                IsFilled = true
            });
            AddText(page, "ZERO-UI QA", sealX - 26, sealY - 14, 9, 0xFF0369A1, true);
            AddText(page, "CALIBRATED", sealX - 28, sealY, 8.5, 0xFF10B981, true);
            AddText(page, "2026-09-07", sealX - 24, sealY + 12, 8, 0xFF64748B);

            AddText(page, "ELECTRONICALLY SIGNED BY CHIEF METROLOGIST", 60, 680, 8.5, 0xFF64748B, true);
            AddText(page, "Dr. Vo Hoang Phong, Lead Instrumentation Specialist", 60, 695, 9, 0xFF0F172A, true);

            return page;
        }

        #endregion

        #region Vector Drawing Helpers

        private static void AddLine(PdfPageModel page, double x1, double y1, double x2, double y2, uint color, double width)
        {
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Line,
                X = x1,
                Y = y1,
                X2 = x2,
                Y2 = y2,
                StrokeColor = color,
                StrokeWidth = width,
                IsStroked = true
            });
        }

        private static void AddDashedLine(PdfPageModel page, double x1, double y1, double x2, double y2, uint color, double width, float[] pattern)
        {
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Line,
                X = x1,
                Y = y1,
                X2 = x2,
                Y2 = y2,
                StrokeColor = color,
                StrokeWidth = width,
                IsStroked = true,
                DashPattern = pattern
            });
        }

        private static void AddText(PdfPageModel page, string text, double x, double y, double fontSize, uint color, bool bold = false)
        {
            double width = text.Length * (fontSize * 0.58);
            page.Elements.Add(new PdfVectorElement
            {
                ElementType = PdfVectorElementType.Text,
                X = x,
                Y = y,
                Width = width,
                Height = fontSize * 1.3,
                Text = text,
                FontSize = fontSize,
                TextColor = color,
                IsBold = bold
            });
            page.TextRuns.Add(new PdfTextRun(text, x, y, width, fontSize * 1.3, fontSize, "Segoe UI", color));
        }

        #endregion
    }
}
