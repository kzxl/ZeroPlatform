using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Erp;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Tests
{
    public class ErpEnterpriseToolkitTests
    {
        [Fact]
        public void ErpEnterpriseToolkit_RegistersAllToolsSuccessfully()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterIndustrialToolkit();

            // Total industrial tools + 43 ERP enterprise tools
            Assert.True(registry.Count >= 45);

            // Warehouse tools
            Assert.True(registry.TryGetTool("check_inventory", out var checkInv));
            Assert.False(checkInv.RequiresApproval);
            Assert.True(registry.TryGetTool("create_stock_transfer", out var stockTrf));
            Assert.True(stockTrf.RequiresApproval);
            Assert.True(registry.TryGetTool("lookup_bin_location", out _));
            Assert.True(registry.TryGetTool("check_fefo_expiry", out _));
            Assert.True(registry.TryGetTool("scan_barcode_inbound", out _));

            // Production & MES tools
            Assert.True(registry.TryGetTool("get_work_order_progress", out _));
            Assert.True(registry.TryGetTool("calculate_oee", out _));
            Assert.True(registry.TryGetTool("schedule_tpm_preventive", out _));
            Assert.True(registry.TryGetTool("analyze_scrap_defect_cause", out _));
            Assert.True(registry.TryGetTool("record_shift_handover", out _));
            Assert.True(registry.TryGetTool("report_machine_breakdown", out var rptBreakdown));
            Assert.True(rptBreakdown.RequiresApproval);

            // Financial & Purchasing tools
            Assert.True(registry.TryGetTool("issue_vat_invoice", out var vatInv));
            Assert.True(vatInv.RequiresApproval);
            Assert.True(registry.TryGetTool("create_payment_voucher", out var payVoucher));
            Assert.True(payVoucher.RequiresApproval);
            Assert.True(registry.TryGetTool("create_purchase_order", out var poTool));
            Assert.True(poTool.RequiresApproval);
        }

        [Fact]
        public async Task WarehouseTools_ExecuteWithFlexibleArguments()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterErpToolkit();

            // 1. Check inventory with key-value style
            string invResult1 = await registry.ExecuteAsync("check_inventory", "sku='SKU-PL01', warehouse='Kho Bình Dương'");
            Assert.Contains("SKU-PL01", invResult1);
            Assert.Contains("available_qty", invResult1);

            // 2. Check inventory with JSON style
            string invResult2 = await registry.ExecuteAsync("check_inventory", "{\"sku\":\"SKU-AL02\",\"warehouse\":\"Kho Tân Bình\"}");
            Assert.Contains("SKU-AL02", invResult2);
            Assert.Contains("InStock", invResult2);

            // 3. Lookup Bin Location
            string binResult = await registry.ExecuteAsync("lookup_bin_location", "sku='SKU-PL01', warehouse='Kho Bình Dương'");
            Assert.Contains("bin_code", binResult);
            Assert.Contains("A03-R02-B14", binResult);

            // 4. FEFO Lot Expiry
            string fefoResult = await registry.ExecuteAsync("check_fefo_expiry", "sku='SKU-PL01', days_ahead=30");
            Assert.Contains("FEFO", fefoResult);
            Assert.Contains("LOT-2026-01-A", fefoResult);
        }

        [Fact]
        public async Task ProductionTools_CalculateOeeAndRCFASuccessfully()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterErpToolkit();

            // 1. Calculate OEE
            string oeeResult = await registry.ExecuteAsync("calculate_oee", "line_id='LINE-01', date='2026-10-03'");
            using var doc = JsonDocument.Parse(oeeResult);
            double oee = doc.RootElement.GetProperty("oee_percent").GetDouble();
            Assert.True(oee > 80.0);

            // 2. Root Cause Failure Analysis
            string rcfaResult = await registry.ExecuteAsync("analyze_scrap_defect_cause", "work_order='WO-2026-0901', line_id='LINE-01'");
            Assert.Contains("root_cause", rcfaResult);
            Assert.Contains("corrective_action", rcfaResult);

            // 3. Shift Handover
            string shiftResult = await registry.ExecuteAsync("record_shift_handover", "shift='Ca 1', line_id='LINE-01', operator='Nguyễn Văn B', notes='Ổn định'");
            Assert.Contains("Signed", shiftResult);
        }

        [Fact]
        public async Task DangerousActions_EnforceHitlSafetyGate()
        {
            var registry = new AgentToolRegistry();
            var safetyGate = new HitlSafetyGate();
            registry.RegisterIndustrialToolkit(safetyGate);

            // Trigger sensitive payment voucher creation
            var task = registry.ExecuteAsync("create_payment_voucher", "recipient='Petro Chem', amount=50000000, reason='Thanh toán đợt 1'");

            await Task.Delay(50);
            var pending = safetyGate.PendingRequests;
            Assert.NotEmpty(pending);

            // Approve
            foreach (var req in pending)
            {
                Assert.Equal("create_payment_voucher", req.ToolName);
                safetyGate.Approve(req.RequestId, "ChiefAccountant-Phong");
            }

            string result = await task;

            Assert.Contains("ApprovedByChiefAccountant", result);
            Assert.Contains("PC-2026", result);
        }

        [Fact]
        public async Task EnterpriseErpOperations_ExecuteAndValidateContracts()
        {
            var registry = new AgentToolRegistry();
            registry.ApprovalHandler = (tool, arg) => Task.FromResult(true);
            registry.RegisterErpToolkit();

            // 1. ERP LotBalance Query
            string lotRes = await registry.ExecuteAsync("erp_inventory_lot_balance_query", "lot_no='LOT-2026-PP01', warehouse_code='WH-MAT-01'");
            Assert.Contains("WH-MAT-01", lotRes);
            Assert.Contains("Manufacturing Raw Material Warehouse", lotRes);
            Assert.Contains("quantity_available", lotRes);

            // 2. ERP Production Plan Query
            string planRes = await registry.ExecuteAsync("erp_production_plan_query", "process_stage='Extrusion', line_code='EXT-01'");
            Assert.Contains("Extrusion", planRes);
            Assert.Contains("WO-2026", planRes);
            Assert.Contains("progress_percent", planRes);

            // 3. ERP Sales Order Query
            string orderRes = await registry.ExecuteAsync("erp_sales_order_query", "order_code='SO-2026-0881', agency='HQ'");
            Assert.Contains("SO-2026-0881", orderRes);
            Assert.Contains("HQ", orderRes);
            Assert.Contains("PACK-2026", orderRes);

            // 4. ERP Sales Packing Audit
            string packRes = await registry.ExecuteAsync("erp_sales_packing_audit", "packing_id='PACK-2026-0312'");
            Assert.Contains("team_leader_approved", packRes);
            Assert.Contains("logistics_approved", packRes);

            // 5. ERP RD BOM Query
            string bomRes = await registry.ExecuteAsync("erp_rd_bom_query", "product_code='PP-LID-120', status='Release'");
            Assert.Contains("BOM-PPLID-v2.1", bomRes);
            Assert.Contains("MAT-PP-500", bomRes);

            // 6. Common ERP Approval Execute
            string appRes = await registry.ExecuteAsync("erp_approval_execute", "feature_code='ERP.Inventory.Material.StockIn', ticket_id='PN-NVL-2026-0042', action_type='Approve', approver_role='Leader'");
            Assert.Contains("Đã duyệt", appRes);
            Assert.Contains("Lưu thông tin thành công", appRes);
        }
    }
}
