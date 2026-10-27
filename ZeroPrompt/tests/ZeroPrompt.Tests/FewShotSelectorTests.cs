using System;
using Xunit;
using ZeroPrompt.Core.FewShot;

namespace ZeroPrompt.Tests
{
    public class FewShotSelectorTests
    {
        [Fact]
        public void FewShotSelector_Should_Retrieve_Most_Similar_Exemplar()
        {
            var selector = new FewShotSelector();

            // Exemplar 1: PLC Query (direction: [1.0, 0.0, 0.0])
            selector.Add(new Exemplar(
                "ex1",
                "Read PLC register 100",
                "Query Modbus PLC",
                "plc_read(100)",
                "Value: 42",
                new float[] { 1.0f, 0.0f, 0.0f }));

            // Exemplar 2: TSDB Query (direction: [0.0, 1.0, 0.0])
            selector.Add(new Exemplar(
                "ex2",
                "Query temperature metrics",
                "Fetch TSDB time series",
                "tsdb_query(\"temp\")",
                "Points: 120",
                new float[] { 0.0f, 1.0f, 0.0f }));

            // Query vector aligned with Exemplar 1: [0.95, 0.05, 0.0]
            float[] query = new float[] { 0.95f, 0.05f, 0.0f };
            var top = selector.SelectTopK(query, k: 1);

            Assert.Single(top);
            Assert.Equal("ex1", top[0].Id);

            string promptBlock = selector.BuildDemonstrationPrompt(query, k: 1);
            Assert.Contains("plc_read(100)", promptBlock);
        }
    }
}
