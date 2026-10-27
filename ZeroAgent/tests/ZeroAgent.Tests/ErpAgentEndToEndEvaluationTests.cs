using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Erp;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Sampling;
using ZeroTokenizer.Core.Vietnamese;

namespace ZeroAgent.Tests
{
    public sealed class ErpAgentEndToEndEvaluationTests
    {
        [Fact]
        public async Task ErpWorkflow_EndToEnd_LotBalance_QueriesAndExplains()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterErpToolkit();

            var mockLlm = new MockLlmClient();
            // Step 1: Model reasons and decides to call erp_inventory_lot_balance_query
            mockLlm.Enqueue(
                "Thought: Người dùng muốn kiểm tra tồn kho lô LOT-2026-PP01 tại kho WH-MAT-01. Tôi cần truy vấn tồn kho ERP.\n" +
                "Action: erp_inventory_lot_balance_query({\"lot_no\": \"LOT-2026-PP01\", \"warehouse_code\": \"WH-MAT-01\"})");

            // Step 2: Model receives observation and formulates final answer
            mockLlm.Enqueue(
                "Thought: Đã nhận được dữ liệu tồn kho. Lô LOT-2026-PP01 hiện có tổng tồn kho 4,500 kg, giữ chỗ 1,200 kg và khả dụng thực tế 3,300 kg.\n" +
                "Final Answer: Tại Kho WH-MAT-01, lô hàng LOT-2026-PP01 của vật tư Polypropylene Raw Granules (MAT-PP-500) hiện có tổng tồn 4,500 kg, trong đó đã giữ chỗ 1,200 kg và số lượng khả dụng thực tế còn lại là 3,300 kg. Số lượng này hoàn toàn đủ để cấp phát cho kế hoạch xưởng.");

            var agent = new ReActAgent("ErpAgent", "Enterprise ERP Inventory Assistant", registry, mockLlm);
            var context = new AgentContext("Kiểm tra số lượng khả dụng của lô LOT-2026-PP01 ở kho WH-MAT-01 xem còn đủ cấp phát cho xưởng không");

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Contains("3,300 kg", response.Output);
            Assert.Contains("4,500 kg", response.Output);
            Assert.True(response.TotalSteps >= 1);
        }

        [Fact]
        public async Task ErpWorkflow_EndToEnd_ProductionPlan_TracksWorkOrder()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterErpToolkit();

            var mockLlm = new MockLlmClient();
            mockLlm.Enqueue(
                "Thought: Yêu cầu tra cứu tiến độ công đoạn Extrusion trên dây chuyền EXT-01.\n" +
                "Action: erp_production_plan_query({\"process_stage\": \"Extrusion\", \"line_code\": \"EXT-01\"})");

            mockLlm.Enqueue(
                "Thought: Tiến độ sản xuất lệnh WO-2026-0412 đã hoàn thành 9,600 kg trên kế hoạch 12,000 kg, đạt 80%.\n" +
                "Final Answer: Công đoạn Extrusion tại chuyền EXT-01 theo lệnh sản xuất WO-2026-0412 đã hoàn thành 9,600 / 12,000 kg (đạt 80.0%), lượng phế liệu phát sinh 140 kg, dây chuyền đang vận hành ổn định.");

            var agent = new ReActAgent("ErpAgent", "Enterprise ERP Production Assistant", registry, mockLlm);
            var context = new AgentContext("Kiểm tra tiến độ ca làm việc hiện tại của máy đùn EXT-01");

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Contains("WO-2026-0412", response.Output);
            Assert.Contains("80.0%", response.Output);
            Assert.Contains("9,600", response.Output);
        }

        [Fact]
        public async Task ErpWorkflow_EndToEnd_SalesOrder_QueriesOrderAndLogistics()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterErpToolkit();

            var mockLlm = new MockLlmClient();
            mockLlm.Enqueue(
                "Thought: Tra cứu thông tin đơn hàng SO-2026-0881 từ hệ thống ERP Sales.\n" +
                "Action: erp_sales_order_query({\"order_code\": \"SO-2026-0881\", \"agency\": \"HQ\"})");

            mockLlm.Enqueue(
                "Thought: Đơn hàng SO-2026-0881 đã được duyệt cho khách hàng Global Packaging Solutions Ltd, kiện đóng gói PACK-2026-0312 đã duyệt logistics.\n" +
                "Final Answer: Đơn hàng SO-2026-0881 của khách hàng Global Packaging Solutions Ltd. có tổng giá trị 850,000,000 VND đã ở trạng thái Approved, kiện hàng PACK-2026-0312 đã được bộ phận Logistics thông qua.");

            var agent = new ReActAgent("ErpAgent", "Enterprise ERP Sales Assistant", registry, mockLlm);
            var context = new AgentContext("Kiểm tra trạng thái đơn bán hàng SO-2026-0881");

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Contains("Global Packaging Solutions Ltd.", response.Output);
            Assert.Contains("850,000,000 VND", response.Output);
            Assert.Contains("Approved", response.Output);
        }

        [Fact]
        public async Task ErpWorkflow_EndToEnd_ApprovalExecute_DispatchesDecision()
        {
            var registry = new AgentToolRegistry();
            registry.ApprovalHandler = (tool, arg) => Task.FromResult(true);
            registry.RegisterErpToolkit();

            var mockLlm = new MockLlmClient();
            mockLlm.Enqueue(
                "Thought: Người dùng muốn phê duyệt phiếu nhập kho PN-NVL-2026-0042.\n" +
                "Action: erp_approval_execute({\"feature_code\": \"ERP.Inventory.Material.StockIn\", \"ticket_id\": \"PN-NVL-2026-0042\", \"action_type\": \"Approve\", \"approver_role\": \"Leader\"})");

            mockLlm.Enqueue(
                "Thought: Thao tác phê duyệt đã thành công với trạng thái Đã duyệt.\n" +
                "Final Answer: Phiếu nhập kho PN-NVL-2026-0042 đã được phê duyệt thành công bởi vai trò Leader.");

            var agent = new ReActAgent("ErpAgent", "Enterprise ERP Approval Assistant", registry, mockLlm);
            var context = new AgentContext("Phê duyệt phiếu nhập kho PN-NVL-2026-0042 giúp tôi");

            var response = await agent.ExecuteAsync(context);

            Assert.True(response.Success);
            Assert.Contains("PN-NVL-2026-0042", response.Output);
            Assert.Contains("phê duyệt thành công", response.Output);
        }
    }
}
