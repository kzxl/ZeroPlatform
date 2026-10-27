using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Http;

namespace ZeroNetwork.Tests
{
    public class ZeroApiClientTests
    {
        private class UserDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class OrderDto
        {
            public string OrderNo { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Fact]
        public async Task ZeroApiClient_GetAndPost_StreamsDirectly()
        {
            int port = GetFreePort();
            using (var server = new ZeroHttpServer(port, "127.0.0.1"))
            {
                server.MapGet("/api/user", req => ZeroHttpResponse.Json("{\"id\":42,\"name\":\"Phong Vo\"}"));
                server.MapPost("/api/orders", req => ZeroHttpResponse.Json(req.Body));

                server.Start();

                string baseUrl = $"http://127.0.0.1:{port}";
                using (var client = new ZeroApiClient(baseUrl))
                {
                    // 1. Test GET with stream deserialization
                    var user = await client.GetAsync<UserDto>("api/user");
                    Assert.NotNull(user);
                    Assert.Equal(42, user.Id);
                    Assert.Equal("Phong Vo", user.Name);

                    // 2. Test POST with stream serialization and deserialization
                    var order = new OrderDto { OrderNo = "ORD-2026-001", Amount = 1500.50m };
                    var resultOrder = await client.PostAsync<OrderDto, OrderDto>("api/orders", order);

                    Assert.NotNull(resultOrder);
                    Assert.Equal("ORD-2026-001", resultOrder.OrderNo);
                    Assert.Equal(1500.50m, resultOrder.Amount);
                }

                server.Stop();
            }
        }

        [Fact]
        public async Task ZeroHttpExtensions_FluentString_Works()
        {
            int port = GetFreePort();
            using (var server = new ZeroHttpServer(port, "127.0.0.1"))
            {
                server.MapGet("/api/profile", req => ZeroHttpResponse.Json("{\"id\":99,\"name\":\"ERP User\"}"));

                server.Start();

                string url = $"http://127.0.0.1:{port}/api/profile";
                var profile = await url.GetJsonAsync<UserDto>();

                Assert.NotNull(profile);
                Assert.Equal(99, profile.Id);
                Assert.Equal("ERP User", profile.Name);

                // Test DownloadBytesAsync
                byte[] bytes = await url.DownloadBytesAsync();
                Assert.NotNull(bytes);
                Assert.True(bytes.Length > 0);

                server.Stop();
            }
        }

        [Fact]
        public void ZeroApiClient_AppendQueryParams_FormatsCorrectly()
        {
            string url = "https://api.erp.local/inventory/stock";
            var query = new { page = 1, pageSize = 50, warehouse = "WH-01" };

            string fullUrl = ZeroApiClient.AppendQueryParams(url, query);

            Assert.Contains("page=1", fullUrl);
            Assert.Contains("pageSize=50", fullUrl);
            Assert.Contains("warehouse=WH-01", fullUrl);
            Assert.StartsWith(url + "?", fullUrl);
        }
    }
}
