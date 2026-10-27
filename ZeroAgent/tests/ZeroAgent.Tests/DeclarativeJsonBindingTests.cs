using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Dialog.Binding;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroData.Core;

namespace ZeroAgent.Tests
{
    public class DeclarativeJsonBindingTests
    {
        [Fact]
        public async Task JsonIntentLoader_BindsDeclarativeJson_ToMockDatabaseExecutor()
        {
            string jsonConfig = @"
{
  ""IntentId"": ""ERP_QUERY_SALES_ORDER"",
  ""DisplayName"": ""Tra cứu đơn hàng bán"",
  ""RequiredPermission"": ""Sales.View"",
  ""SampleUtterances"": [
    ""kiểm tra đơn hàng"",
    ""tình trạng đơn sale"",
    ""xem đơn hàng""
  ],
  ""Slots"": [
    {
      ""Name"": ""order_code"",
      ""Type"": ""string"",
      ""IsRequired"": true,
      ""ClarificationPrompt"": ""Vui lòng cung cấp mã đơn hàng cần kiểm tra.""
    }
  ],
  ""DataSource"": {
    ""Provider"": ""SqlServer"",
    ""ConnectionKey"": ""MDS_ERP_Production"",
    ""Query"": ""SELECT OrderCode, CustomerName, DeliveryStatus, TotalAmount FROM tb_SalesOrders WHERE OrderCode = @order_code"",
    ""Parameters"": {
      ""@order_code"": ""{{slots.order_code}}""
    }
  },
  ""ResponseTemplate"": ""Đơn hàng {{OrderCode}} của {{CustomerName}} - Trạng thái: {{DeliveryStatus}} (Tổng tiền: {{TotalAmount}} VNĐ)""
}";

            var mockDb = new MockDbSourceExecutor("SqlServer");
            mockDb.AddTable("tb_SalesOrders", new[]
            {
                new Dictionary<string, object?>
                {
                    ["OrderCode"] = "SO-1082",
                    ["CustomerName"] = "Công ty TNHH Minh Long",
                    ["DeliveryStatus"] = "Đang đóng gói",
                    ["TotalAmount"] = "45,000,000"
                },
                new Dictionary<string, object?>
                {
                    ["OrderCode"] = "SO-1085",
                    ["CustomerName"] = "Đại lý An Phát",
                    ["DeliveryStatus"] = "Chờ xuất kho",
                    ["TotalAmount"] = "120,000,000"
                }
            });

            var loader = new JsonIntentLoader()
                .RegisterExecutor(mockDb);

            var intent = loader.LoadFromJson(jsonConfig);

            Assert.Equal("ERP_QUERY_SALES_ORDER", intent.Name);
            Assert.Equal("Sales.View", intent.RequiredPermission);
            Assert.Contains("order_code", intent.RequiredSlots);
            Assert.NotNull(intent.ActionHandler);

            // Execute simulated session with slot order_code = SO-1082
            var session = new DialogueSession("test_session");
            session.SetSlot("order_code", "SO-1082");

            string result = await intent.ActionHandler!(session);

            Assert.Contains("SO-1082", result);
            Assert.Contains("Minh Long", result);
            Assert.Contains("Đang đóng gói", result);
        }

        [Fact]
        public async Task DataFrameSourceExecutor_ExecutesColumnarQueriesSafely()
        {
            var df = new DataFrame(
                new DataColumn<string>("OrderCode", new[] { "SO-201", "SO-202", "SO-203" }),
                new DataColumn<string>("Customer", new[] { "Kha Banh", "Minh Long", "Tan Binh" }),
                new DataColumn<string>("Status", new[] { "Completed", "Pending", "Shipping" })
            );

            var executor = new DataFrameSourceExecutor();
            executor.RegisterDataFrame("SalesOrders", df);

            var queryParams = new Dictionary<string, object?>
            {
                ["@OrderCode"] = "SO-202"
            };

            var queryResult = await executor.ExecuteAsync("SalesOrders", "SELECT * FROM SalesOrders", queryParams);

            Assert.True(queryResult.Success);
            Assert.Single(queryResult.Rows);
            Assert.Equal("SO-202", queryResult.Rows[0]["OrderCode"]);
            Assert.Equal("Minh Long", queryResult.Rows[0]["Customer"]);
            Assert.Equal("Pending", queryResult.Rows[0]["Status"]);
        }

        [Fact]
        public async Task EndToEnd_DeclarativeJsonERPFlow_WithDialogueEngine()
        {
            string jsonConfig = @"
{
  ""IntentId"": ""ERP_QUERY_INVENTORY"",
  ""DisplayName"": ""Kiểm tra tồn kho"",
  ""SampleUtterances"": [
    ""kiểm tra tồn kho"",
    ""xem tồn kho"",
    ""tồn kho còn bao nhiêu""
  ],
  ""Slots"": [
    {
      ""Name"": ""product"",
      ""Type"": ""string"",
      ""IsRequired"": true,
      ""ClarificationPrompt"": ""Bạn muốn kiểm tra tồn kho cho sản phẩm nào?""
    }
  ],
  ""DataSource"": {
    ""Provider"": ""MockDb"",
    ""ConnectionKey"": ""ERP_Inventory"",
    ""Query"": ""SELECT ProductCode, Quantity, Warehouse FROM tb_Inventory WHERE ProductCode = @product"",
    ""Parameters"": {
      ""@product"": ""{{slots.product}}""
    }
  },
  ""ResponseTemplate"": ""Sản phẩm {{ProductCode}} còn {{Quantity}} cái tại kho {{Warehouse}}.""
}";

            var mockDb = new MockDbSourceExecutor("MockDb");
            mockDb.AddTable("tb_Inventory", new[]
            {
                new Dictionary<string, object?>
                {
                    ["ProductCode"] = "SP-99",
                    ["Quantity"] = "450",
                    ["Warehouse"] = "Kho Tổng Tân Bình"
                }
            });

            var loader = new JsonIntentLoader().RegisterExecutor(mockDb);
            var intent = loader.LoadFromJson(jsonConfig);

            var engine = new ZeroDialogEngine();
            engine.Dst.RegisterIntent(intent);

            var profile = new UserProfile("saleman_01", "Phong", UserRole.Operator);

            // Step 1: User asks without product slot
            var r1 = await engine.ChatAsync("session_erp_01", "Kiểm tra tồn kho giúp anh với", profile);
            Assert.Equal(SessionState.CollectingSlots, r1.State);
            Assert.Contains("Bạn muốn kiểm tra tồn kho cho sản phẩm nào?", r1.Text);

            // Step 2: User provides product code
            var r2 = await engine.ChatAsync("session_erp_01", "SP-99", profile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Contains("SP-99 còn 450 cái tại kho Kho Tổng Tân Bình", r2.Text);
            Assert.True(r2.IsActionExecuted);
        }
    }
}
