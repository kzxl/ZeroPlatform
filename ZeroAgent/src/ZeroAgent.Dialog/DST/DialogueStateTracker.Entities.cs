using System;
using System.Text.RegularExpressions;

namespace ZeroAgent.Dialog.DST
{
    /// <summary>
    /// Partial implementation of DialogueStateTracker focusing on entity extraction,
    /// slot validation, and multi-turn session progression.
    /// </summary>
    public sealed partial class DialogueStateTracker
    {
        /// <summary>
        /// Advances the dialogue state: extracts entities/slots from input and updates the session.
        /// </summary>
        public void AdvanceSession(DialogueSession session, string resolvedText, DialogueIntent? detectedIntent)
        {
            // 1. If session was waiting for a specific slot, check if user provided it directly
            if (!string.IsNullOrEmpty(session.PendingRequiredSlot))
            {
                string pendingSlot = session.PendingRequiredSlot!;
                string extracted = ExtractSlotValue(resolvedText, pendingSlot);
                if (!string.IsNullOrEmpty(extracted))
                {
                    session.SetSlot(pendingSlot, extracted);
                    session.PendingRequiredSlot = null;
                }
                else
                {
                    // If short answer (e.g. single word/token), assume it is the direct slot value
                    string clean = resolvedText.Trim();
                    if (clean.Length > 0 && clean.Length < 30 && !clean.Contains(" "))
                    {
                        session.SetSlot(pendingSlot, clean);
                        session.PendingRequiredSlot = null;
                    }
                }
            }

            // 2. If new intent detected with high confidence, set as current intent
            if (detectedIntent != null && (session.CurrentIntent == null || session.State == SessionState.Completed || session.State == SessionState.Idle))
            {
                session.CurrentIntent = detectedIntent;
            }
            else if (detectedIntent == null && (session.State == SessionState.Completed || session.State == SessionState.Idle))
            {
                session.CurrentIntent = null;
            }

            // 3. Extract common entity slots from resolved text
            ExtractAllEntities(resolvedText, session);

            // 4. Verify required slots for the active intent
            if (session.CurrentIntent != null)
            {
                session.PendingRequiredSlot = null;
                bool allSatisfied = true;
                foreach (var req in session.CurrentIntent.RequiredSlots)
                {
                    if (!session.HasSlot(req))
                    {
                        session.PendingRequiredSlot = req;
                        session.State = SessionState.CollectingSlots;
                        allSatisfied = false;
                        break;
                    }
                }

                if (allSatisfied)
                {
                    session.State = SessionState.ReadyToExecute;
                }
            }
        }

        private static void ExtractAllEntities(string text, DialogueSession session)
        {
            // Machine ID pattern: CNC-01, PRESS-03, F-01, ROBOT-ARM-01, etc.
            var machineMatch = Regex.Match(text, @"\b(CNC-\d+|PRESS-\d+|ROBOT-ARM-\d+|CONVEYOR-\d+|F-\d+|D-\d+)\b", RegexOptions.IgnoreCase);
            if (machineMatch.Success)
            {
                session.SetSlot("machine_id", machineMatch.Value.ToUpperInvariant());
            }

            // Metric pattern: nhiệt độ, áp suất, độ rung, lỗi, công suất
            if (Regex.IsMatch(text, @"\b(nhiệt độ|nhiệt|nóng|temperature|temp)\b", RegexOptions.IgnoreCase))
            {
                session.SetSlot("metric", "temperature");
            }
            else if (Regex.IsMatch(text, @"\b(áp suất|áp lực|pressure|psi)\b", RegexOptions.IgnoreCase))
            {
                session.SetSlot("metric", "pressure");
            }
            else if (Regex.IsMatch(text, @"\b(rung|độ rung|vibration|bearing)\b", RegexOptions.IgnoreCase))
            {
                session.SetSlot("metric", "vibration");
            }
            else if (Regex.IsMatch(text, @"\b(lỗi|khuyết tật|defect|error|fault)\b", RegexOptions.IgnoreCase))
            {
                session.SetSlot("metric", "defects");
            }

            // Target value pattern: e.g. "xuống 80%", "bằng 100", "value: 42"
            var valueMatch = Regex.Match(text, @"\b(\d+(\.\d+)?)\s*(%|c|f|psi|bar|rpm)?\b", RegexOptions.IgnoreCase);
            if (valueMatch.Success && !machineMatch.Success) // Avoid capturing machine digits as value
            {
                session.SetSlot("value", valueMatch.Groups[1].Value);
            }

            // Area pattern
            var areaMatch = Regex.Match(text, @"\b(xưởng đúc|xưởng ép|xưởng cnc|dây chuyền 1|dây chuyền 2)\b", RegexOptions.IgnoreCase);
            if (areaMatch.Success)
            {
                session.SetSlot("area", areaMatch.Value.ToLowerInvariant());
            }

            // Table name pattern
            var tableMatch = Regex.Match(text, @"\b(factory_machines|SampleData|production_lines|telemetry_logs)\b", RegexOptions.IgnoreCase);
            if (tableMatch.Success)
            {
                session.SetSlot("tableName", tableMatch.Value);
            }

            // === Standard ERP Entity Extractors ===
            // 1. SKU / Material Code
            var skuMatch = Regex.Match(text, @"\b(SKU-[A-Za-z0-9_\-]+|VT-[A-Za-z0-9_\-]+|NVL-[A-Za-z0-9_\-]+|SP-[A-Za-z0-9_\-]+|TP-[A-Za-z0-9_\-]+)\b", RegexOptions.IgnoreCase);
            if (skuMatch.Success)
            {
                session.SetSlot("item_code", skuMatch.Value.ToUpperInvariant());
                session.SetSlot("sku", skuMatch.Value.ToUpperInvariant());
            }

            // 2. Warehouse ID / Name
            var whMatch = Regex.Match(text, @"\b(KHO-[A-Za-z0-9_\-]+|WH-[A-Za-z0-9_\-]+|kho tổng|kho nguyên liệu|kho thành phẩm|kho vật tư|kho phụ liệu|kho hà nội|kho hcm)\b", RegexOptions.IgnoreCase);
            if (whMatch.Success)
            {
                session.SetSlot("warehouse_id", whMatch.Value.Trim());
            }

            // 3. Purchase Order (PO)
            var poMatch = Regex.Match(text, @"\b(PO-[A-Za-z0-9_\-]+|PO\d{3,})\b", RegexOptions.IgnoreCase);
            if (poMatch.Success)
            {
                session.SetSlot("po_number", poMatch.Value.ToUpperInvariant());
            }

            // 4. Sales Order (SO)
            var soMatch = Regex.Match(text, @"\b(SO-[A-Za-z0-9_\-]+|SO\d{3,})\b", RegexOptions.IgnoreCase);
            if (soMatch.Success)
            {
                session.SetSlot("so_number", soMatch.Value.ToUpperInvariant());
            }

            // 5. Manufacturing Order / Work Order (MO / WO / LSX)
            var moMatch = Regex.Match(text, @"\b(MO-[A-Za-z0-9_\-]+|WO-[A-Za-z0-9_\-]+|LSX-[A-Za-z0-9_\-]+)\b", RegexOptions.IgnoreCase);
            if (moMatch.Success)
            {
                session.SetSlot("mo_number", moMatch.Value.ToUpperInvariant());
            }

            // 6. Customer / Supplier (NCC / KH / Đối tác)
            var partnerMatch = Regex.Match(text, @"\b(KH-[A-Za-z0-9_\-]+|NCC-[A-Za-z0-9_\-]+|khách hàng\s+([A-Za-z0-9_\-]+)|nhà cung cấp\s+([A-Za-z0-9_\-]+)|công ty\s+([A-Za-z0-9_\-]+))\b", RegexOptions.IgnoreCase);
            if (partnerMatch.Success)
            {
                string partnerVal = partnerMatch.Value.Trim();
                if (partnerVal.StartsWith("khách hàng", StringComparison.OrdinalIgnoreCase) || partnerVal.StartsWith("KH-", StringComparison.OrdinalIgnoreCase))
                {
                    session.SetSlot("customer_id", partnerVal);
                }
                else if (partnerVal.StartsWith("nhà cung cấp", StringComparison.OrdinalIgnoreCase) || partnerVal.StartsWith("NCC-", StringComparison.OrdinalIgnoreCase))
                {
                    session.SetSlot("supplier_id", partnerVal);
                }
                else
                {
                    session.SetSlot("partner_name", partnerVal);
                }
            }

            // 7. Quantity with unit: e.g. 500 cái, 20 kg, 100 tấn
            var qtyMatch = Regex.Match(text, @"\b(\d+)\s*(cái|chiếc|bộ|kg|tấn|thùng|hộp|cuộn|mét|m|pcs)\b", RegexOptions.IgnoreCase);
            if (qtyMatch.Success)
            {
                session.SetSlot("quantity", qtyMatch.Groups[1].Value);
                session.SetSlot("unit", qtyMatch.Groups[2].Value.ToLowerInvariant());
            }
        }

        private static string ExtractSlotValue(string text, string slotName)
        {
            if (slotName.Equals("machine_id", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(text, @"\b(CNC-\d+|PRESS-\d+|ROBOT-ARM-\d+|CONVEYOR-\d+|F-\d+|D-\d+)\b", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value.ToUpperInvariant();
                // Match words like "máy 1", "lò 2"
                var mAlt = Regex.Match(text, @"(máy|lò|băng chuyền)\s*([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                if (mAlt.Success) return mAlt.Groups[2].Value.ToUpperInvariant();
            }
            else if (slotName.Equals("tableName", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(text, @"\b(factory_machines|SampleData|production_lines|telemetry_logs|[a-zA-Z0-9_]+)\b", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value;
            }
            else if (slotName.Equals("item_code", StringComparison.OrdinalIgnoreCase) || slotName.Equals("sku", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(text, @"\b(SKU-[A-Za-z0-9_\-]+|VT-[A-Za-z0-9_\-]+|NVL-[A-Za-z0-9_\-]+|SP-[A-Za-z0-9_\-]+|TP-[A-Za-z0-9_\-]+)\b", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value.ToUpperInvariant();
            }
            else if (slotName.Equals("warehouse_id", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(text, @"\b(KHO-[A-Za-z0-9_\-]+|WH-[A-Za-z0-9_\-]+|kho tổng|kho nguyên liệu|kho thành phẩm|kho vật tư|kho phụ liệu|kho hà nội|kho hcm)\b", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value.Trim();
            }
            else if (slotName.Equals("so_number", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(text, @"\b(SO-[A-Za-z0-9_\-]+|SO\d{3,})\b", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value.ToUpperInvariant();
            }
            else if (slotName.Equals("po_number", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(text, @"\b(PO-[A-Za-z0-9_\-]+|PO\d{3,})\b", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value.ToUpperInvariant();
            }
            else if (slotName.Equals("mo_number", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(text, @"\b(MO-[A-Za-z0-9_\-]+|WO-[A-Za-z0-9_\-]+|LSX-[A-Za-z0-9_\-]+)\b", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value.ToUpperInvariant();
            }
            return string.Empty;
        }
    }
}
