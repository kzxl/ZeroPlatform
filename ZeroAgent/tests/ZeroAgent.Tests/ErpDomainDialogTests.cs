using System;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Engine;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;

namespace ZeroAgent.Tests
{
    public class ErpDomainDialogTests
    {
        [Fact]
        public async Task ErpBot_MoE_RoutesToInventoryDomain_AndFillsSlots()
        {
            var bot = ErpDialogFactory.CreateErpBot();
            string sessionId = "test-erp-inv-1";

            var response = await bot.ChatAsync(sessionId, "Kiểm tra tồn kho mã hàng SKU-STEEL-01 tại kho tổng");

            var session = bot.Sessions.GetOrCreate(sessionId);
            Assert.Equal("inventory", session.ActiveDomain);
            Assert.Equal("CHECK_INVENTORY", response.IntentName);
            Assert.Equal(SessionState.Completed, response.State);
            Assert.True(session.HasSlot("item_code"));
            Assert.Equal("SKU-STEEL-01", session.Slots["item_code"]);
            Assert.Contains("500 cái", response.Text);
        }

        [Fact]
        public async Task ErpBot_MoE_RoutesToSalesDomain_AndExecutesSoStatus()
        {
            var bot = ErpDialogFactory.CreateErpBot();
            string sessionId = "test-erp-sales-1";

            var response = await bot.ChatAsync(sessionId, "Tiến độ đơn hàng SO-2026-001 tới đâu rồi");

            var session = bot.Sessions.GetOrCreate(sessionId);
            Assert.Equal("sales", session.ActiveDomain);
            Assert.Equal("CHECK_SO_STATUS", response.IntentName);
            Assert.Equal(SessionState.Completed, response.State);
            Assert.Equal("SO-2026-001", session.Slots["so_number"]);
            Assert.Contains("xuất kho 80%", response.Text);
        }

        [Fact]
        public async Task ErpBot_MoE_RoutesToCustomerBalance_AndExtractsPartner()
        {
            var bot = ErpDialogFactory.CreateErpBot();
            string sessionId = "test-erp-cust-1";

            var response = await bot.ChatAsync(sessionId, "Tra cứu công nợ của khách hàng ALPHA");

            var session = bot.Sessions.GetOrCreate(sessionId);
            Assert.Equal("sales", session.ActiveDomain);
            Assert.Equal("GET_CUSTOMER_BALANCE", response.IntentName);
            Assert.Equal(SessionState.Completed, response.State);
            Assert.Contains("145.000.000 VNĐ", response.Text);
        }

        [Fact]
        public async Task ErpBot_MoE_RoutesToProductionDomain_AndChecksMo()
        {
            var bot = ErpDialogFactory.CreateErpBot();
            string sessionId = "test-erp-prod-1";

            var response = await bot.ChatAsync(sessionId, "Tiến độ lệnh sản xuất MO-2026-01 đang thế nào");

            var session = bot.Sessions.GetOrCreate(sessionId);
            Assert.Equal("production", session.ActiveDomain);
            Assert.Equal("CHECK_MO_PROGRESS", response.IntentName);
            Assert.Equal(SessionState.Completed, response.State);
            Assert.Equal("MO-2026-01", session.Slots["mo_number"]);
            Assert.Contains("65%", response.Text);
        }

        [Fact]
        public async Task ErpBot_MoE_RoutesToFinanceDomain_AndReportsCashBalance()
        {
            var bot = ErpDialogFactory.CreateErpBot();
            string sessionId = "test-erp-fin-1";

            var response = await bot.ChatAsync(sessionId, "Kiểm tra số dư quỹ tiền mặt và ngân hàng hiện tại");

            var session = bot.Sessions.GetOrCreate(sessionId);
            Assert.Equal("finance", session.ActiveDomain);
            Assert.Equal("CHECK_CASH_BALANCE", response.IntentName);
            Assert.Equal(SessionState.Completed, response.State);
            Assert.Contains("VCB", response.Text);
            Assert.Contains("TCB", response.Text);
        }

        [Fact]
        public async Task ErpBot_LiveLocalGemma_SynthesizesNaturalResponseAcrossDomains()
        {
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            try
            {
                var ping = await httpClient.GetAsync("http://192.168.10.7:1234/v1/models");
                if (!ping.IsSuccessStatusCode) return; // Skip if local gateway is offline
            }
            catch
            {
                return; // Gateway offline in environment
            }

            var bot = ErpDialogFactory.CreateErpBot();

            var gemmaClient = new ExternalApiLlmClient(
                endpoint: "http://192.168.10.7:1234/v1/chat/completions",
                model: "google/gemma-4-e4b",
                apiKey: "ollama");

            bot.UseGenerativeNlg(gemmaClient);

            string sessionId = "test-erp-live-gemma";
            var response = await bot.ChatAsync(sessionId, "Kiểm tra tồn kho mã hàng SKU-STEEL-01 tại kho tổng");

            Assert.Equal("CHECK_INVENTORY", response.IntentName);
            Assert.Equal(SessionState.Completed, response.State);
            Assert.False(string.IsNullOrWhiteSpace(response.Text));
            Assert.True(response.Text.Length > 10);
        }
    }
}
