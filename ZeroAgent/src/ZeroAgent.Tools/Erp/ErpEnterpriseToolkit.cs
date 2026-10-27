using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tools.Erp
{
    public static class ErpEnterpriseToolkit
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        private static string ToJson(object data) => JsonSerializer.Serialize(data, _jsonOptions);

        public static AgentToolRegistry RegisterErpToolkit(this AgentToolRegistry registry)
        {
            RegisterAll(registry);
            return registry;
        }

        public static void RegisterAll(AgentToolRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // ==========================================
            // 1. WAREHOUSE MANAGEMENT SYSTEM (WMS)
            // ==========================================
            registry.Register(new AgentTool(
                "check_inventory",
                "Queries inventory stock levels, available quantity, and reservations for a given SKU and warehouse.",
                "sku: string, warehouse: string",
                ExecuteCheckInventoryAsync));

            registry.Register(new AgentTool(
                "create_stock_transfer",
                "Creates an internal stock transfer order between two warehouses.",
                "sku: string, from_warehouse: string, to_warehouse: string, quantity: int",
                ExecuteCreateStockTransferAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "report_low_stock",
                "Generates a list of items whose stock levels have fallen below safety thresholds.",
                "warehouse: string, threshold: int",
                ExecuteReportLowStockAsync));

            registry.Register(new AgentTool(
                "get_stock_valuation",
                "Retrieves total monetary inventory valuation and unit asset value for a warehouse.",
                "warehouse: string",
                ExecuteGetStockValuationAsync));

            registry.Register(new AgentTool(
                "stock_taking_audit",
                "Reconciles system book inventory against physical inventory count to report discrepancies.",
                "warehouse: string, sku: string",
                ExecuteStockTakingAuditAsync));

            registry.Register(new AgentTool(
                "lookup_bin_location",
                "Finds the precise warehouse bin, rack, and shelf location for an inventory SKU.",
                "sku: string, warehouse: string",
                ExecuteLookupBinLocationAsync));

            registry.Register(new AgentTool(
                "check_fefo_expiry",
                "Audits lots expiring soonest sorted by First Expired, First Out (FEFO) principle.",
                "sku: string, days_ahead: int",
                ExecuteCheckFefoExpiryAsync));

            registry.Register(new AgentTool(
                "scan_barcode_inbound",
                "Validates inbound barcode/QR scan and verifies batch receipt into warehouse.",
                "barcode: string, warehouse: string, batch: string",
                ExecuteScanBarcodeInboundAsync));

            registry.Register(new AgentTool(
                "record_pallet_movement",
                "Logs physical movement of pallet between warehouse bin locations.",
                "pallet_id: string, from_bin: string, to_bin: string",
                ExecuteRecordPalletMovementAsync));

            registry.Register(new AgentTool(
                "optimize_warehouse_layout",
                "Recommends slotting rearrangement to place fast-moving SKUs closest to packing stations.",
                "warehouse: string, category: string",
                ExecuteOptimizeWarehouseLayoutAsync));

            // ==========================================
            // 2. MANUFACTURING EXECUTION SYSTEM (MES)
            // ==========================================
            registry.Register(new AgentTool(
                "get_work_order_progress",
                "Queries real-time production status, completion percentage, and active workstation for a work order.",
                "work_order: string",
                ExecuteGetWorkOrderProgressAsync));

            registry.Register(new AgentTool(
                "check_bom_availability",
                "Calculates bill of materials (BOM) explosion and determines raw material shortages.",
                "product_sku: string, quantity: int",
                ExecuteCheckBomAvailabilityAsync));

            registry.Register(new AgentTool(
                "calculate_oee",
                "Computes Overall Equipment Effectiveness (Availability, Performance, Quality, OEE %).",
                "line_id: string, date: string",
                ExecuteCalculateOeeAsync));

            registry.Register(new AgentTool(
                "report_machine_breakdown",
                "Logs an emergency or critical equipment breakdown and dispatches maintenance engineers.",
                "machine_id: string, issue_type: string, urgency: string",
                ExecuteReportMachineBreakdownAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "schedule_maintenance",
                "Schedules routine or corrective maintenance for plant machinery.",
                "machine_id: string, maintenance_type: string, date: string",
                ExecuteScheduleMaintenanceAsync));

            registry.Register(new AgentTool(
                "schedule_tpm_preventive",
                "Schedules Total Productive Maintenance (TPM) autonomous inspection checks on manufacturing lines.",
                "line_id: string, machine_id: string, check_type: string",
                ExecuteScheduleTpmPreventiveAsync));

            registry.Register(new AgentTool(
                "analyze_scrap_defect_cause",
                "Performs root cause failure analysis (RCFA) for scrap and defective parts in a work order.",
                "work_order: string, line_id: string",
                ExecuteAnalyzeScrapDefectCauseAsync));

            registry.Register(new AgentTool(
                "record_shift_handover",
                "Logs production shift handover report including output tally, downtime, and open safety items.",
                "shift: string, line_id: string, operator: string, notes: string",
                ExecuteRecordShiftHandoverAsync));

            registry.Register(new AgentTool(
                "monitor_energy_consumption",
                "Queries electrical power and thermal energy consumption for industrial production lines.",
                "line_id: string, period: string",
                ExecuteMonitorEnergyConsumptionAsync));

            registry.Register(new AgentTool(
                "reschedule_production_bottleneck",
                "Rebalances and reschedules work orders across parallel workstations to relieve factory bottlenecks.",
                "work_order: string, bottleneck_station: string",
                ExecuteRescheduleProductionBottleneckAsync,
                requiresApproval: true));

            // ==========================================
            // 3. SALES & CRM
            // ==========================================
            registry.Register(new AgentTool(
                "calculate_sales_discount",
                "Calculates eligible discount rates, promotional policy rebates, and net quote pricing.",
                "customer_id: string, order_value: double, product_tier: string",
                ExecuteCalculateSalesDiscountAsync));

            registry.Register(new AgentTool(
                "query_order_status",
                "Queries real-time shipping, fulfillment status, and payment progress for a sales order.",
                "order_id: string",
                ExecuteQueryOrderStatusAsync));

            registry.Register(new AgentTool(
                "create_quotation",
                "Drafts an official commercial sales quotation for a prospective client.",
                "customer_id: string, items: string, valid_days: int",
                ExecuteCreateQuotationAsync));

            registry.Register(new AgentTool(
                "check_credit_limit",
                "Queries customer credit ceiling, outstanding receivables, and remaining credit margin.",
                "customer_id: string",
                ExecuteCheckCreditLimitAsync));

            registry.Register(new AgentTool(
                "get_top_selling_products",
                "Retrieves top-performing revenue and unit sales products over a specified timeframe.",
                "period: string, limit: int",
                ExecuteGetTopSellingProductsAsync));

            // ==========================================
            // 4. R&D & FORMULATION
            // ==========================================
            registry.Register(new AgentTool(
                "query_formula_revision",
                "Retrieves chemical formulation specifications, revision history, and approval signatures.",
                "formula_code: string",
                ExecuteQueryFormulaRevisionAsync));

            registry.Register(new AgentTool(
                "compare_bom_versions",
                "Computes structural and constituent differences between two BOM formula revisions.",
                "product_sku: string, v1: string, v2: string",
                ExecuteCompareBomVersionsAsync));

            registry.Register(new AgentTool(
                "estimate_batch_cost",
                "Simulates standard raw material and conversion cost for a pilot production batch.",
                "formula_code: string, batch_size: double",
                ExecuteEstimateBatchCostAsync));

            registry.Register(new AgentTool(
                "validate_raw_material_ratio",
                "Validates chemical ingredient percentages against safety limits and regulatory standards.",
                "formula_code: string, active_ingredient: string",
                ExecuteValidateRawMaterialRatioAsync));

            // ==========================================
            // 5. QUALITY ASSURANCE & HANDOVER
            // ==========================================
            registry.Register(new AgentTool(
                "create_inspection_record",
                "Logs an official Quality Control (QC) inspection test result with measured laboratory metrics.",
                "batch_number: string, inspection_type: string, result: string",
                ExecuteCreateInspectionRecordAsync));

            registry.Register(new AgentTool(
                "flag_quarantine_batch",
                "Places a non-compliant or suspect production batch under quarantine lockdown.",
                "batch_number: string, reason: string",
                ExecuteFlagQuarantineBatchAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "approve_quality_certificate",
                "Approves and digitally signs a Certificate of Analysis (COA) for shipping release.",
                "co_number: string, batch_number: string",
                ExecuteApproveQualityCertificateAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "verify_handover_checklist",
                "Validates engineering sign-off, safety compliance, and handover protocols for equipment or products.",
                "order_id: string, stage: string",
                ExecuteVerifyHandoverChecklistAsync));

            // ==========================================
            // 6. FINANCE & ACCOUNTING
            // ==========================================
            registry.Register(new AgentTool(
                "get_account_balance",
                "Queries general ledger chart of accounts debit, credit, and net balance.",
                "account_code: string",
                ExecuteGetAccountBalanceAsync));

            registry.Register(new AgentTool(
                "reconcile_payable_debt",
                "Reconciles accounts payable ledger against supplier billing invoices.",
                "supplier_id: string, period: string",
                ExecuteReconcilePayableDebtAsync));

            registry.Register(new AgentTool(
                "issue_vat_invoice",
                "Issues an official electronic tax invoice (Hóa đơn điện tử VAT) according to tax authority schema.",
                "order_id: string, tax_code: string",
                ExecuteIssueVatInvoiceAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "get_financial_summary",
                "Aggregates enterprise revenue, COGS, operating expenses, and net profit margins.",
                "period: string",
                ExecuteGetFinancialSummaryAsync));

            registry.Register(new AgentTool(
                "create_payment_voucher",
                "Generates an accounting disbursement voucher (Phiếu chi) for authorized expenses.",
                "recipient: string, amount: double, reason: string",
                ExecuteCreatePaymentVoucherAsync,
                requiresApproval: true));

            // ==========================================
            // 7. PURCHASING & SUPPLY CHAIN
            // ==========================================
            registry.Register(new AgentTool(
                "create_purchase_requisition",
                "Drafts an internal purchase requisition (PR) for replenishment of materials.",
                "department: string, item_sku: string, quantity: int",
                ExecuteCreatePurchaseRequisitionAsync));

            registry.Register(new AgentTool(
                "compare_vendor_quotes",
                "Compares pricing, delivery lead time, and payment terms across multiple supplier quotations.",
                "rfq_code: string",
                ExecuteCompareVendorQuotesAsync));

            registry.Register(new AgentTool(
                "create_purchase_order",
                "Issues a binding commercial purchase order (PO) to an approved vendor.",
                "vendor_id: string, items: string, total_amount: double",
                ExecuteCreatePurchaseOrderAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "track_purchase_delivery",
                "Tracks shipping dispatch, customs status, and estimated warehouse delivery date for a PO.",
                "po_number: string",
                ExecuteTrackPurchaseDeliveryAsync));

            registry.Register(new AgentTool(
                "evaluate_supplier_performance",
                "Calculates vendor quality score, on-time delivery (OTD) rate, and defect PPM metric.",
                "supplier_id: string",
                ExecuteEvaluateSupplierPerformanceAsync));

            // ==========================================
            // 8. ENTERPRISE SUBSYSTEMS & OPERATIONS (ERP / MES / WMS / SCM)
            // ==========================================
            registry.Register(new AgentTool(
                "erp_inventory_lot_balance_query",
                "[Enterprise ERP] Queries lot balance, available quantity, reservations, and expiry across Material, SemiProduct, or Product lots.",
                "lot_no: string, warehouse_code: string, level: string",
                ExecuteErpInventoryLotBalanceQueryAsync));

            registry.Register(new AgentTool(
                "erp_inventory_stock_in_create",
                "[Enterprise ERP] Creates a Material Stock In ticket for manufacturing warehouses from suppliers or internal production.",
                "warehouse_code: string, in_type: string, supplier_code: string, po_number: string, items: string",
                ExecuteErpInventoryStockInCreateAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "erp_inventory_stock_transfer",
                "[Enterprise ERP] Creates an internal stock transfer between enterprise warehouses.",
                "from_warehouse: string, to_warehouse: string, item_code: string, quantity: double, reason: string",
                ExecuteErpInventoryStockTransferAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "erp_production_plan_query",
                "[Enterprise ERP MES] Queries manufacturing plan and execution progress by process stage.",
                "process_stage: string, line_code: string, plan_date: string",
                ExecuteErpProductionPlanQueryAsync));

            registry.Register(new AgentTool(
                "erp_production_diary_record",
                "[Enterprise ERP MES] Records production shift diary (output, scrap quantity, downtime minutes, operator notes).",
                "shift_name: string, process_stage: string, operator: string, completed_qty: double, scrap_qty: double, downtime_minutes: int, notes: string",
                ExecuteErpProductionDiaryRecordAsync));

            registry.Register(new AgentTool(
                "erp_maintenance_report_incident",
                "[Enterprise ERP Maintenance] Reports urgent machine stoppage breakdown incident and dispatches mechanical/electrical maintenance crew.",
                "machine_code: string, issue_description: string, priority: string, line_code: string",
                ExecuteErpMaintenanceReportIncidentAsync,
                requiresApproval: true));

            registry.Register(new AgentTool(
                "erp_sales_order_query",
                "[Enterprise ERP Sales] Queries sales orders by sales channel, agency, and order code.",
                "order_code: string, agency: string",
                ExecuteErpSalesOrderQueryAsync));

            registry.Register(new AgentTool(
                "erp_sales_packing_audit",
                "[Enterprise ERP Sales] Audits sales order carton packing log, delays, and approval trail.",
                "packing_id: string, order_code: string",
                ExecuteErpSalesPackingAuditAsync));

            registry.Register(new AgentTool(
                "erp_rd_bom_query",
                "[Enterprise ERP R&D] Queries technical Bill of Materials (BOM) or PartList specifications and release status.",
                "product_code: string, status: string, bom_type: string",
                ExecuteErpRdBomQueryAsync));

            registry.Register(new AgentTool(
                "erp_approval_execute",
                "[00.ERP Common] Executes approval or revoke approval workflow for enterprise tickets with role-based checks.",
                "feature_code: string, ticket_id: string, action_type: string, approver_role: string, reason: string",
                ExecuteErpApprovalExecuteAsync,
                requiresApproval: true));
        }

        #region Argument Parsing Helpers

        private static Dictionary<string, string> ParseArgs(string raw)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return result;

            string trimmed = raw.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        result[prop.Name] = prop.Value.ToString();
                    }
                    return result;
                }
                catch
                {
                    // Fall back to regex parser if JSON parsing fails
                }
            }

            // Parse key='value' or key="value" or key=value pairs
            var matches = Regex.Matches(trimmed, @"([a-zA-Z0-9_\-]+)\s*[:=]\s*(?:['""]([^'""]*)['""]|([^,\s'""]+))");
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                string val = !string.IsNullOrEmpty(match.Groups[2].Value) ? match.Groups[2].Value : match.Groups[3].Value;
                result[key] = val;
            }

            return result;
        }

        private static string GetStr(Dictionary<string, string> dict, string key, string fallback = "") =>
            dict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : fallback;

        private static int GetInt(Dictionary<string, string> dict, string key, int fallback = 0) =>
            dict.TryGetValue(key, out var val) && int.TryParse(val, out var n) ? n : fallback;

        private static double GetDouble(Dictionary<string, string> dict, string key, double fallback = 0.0) =>
            dict.TryGetValue(key, out var val) && double.TryParse(val, out var n) ? n : fallback;

        #endregion

        #region Tool Implementations

        // 1. Warehouse
        private static Task<string> ExecuteCheckInventoryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string sku = GetStr(p, "sku", "SKU-PL01");
            string wh = GetStr(p, "warehouse", "Kho Bình Dương");
            int total = 1250;
            int reserved = 150;
            int available = total - reserved;

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                sku = sku,
                warehouse = wh,
                total_qty = total,
                available_qty = available,
                reserved_qty = reserved,
                uom = "đơn vị",
                status = "InStock"
            }));
        }

        private static Task<string> ExecuteCreateStockTransferAsync(string arg)
        {
            var p = ParseArgs(arg);
            string sku = GetStr(p, "sku", "SKU-PL01");
            string fromWh = GetStr(p, "from_warehouse", GetStr(p, "from", "Kho Bình Dương"));
            string toWh = GetStr(p, "to_warehouse", GetStr(p, "to", "Kho Tân Bình"));
            int qty = GetInt(p, "quantity", GetInt(p, "qty", 100));
            string trfId = $"TRF-2026-{DateTime.UtcNow:MMddHHmm}";

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                transfer_id = trfId,
                sku = sku,
                from_warehouse = fromWh,
                to_warehouse = toWh,
                quantity = qty,
                status = "Approved",
                created_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }

        private static Task<string> ExecuteReportLowStockAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wh = GetStr(p, "warehouse", "Kho Bình Dương");
            int threshold = GetInt(p, "threshold", 100);

            var items = new[]
            {
                new { sku = "SKU-RD04", name = "Chất kết dính Polyurethane R-4", stock = 32, safety_stock = threshold, unit = "can" },
                new { sku = "SKU-CT08", name = "Cảm biến quang tiệm cận Omron", stock = 8, safety_stock = 20, unit = "cái" }
            };

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                warehouse = wh,
                threshold = threshold,
                low_stock_count = items.Length,
                critical_items = items
            }));
        }

        private static Task<string> ExecuteGetStockValuationAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wh = GetStr(p, "warehouse", "Kho Tổng");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                warehouse = wh,
                total_skus = 450,
                total_units = 184500,
                valuation_vnd = 4850000000.0,
                valuation_formatted = "4.850.000.000 VNĐ",
                audit_date = DateTime.UtcNow.ToString("yyyy-MM-dd")
            }));
        }

        private static Task<string> ExecuteStockTakingAuditAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wh = GetStr(p, "warehouse", "Kho Bình Dương");
            string sku = GetStr(p, "sku", "SKU-PL01");

            int bookQty = 1250;
            int actualQty = 1248;
            int delta = actualQty - bookQty;

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                warehouse = wh,
                sku = sku,
                system_book_qty = bookQty,
                actual_counted_qty = actualQty,
                discrepancy = delta,
                status = delta == 0 ? "Matched" : "MinorShortage"
            }));
        }

        private static Task<string> ExecuteLookupBinLocationAsync(string arg)
        {
            var p = ParseArgs(arg);
            string sku = GetStr(p, "sku", "SKU-PL01");
            string wh = GetStr(p, "warehouse", "Kho Bình Dương");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                sku = sku,
                warehouse = wh,
                zone = "Zone-A (Raw Materials)",
                aisle = "Kệ A-03",
                bin_code = "A03-R02-B14",
                shelf_level = 2,
                capacity_utilized_percent = 78.5
            }));
        }

        private static Task<string> ExecuteCheckFefoExpiryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string sku = GetStr(p, "sku", "SKU-PL01");
            int days = GetInt(p, "days_ahead", 30);

            var expiringLots = new[]
            {
                new { lot = "LOT-2026-01-A", quantity = 180, expiry_date = DateTime.UtcNow.AddDays(12).ToString("yyyy-MM-dd"), days_remaining = 12 },
                new { lot = "LOT-2026-02-C", quantity = 320, expiry_date = DateTime.UtcNow.AddDays(25).ToString("yyyy-MM-dd"), days_remaining = 25 }
            };

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                sku = sku,
                days_ahead = days,
                rule = "FEFO",
                expiring_lots = expiringLots
            }));
        }

        private static Task<string> ExecuteScanBarcodeInboundAsync(string arg)
        {
            var p = ParseArgs(arg);
            string barcode = GetStr(p, "barcode", "8938500123456");
            string wh = GetStr(p, "warehouse", "Kho Bình Dương");
            string batch = GetStr(p, "batch", "B-2026-0315");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                barcode = barcode,
                warehouse = wh,
                batch_number = batch,
                product_name = "Nhựa hạt Polypropylene PP-500",
                scan_status = "Verified",
                quarantine_check = "Passed",
                assigned_bin = "A01-R01-B02"
            }));
        }

        private static Task<string> ExecuteRecordPalletMovementAsync(string arg)
        {
            var p = ParseArgs(arg);
            string palletId = GetStr(p, "pallet_id", "PLT-8821");
            string fromBin = GetStr(p, "from_bin", "A01-R01-B02");
            string toBin = GetStr(p, "to_bin", "STAGE-OUT-01");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                pallet_id = palletId,
                from_bin = fromBin,
                to_bin = toBin,
                carrier = "Forklift #02",
                moved_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                status = "Success"
            }));
        }

        private static Task<string> ExecuteOptimizeWarehouseLayoutAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wh = GetStr(p, "warehouse", "Kho Bình Dương");
            string cat = GetStr(p, "category", "FastMoving");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                warehouse = wh,
                category = cat,
                strategy = "ABC Slotting Optimization",
                recommendation = "Di chuyển 5 mã hàng thuộc nhóm A (tần suất xuất kho > 50 lần/ngày) từ dãy Kệ D-08 về Kệ A-01 gần cửa xuất",
                expected_travel_reduction_percent = 28.4
            }));
        }

        // 2. Production
        private static Task<string> ExecuteGetWorkOrderProgressAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wo = GetStr(p, "work_order", "WO-2026-0901");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                work_order = wo,
                target_quantity = 5000,
                completed_quantity = 3850,
                scrap_quantity = 42,
                progress_percent = 77.0,
                current_station = "Công đoạn Đóng gói tự động (Line #2)",
                status = "Running"
            }));
        }

        private static Task<string> ExecuteCheckBomAvailabilityAsync(string arg)
        {
            var p = ParseArgs(arg);
            string sku = GetStr(p, "product_sku", "FG-001");
            int qty = GetInt(p, "quantity", 1000);

            var items = new[]
            {
                new { material = "Nhựa PP-500", required = 1000.0, available = 2400.0, sufficient = true },
                new { material = "Chất tạo màu Xanh Industrial", required = 25.0, available = 40.0, sufficient = true },
                new { material = "Thùng carton 5 lớp", required = 100.0, available = 85.0, sufficient = false }
            };

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                product_sku = sku,
                requested_quantity = qty,
                bom_sufficient = false,
                shortage_detected = true,
                materials = items
            }));
        }

        private static Task<string> ExecuteCalculateOeeAsync(string arg)
        {
            var p = ParseArgs(arg);
            string lineId = GetStr(p, "line_id", "LINE-01");
            string date = GetStr(p, "date", DateTime.UtcNow.ToString("yyyy-MM-dd"));

            double availability = 91.5;
            double performance = 94.0;
            double quality = 98.8;
            double oee = Math.Round((availability * performance * quality) / 10000.0, 2);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                line_id = lineId,
                date = date,
                availability_percent = availability,
                performance_percent = performance,
                quality_percent = quality,
                oee_percent = oee,
                world_class_benchmark = 85.0,
                evaluation = oee >= 85.0 ? "World Class" : "Standard"
            }));
        }

        private static Task<string> ExecuteReportMachineBreakdownAsync(string arg)
        {
            var p = ParseArgs(arg);
            string machineId = GetStr(p, "machine_id", "CNC-03");
            string issue = GetStr(p, "issue_type", "Overheating / Spindle vibration");
            string urgency = GetStr(p, "urgency", "High");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                incident_id = $"INC-2026-{DateTime.UtcNow:MMddHHmm}",
                machine_id = machineId,
                reported_issue = issue,
                urgency = urgency,
                dispatched_team = "Tổ Bảo trì Cơ điện Phân xưởng 1",
                estimated_arrival_minutes = 15,
                line_status = "Halted - Safety Interlock Active"
            }));
        }

        private static Task<string> ExecuteScheduleMaintenanceAsync(string arg)
        {
            var p = ParseArgs(arg);
            string machineId = GetStr(p, "machine_id", "CNC-03");
            string type = GetStr(p, "maintenance_type", "Preventive 500h");
            string date = GetStr(p, "date", DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"));

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                schedule_id = $"PM-2026-{DateTime.UtcNow:MMdd}",
                machine_id = machineId,
                maintenance_type = type,
                scheduled_date = date,
                technician = "Kỹ thuật viên Nguyễn Văn A",
                allocated_downtime_hours = 2.5
            }));
        }

        private static Task<string> ExecuteScheduleTpmPreventiveAsync(string arg)
        {
            var p = ParseArgs(arg);
            string lineId = GetStr(p, "line_id", "LINE-02");
            string machineId = GetStr(p, "machine_id", "EXTRUDER-01");
            string checkType = GetStr(p, "check_type", "TPM Level 1 Autonomous Maintenance");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                tpm_ticket = $"TPM-2026-{DateTime.UtcNow:MMddHH}",
                line_id = lineId,
                machine_id = machineId,
                check_type = checkType,
                tasks = new[] { "Kiểm tra mức dầu bôi trơn hộp số", "Vệ sinh lưới lọc khí làm mát", "Đo độ rơ dây curoa truyền động" },
                operator_signoff_required = true
            }));
        }

        private static Task<string> ExecuteAnalyzeScrapDefectCauseAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wo = GetStr(p, "work_order", "WO-2026-0901");
            string lineId = GetStr(p, "line_id", "LINE-01");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                work_order = wo,
                line_id = lineId,
                defect_rate_percent = 2.4,
                root_cause = "Dao cắt bị mòn dẫn đến ba via mép sản phẩm vượt dung sai cho phép (0.2mm)",
                corrective_action = "Thay lưỡi dao cắt khuôn định hình và hiệu chuẩn lại cữ chặn",
                action_status = "PendingTechnicianApproval"
            }));
        }

        private static Task<string> ExecuteRecordShiftHandoverAsync(string arg)
        {
            var p = ParseArgs(arg);
            string shift = GetStr(p, "shift", "Ca 1 (06:00 - 14:00)");
            string lineId = GetStr(p, "line_id", "LINE-01");
            string op = GetStr(p, "operator", "Trần Văn B");
            string notes = GetStr(p, "notes", "Sản lượng đạt 98% kế hoạch, máy ép chạy ổn định, đã bàn giao đủ phụ tùng ca 2.");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                log_id = $"LOG-SHIFT-{DateTime.UtcNow:yyyyMMdd-HH}",
                shift = shift,
                line_id = lineId,
                logged_by = op,
                safety_incident_count = 0,
                notes = notes,
                handover_status = "Signed"
            }));
        }

        private static Task<string> ExecuteMonitorEnergyConsumptionAsync(string arg)
        {
            var p = ParseArgs(arg);
            string lineId = GetStr(p, "line_id", "LINE-01");
            string period = GetStr(p, "period", "Hôm nay");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                line_id = lineId,
                period = period,
                total_kwh = 1420.5,
                peak_power_kw = 185.2,
                cost_estimate_vnd = 3125000,
                efficiency_rating = "A+ (Eco Optimized)"
            }));
        }

        private static Task<string> ExecuteRescheduleProductionBottleneckAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wo = GetStr(p, "work_order", "WO-2026-0901");
            string station = GetStr(p, "bottleneck_station", "Máy phay CNC-02");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                work_order = wo,
                bottleneck_station = station,
                mitigation = "Phân bổ 40% sản lượng còn lại sang máy CNC-04 dự phòng đang rảnh tải",
                lead_time_recovery_hours = 6.0,
                rebalanced_status = "Scheduled"
            }));
        }

        // 3. Sales
        private static Task<string> ExecuteCalculateSalesDiscountAsync(string arg)
        {
            var p = ParseArgs(arg);
            string cust = GetStr(p, "customer_id", "KH-VIP-01");
            double val = GetDouble(p, "order_value", 50000000);
            string tier = GetStr(p, "product_tier", "Standard");

            double discPercent = val >= 100000000 ? 12.0 : (val >= 30000000 ? 8.0 : 3.0);
            double discAmount = val * discPercent / 100.0;
            double finalAmount = val - discAmount;

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                customer_id = cust,
                original_value = val,
                discount_percent = discPercent,
                discount_amount = discAmount,
                final_value = finalAmount,
                policy = "Chính sách ưu đãi khách hàng VIP quý 3"
            }));
        }

        private static Task<string> ExecuteQueryOrderStatusAsync(string arg)
        {
            var p = ParseArgs(arg);
            string orderId = GetStr(p, "order_id", "SO-2026-0042");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                order_id = orderId,
                status = "Đang vận chuyển",
                carrier = "Viettel Post Logistic",
                tracking_number = "VP89234812VN",
                estimated_delivery = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
                payment_status = "Đã thanh toán 100%"
            }));
        }

        private static Task<string> ExecuteCreateQuotationAsync(string arg)
        {
            var p = ParseArgs(arg);
            string cust = GetStr(p, "customer_id", "KH-092");
            int days = GetInt(p, "valid_days", 15);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                quotation_code = $"BG-2026-{DateTime.UtcNow:MMddHH}",
                customer_id = cust,
                valid_until = DateTime.UtcNow.AddDays(days).ToString("yyyy-MM-dd"),
                terms = "Thanh toán 30% đặt cọc, 70% khi nhận hàng",
                status = "DraftCreated"
            }));
        }

        private static Task<string> ExecuteCheckCreditLimitAsync(string arg)
        {
            var p = ParseArgs(arg);
            string cust = GetStr(p, "customer_id", "KH-092");

            double limit = 500000000.0;
            double outstanding = 185000000.0;
            double remaining = limit - outstanding;

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                customer_id = cust,
                credit_limit_vnd = limit,
                outstanding_debt_vnd = outstanding,
                available_credit_margin = remaining,
                credit_status = remaining > 0 ? "ApprovedForOrder" : "CreditCeilingExceeded"
            }));
        }

        private static Task<string> ExecuteGetTopSellingProductsAsync(string arg)
        {
            var p = ParseArgs(arg);
            string period = GetStr(p, "period", "Tháng này");
            int limit = GetInt(p, "limit", 3);

            var topItems = new[]
            {
                new { rank = 1, sku = "SKU-PL01", name = "Nhựa tấm Polycarbonate 5mm", revenue_vnd = 1250000000.0, units = 4500 },
                new { rank = 2, sku = "SKU-AL02", name = "Thanh nhôm định hình 40x40", revenue_vnd = 890000000.0, units = 2200 },
                new { rank = 3, sku = "SKU-CT08", name = "Cảm biến quang tiệm cận Omron", revenue_vnd = 420000000.0, units = 1100 }
            };

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                period = period,
                top_products = topItems
            }));
        }

        // 4. R&D
        private static Task<string> ExecuteQueryFormulaRevisionAsync(string arg)
        {
            var p = ParseArgs(arg);
            string formula = GetStr(p, "formula_code", "CT-SMR-02");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                formula_code = formula,
                active_revision = "Rev-3.2",
                title = "Công thức Sơn Epoxy kháng hóa chất cao cấp",
                approved_by = "Giám đốc R&D TS. Lê Hoàng Nam",
                effective_date = "2026-01-15",
                status = "ProductionReady"
            }));
        }

        private static Task<string> ExecuteCompareBomVersionsAsync(string arg)
        {
            var p = ParseArgs(arg);
            string sku = GetStr(p, "product_sku", "FG-001");
            string v1 = GetStr(p, "v1", "v1.0");
            string v2 = GetStr(p, "v2", "v2.0");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                product_sku = sku,
                base_version = v1,
                target_version = v2,
                changes = new[]
                {
                    "Tăng tỷ lệ phụ gia chống UV từ 1.2% lên 1.8%",
                    "Thay thế dung môi hữu cơ TOL bằng dung môi gốc nước ECO-SOLV",
                    "Giảm chi phí thành phẩm trên đơn vị: -4.5%"
                }
            }));
        }

        private static Task<string> ExecuteEstimateBatchCostAsync(string arg)
        {
            var p = ParseArgs(arg);
            string formula = GetStr(p, "formula_code", "CT-SMR-02");
            double batchSize = GetDouble(p, "batch_size", 1000.0);

            double rawMaterialCost = batchSize * 45000.0;
            double laborCost = batchSize * 5000.0;
            double overheadCost = batchSize * 3500.0;
            double totalCost = rawMaterialCost + laborCost + overheadCost;

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                formula_code = formula,
                batch_size_kg = batchSize,
                raw_material_cost_vnd = rawMaterialCost,
                labor_cost_vnd = laborCost,
                overhead_cost_vnd = overheadCost,
                total_cost_vnd = totalCost,
                unit_cost_per_kg_vnd = totalCost / batchSize
            }));
        }

        private static Task<string> ExecuteValidateRawMaterialRatioAsync(string arg)
        {
            var p = ParseArgs(arg);
            string formula = GetStr(p, "formula_code", "CT-SMR-02");
            string active = GetStr(p, "active_ingredient", "Hardener Polyamine");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                formula_code = formula,
                active_ingredient = active,
                current_ratio_percent = 18.5,
                standard_range = "15.0% - 20.0%",
                compliance = "Compliant",
                notes = "Nằm trong ngưỡng an toàn tiêu chuẩn ISO 9001:2015"
            }));
        }

        // 5. QC / QA
        private static Task<string> ExecuteCreateInspectionRecordAsync(string arg)
        {
            var p = ParseArgs(arg);
            string batch = GetStr(p, "batch_number", "B-2026-0315");
            string type = GetStr(p, "inspection_type", "IQC Đạt chuẩn đầu vào");
            string result = GetStr(p, "result", "Pass");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                inspection_id = $"QC-2026-{DateTime.UtcNow:MMddHHmm}",
                batch_number = batch,
                inspection_type = type,
                result = result,
                tested_parameters = new { purity = "99.8%", moisture = "0.04%", visual = "Không cặn bẩn" },
                inspector = "QC Tester Nguyễn Thị Mai"
            }));
        }

        private static Task<string> ExecuteFlagQuarantineBatchAsync(string arg)
        {
            var p = ParseArgs(arg);
            string batch = GetStr(p, "batch_number", "B-2026-0315");
            string reason = GetStr(p, "reason", "Độ ẩm vượt quá giới hạn 0.1%");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                quarantine_id = $"QRT-2026-{DateTime.UtcNow:MMddHH}",
                batch_number = batch,
                quarantine_reason = reason,
                allocation_locked = true,
                storage_location = "Khu cách ly Kho B",
                action_required = "Kiểm tra tái nghiệm phòng thí nghiệm R&D"
            }));
        }

        private static Task<string> ExecuteApproveQualityCertificateAsync(string arg)
        {
            var p = ParseArgs(arg);
            string co = GetStr(p, "co_number", "COA-2026-0881");
            string batch = GetStr(p, "batch_number", "B-2026-0315");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                coa_number = co,
                batch_number = batch,
                status = "DigitallySignedAndApproved",
                released_for_shipping = true,
                sign_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }

        private static Task<string> ExecuteVerifyHandoverChecklistAsync(string arg)
        {
            var p = ParseArgs(arg);
            string orderId = GetStr(p, "order_id", "SO-2026-0042");
            string stage = GetStr(p, "stage", "Final Handover");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                order_id = orderId,
                stage = stage,
                all_checks_passed = true,
                checklist = new[]
                {
                    "Chứng chỉ chất lượng COA đầy đủ",
                    "Đóng gói niêm phong pallet nguyên vẹn",
                    "Phiếu xuất kho có chữ ký thủ kho và tài xế"
                },
                handover_status = "Completed"
            }));
        }

        // 6. Finance
        private static Task<string> ExecuteGetAccountBalanceAsync(string arg)
        {
            var p = ParseArgs(arg);
            string acc = GetStr(p, "account_code", "1121"); // Ngân hàng Vietcombank

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                account_code = acc,
                account_name = "Tiền gửi ngân hàng VND (Vietcombank)",
                debit_balance_vnd = 24500000000.0,
                credit_balance_vnd = 0.0,
                net_balance_vnd = 24500000000.0,
                net_formatted = "24.500.000.000 VNĐ"
            }));
        }

        private static Task<string> ExecuteReconcilePayableDebtAsync(string arg)
        {
            var p = ParseArgs(arg);
            string supp = GetStr(p, "supplier_id", "NCC-HOACHAT-01");
            string period = GetStr(p, "period", "Quý 2/2026");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                supplier_id = supp,
                period = period,
                total_invoiced_vnd = 1800000000.0,
                total_paid_vnd = 1200000000.0,
                outstanding_payable_vnd = 600000000.0,
                payment_due_date = DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd")
            }));
        }

        private static Task<string> ExecuteIssueVatInvoiceAsync(string arg)
        {
            var p = ParseArgs(arg);
            string orderId = GetStr(p, "order_id", "SO-2026-0042");
            string taxCode = GetStr(p, "tax_code", "0314889922");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                invoice_number = $"HD-2026-{DateTime.UtcNow:MMddHHmm}",
                tax_code = taxCode,
                order_id = orderId,
                vat_rate = "10%",
                invoice_status = "SignedAndTransmittedToTaxAuthority",
                xml_hash = Guid.NewGuid().ToString("N").Substring(0, 16)
            }));
        }

        private static Task<string> ExecuteGetFinancialSummaryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string period = GetStr(p, "period", "Tháng này");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                period = period,
                gross_revenue_vnd = 18500000000.0,
                cogs_vnd = 12200000000.0,
                gross_profit_vnd = 6300000000.0,
                operating_expense_vnd = 2100000000.0,
                net_profit_vnd = 4200000000.0,
                net_margin_percent = 22.7
            }));
        }

        private static Task<string> ExecuteCreatePaymentVoucherAsync(string arg)
        {
            var p = ParseArgs(arg);
            string recipient = GetStr(p, "recipient", "Công ty Hóa chất Petro");
            double amount = GetDouble(p, "amount", 150000000.0);
            string reason = GetStr(p, "reason", "Thanh toán đợt 2 tiền nhập phụ gia");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                voucher_code = $"PC-2026-{DateTime.UtcNow:MMddHHmm}",
                recipient = recipient,
                amount_vnd = amount,
                reason = reason,
                status = "ApprovedByChiefAccountant"
            }));
        }

        // 7. Purchasing
        private static Task<string> ExecuteCreatePurchaseRequisitionAsync(string arg)
        {
            var p = ParseArgs(arg);
            string dept = GetStr(p, "department", "Phân xưởng Sản xuất 1");
            string sku = GetStr(p, "item_sku", "SKU-PL01");
            int qty = GetInt(p, "quantity", 500);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                pr_code = $"PR-2026-{DateTime.UtcNow:MMddHH}",
                requesting_department = dept,
                item_sku = sku,
                requested_quantity = qty,
                status = "PendingApproval"
            }));
        }

        private static Task<string> ExecuteCompareVendorQuotesAsync(string arg)
        {
            var p = ParseArgs(arg);
            string rfq = GetStr(p, "rfq_code", "RFQ-2026-081");

            var quotes = new[]
            {
                new { vendor = "Nhà cung cấp A", unit_price_vnd = 42000.0, lead_time_days = 3, rating = 4.8 },
                new { vendor = "Nhà cung cấp B", unit_price_vnd = 40500.0, lead_time_days = 7, rating = 4.3 }
            };

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                rfq_code = rfq,
                compared_quotes = quotes,
                recommendation = "Chọn NCC A do thời gian giao hàng nhanh (3 ngày) và độ tin cậy chất lượng 4.8/5.0"
            }));
        }

        private static Task<string> ExecuteCreatePurchaseOrderAsync(string arg)
        {
            var p = ParseArgs(arg);
            string vendor = GetStr(p, "vendor_id", "NCC-A");
            string items = GetStr(p, "items", "SKU-PL01 x 500");
            double amount = GetDouble(p, "total_amount", 21000000.0);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                po_number = $"PO-2026-{DateTime.UtcNow:MMddHHmm}",
                vendor_id = vendor,
                items_summary = items,
                total_amount_vnd = amount,
                po_status = "IssuedToSupplier"
            }));
        }

        private static Task<string> ExecuteTrackPurchaseDeliveryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string po = GetStr(p, "po_number", "PO-2026-0035");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                po_number = po,
                shipping_status = "Đang thông quan tại Cảng Cát Lái",
                estimated_arrival = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"),
                customs_status = "Cleared",
                carrier = "Evergreen Shipping Line"
            }));
        }

        private static Task<string> ExecuteEvaluateSupplierPerformanceAsync(string arg)
        {
            var p = ParseArgs(arg);
            string vendor = GetStr(p, "supplier_id", "NCC-A");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                supplier_id = vendor,
                on_time_delivery_rate_percent = 97.5,
                quality_pass_rate_percent = 99.2,
                defect_ppm = 120,
                overall_score = 4.85,
                tier = "Tier-1 Strategic Partner"
            }));
        }

        // 8. Enterprise Subsystem Implementations (ERP / MES / WMS / SCM)
        private static Task<string> ExecuteErpInventoryLotBalanceQueryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string lot = GetStr(p, "lot_no", "LOT-2026-PP01");
            string wh = GetStr(p, "warehouse_code", "WH-MAT-01");
            string level = GetStr(p, "level", "Material");

            return Task.FromResult(ToJson(new
            {
                lot_no = lot,
                item_code = "MAT-PP-500",
                item_name = "Polypropylene Raw Granules",
                warehouse_code = wh,
                warehouse_name = wh == "WH-MAT-01" ? "Manufacturing Raw Material Warehouse" : wh,
                level = level,
                quantity_on_hand = 4500.0,
                quantity_allocated = 1200.0,
                quantity_available = 3300.0,
                uom = "kg",
                expiry_date = "2027-06-30",
                status = "Active"
            }));
        }

        private static Task<string> ExecuteErpInventoryStockInCreateAsync(string arg)
        {
            var p = ParseArgs(arg);
            string wh = GetStr(p, "warehouse_code", "WH-MAT-01");
            string inType = GetStr(p, "in_type", "RawMaterial");
            string supp = GetStr(p, "supplier_code", "SUPP-001");
            string po = GetStr(p, "po_number", "PO-2026-0812");
            string ticketId = $"PN-MAT-2026-{DateTime.UtcNow:MMddHHmm}";

            return Task.FromResult(ToJson(new
            {
                ticket_id = ticketId,
                warehouse_code = wh,
                in_type = inType,
                supplier_code = supp,
                po_number = po,
                status = "Draft",
                message = "Lưu thông tin thành công",
                created_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }

        private static Task<string> ExecuteErpInventoryStockTransferAsync(string arg)
        {
            var p = ParseArgs(arg);
            string fromWh = GetStr(p, "from_warehouse", "WH-MAT-01");
            string toWh = GetStr(p, "to_warehouse", "WH-MAT-02");
            string item = GetStr(p, "item_code", "MAT-PP-500");
            double qty = GetDouble(p, "quantity", 500.0);
            string transferId = $"PX-TRF-2026-{DateTime.UtcNow:MMddHHmm}";

            return Task.FromResult(ToJson(new
            {
                transfer_ticket_id = transferId,
                from_warehouse = fromWh,
                to_warehouse = toWh,
                item_code = item,
                quantity = qty,
                status = "PendingDispatch",
                message = $"Đã chuyển phiếu sang {toWh}"
            }));
        }

        private static Task<string> ExecuteErpProductionPlanQueryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string stage = GetStr(p, "process_stage", "Extrusion");
            string line = GetStr(p, "line_code", "EXT-01");
            string planDate = GetStr(p, "plan_date", "today");

            return Task.FromResult(ToJson(new
            {
                process_stage = stage,
                line_code = line,
                plan_date = planDate,
                shift = "Shift 1",
                work_order = "WO-2026-0412",
                product_name = stage == "Extrusion" ? "PP Roll Sheet 0.6mm" : "PET Thermoformed Tray 150ml",
                planned_qty = stage == "Extrusion" ? 12000.0 : 25000.0,
                completed_qty = stage == "Extrusion" ? 9600.0 : 18500.0,
                progress_percent = stage == "Extrusion" ? 80.0 : 74.0,
                scrap_qty = stage == "Extrusion" ? 140.0 : 210.0,
                status = "Running"
            }));
        }

        private static Task<string> ExecuteErpProductionDiaryRecordAsync(string arg)
        {
            var p = ParseArgs(arg);
            string shift = GetStr(p, "shift_name", "Shift 1");
            string stage = GetStr(p, "process_stage", "Extrusion");
            string op = GetStr(p, "operator", "Operator 1");
            double completed = GetDouble(p, "completed_qty", 9600.0);
            double scrap = GetDouble(p, "scrap_qty", 140.0);
            int downtime = GetInt(p, "downtime_minutes", 15);
            string notes = GetStr(p, "notes", "Normal operation without incidents");

            return Task.FromResult(ToJson(new
            {
                diary_id = $"LOG-PROD-{DateTime.UtcNow:yyyyMMdd-HHmm}",
                shift_name = shift,
                process_stage = stage,
                operator_name = op,
                completed_qty = completed,
                scrap_qty = scrap,
                downtime_minutes = downtime,
                notes = notes,
                status = "Recorded",
                message = "Lưu thông tin thành công"
            }));
        }

        private static Task<string> ExecuteErpMaintenanceReportIncidentAsync(string arg)
        {
            var p = ParseArgs(arg);
            string machine = GetStr(p, "machine_code", "TF-03");
            string issue = GetStr(p, "issue_description", "Heating chamber temperature fluctuation");
            string priority = GetStr(p, "priority", "HIGH");
            string line = GetStr(p, "line_code", "Line-02");

            return Task.FromResult(ToJson(new
            {
                incident_id = $"INC-MAINT-2026-{DateTime.UtcNow:MMddHHmm}",
                machine_code = machine,
                line_code = line,
                issue = issue,
                priority = priority,
                status = "Dispatched",
                assigned_team = "Mechanical & Electrical Maintenance Crew 2",
                dispatched_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }

        private static Task<string> ExecuteErpSalesOrderQueryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string orderCode = GetStr(p, "order_code", "SO-2026-0881");
            string agency = GetStr(p, "agency", "HQ");

            return Task.FromResult(ToJson(new
            {
                order_code = orderCode,
                agency = agency,
                customer_name = "Global Packaging Solutions Ltd.",
                total_amount_vnd = 850000000.0,
                order_status = "Approved",
                packing_id = "PACK-2026-0312",
                packing_status = "LogisticsApproved",
                delivery_date = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd")
            }));
        }

        private static Task<string> ExecuteErpSalesPackingAuditAsync(string arg)
        {
            var p = ParseArgs(arg);
            string packingId = GetStr(p, "packing_id", "PACK-2026-0312");
            string orderCode = GetStr(p, "order_code", "SO-2026-0881");

            return Task.FromResult(ToJson(new
            {
                packing_id = packingId,
                order_code = orderCode,
                team_leader_approved = true,
                logistics_approved = true,
                qa_staff_approved = true,
                is_delay = false,
                carton_package_calculated = true,
                status = "ReadyForShipment"
            }));
        }

        private static Task<string> ExecuteErpRdBomQueryAsync(string arg)
        {
            var p = ParseArgs(arg);
            string prod = GetStr(p, "product_code", "PP-LID-120");
            string status = GetStr(p, "status", "Release");

            var materials = new[]
            {
                new { item_code = "MAT-PP-500", name = "PP Granules Virgin", standard_qty = 0.015, unit = "kg" },
                new { item_code = "MAT-MB-WHITE", name = "White Masterbatch", standard_qty = 0.0003, unit = "kg" }
            };

            return Task.FromResult(ToJson(new
            {
                product_code = prod,
                product_name = "PP Container Lid 120mm",
                bom_code = "BOM-PPLID-v2.1",
                status = status,
                approval_user = "R&D Lead",
                effective_date = "2026-08-15",
                materials = materials
            }));
        }

        private static Task<string> ExecuteErpApprovalExecuteAsync(string arg)
        {
            var p = ParseArgs(arg);
            string feature = GetStr(p, "feature_code", "ERP.Inventory.Material.StockIn");
            string ticket = GetStr(p, "ticket_id", "PN-NVL-2026-0042");
            string action = GetStr(p, "action_type", "Approve");
            string role = GetStr(p, "approver_role", "Leader");
            string reason = GetStr(p, "reason", "Đạt chuẩn nghiệm thu");

            return Task.FromResult(ToJson(new
            {
                success = true,
                feature_code = feature,
                ticket_id = ticket,
                action = action,
                approver_role = role,
                reason = reason,
                new_status = action == "Approve" ? "Đã duyệt" : "Đang cập nhật",
                message = "Lưu thông tin thành công",
                approved_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }

        #endregion
    }
}
