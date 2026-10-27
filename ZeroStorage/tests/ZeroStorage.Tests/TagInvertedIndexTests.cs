using System;
using System.Collections.Generic;
using Xunit;
using ZeroStorage.Core.Indexing;

namespace ZeroStorage.Tests
{
    public class TagInvertedIndexTests
    {
        [Fact]
        public void TagInvertedIndex_RegistrationAndQuery_ResolvesAccurately()
        {
            var index = new TagInvertedIndex();

            var tags1 = new Dictionary<string, string>
            {
                ["line"] = "Line1",
                ["machine"] = "CNC_01",
                ["sensor"] = "Vibration"
            };

            var tags2 = new Dictionary<string, string>
            {
                ["line"] = "Line1",
                ["machine"] = "CNC_02",
                ["sensor"] = "Vibration"
            };

            var tags3 = new Dictionary<string, string>
            {
                ["line"] = "Line2",
                ["machine"] = "CNC_01",
                ["sensor"] = "Temperature"
            };

            int id1 = index.GetOrRegisterSeries("telemetry", tags1);
            int id2 = index.GetOrRegisterSeries("telemetry", tags2);
            int id3 = index.GetOrRegisterSeries("telemetry", tags3);

            Assert.Equal(3, index.SeriesCount);

            // Re-registering id1 returns the exact same ID
            int id1Again = index.GetOrRegisterSeries("telemetry", tags1);
            Assert.Equal(id1, id1Again);

            // Query by line="Line1": should return id1, id2
            var line1Matches = index.FindSeries(new KeyValuePair<string, string>("line", "Line1"));
            Assert.Equal(new int[] { id1, id2 }, line1Matches);

            // Query by machine="CNC_01": should return id1, id3
            var cnc01Matches = index.FindSeries(new KeyValuePair<string, string>("machine", "CNC_01"));
            Assert.Equal(new int[] { id1, id3 }, cnc01Matches);

            // Query intersection: line="Line1" AND machine="CNC_01": should return only id1
            var intersectionMatches = index.FindSeries(
                new KeyValuePair<string, string>("line", "Line1"),
                new KeyValuePair<string, string>("machine", "CNC_01")
            );
            Assert.Equal(new int[] { id1 }, intersectionMatches);

            // Query non-existing tag: returns empty
            var noMatches = index.FindSeries(new KeyValuePair<string, string>("line", "Line999"));
            Assert.Empty(noMatches);
        }
    }
}
