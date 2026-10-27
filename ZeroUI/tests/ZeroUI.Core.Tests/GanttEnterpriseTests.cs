using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class GanttEnterpriseTests
    {
        [Fact]
        public void CriticalPathEngine_EmptyOrNullList_ReturnsEmptySet()
        {
            var emptyResult = CriticalPathEngine.CalculateCriticalPath(new List<GanttTaskItem>());
            Assert.Empty(emptyResult);

            var nullResult = CriticalPathEngine.CalculateCriticalPath(null!);
            Assert.Empty(nullResult);
        }

        [Fact]
        public void CriticalPathEngine_LinearChain_AllTasksAreCritical()
        {
            DateTime start = new DateTime(2026, 9, 1);
            var task1 = new GanttTaskItem(1, "Phase 1: Design", start, start.AddDays(5));
            var task2 = new GanttTaskItem(2, "Phase 2: Fabrication", start.AddDays(5), start.AddDays(15));
            task2.PredecessorIds.Add(1);
            var task3 = new GanttTaskItem(3, "Phase 3: Assembly & Inspection", start.AddDays(15), start.AddDays(20));
            task3.PredecessorIds.Add(2);

            var tasks = new List<GanttTaskItem> { task1, task2, task3 };
            var criticalIds = CriticalPathEngine.CalculateCriticalPath(tasks);

            Assert.Contains(1, criticalIds);
            Assert.Contains(2, criticalIds);
            Assert.Contains(3, criticalIds);
            Assert.Equal(3, criticalIds.Count);
        }

        [Fact]
        public void CriticalPathEngine_ParallelBranches_IdentifiesLongestPathAndExcludesSlackTasks()
        {
            DateTime start = new DateTime(2026, 9, 1);

            // Task 1: Start node (duration 3 days)
            var task1 = new GanttTaskItem(1, "Milestone Start", start, start.AddDays(3));

            // Branch A (Long branch: 10 days) -> Critical
            var task2A = new GanttTaskItem(2, "Custom Machining", start.AddDays(3), start.AddDays(13));
            task2A.PredecessorIds.Add(1);

            // Branch B (Short branch: 2 days) -> Has slack of 8 days, NOT critical
            var task2B = new GanttTaskItem(3, "Order Off-The-Shelf Screws", start.AddDays(3), start.AddDays(5));
            task2B.PredecessorIds.Add(1);

            // Task 4: Merge node (duration 4 days)
            var task4 = new GanttTaskItem(4, "Final Packaging", start.AddDays(13), start.AddDays(17));
            task4.PredecessorIds.Add(2);
            task4.PredecessorIds.Add(3);

            var tasks = new List<GanttTaskItem> { task1, task2A, task2B, task4 };
            var criticalIds = CriticalPathEngine.CalculateCriticalPath(tasks);

            // Task 1, 2A, and 4 must be critical
            Assert.Contains(1, criticalIds);
            Assert.Contains(2, criticalIds);
            Assert.Contains(4, criticalIds);

            // Task 2B (short branch) must NOT be critical
            Assert.DoesNotContain(3, criticalIds);
            Assert.Equal(3, criticalIds.Count);
        }

        [Fact]
        public void GanttTimeScale_SupportsStandardIndustrialResolutions()
        {
            Assert.Equal(GanttTimeScale.Hours, (GanttTimeScale)0);
            Assert.Equal(GanttTimeScale.Days, (GanttTimeScale)1);
            Assert.Equal(GanttTimeScale.Weeks, (GanttTimeScale)2);
            Assert.Equal(GanttTimeScale.Months, (GanttTimeScale)3);
            Assert.Equal(GanttTimeScale.Quarters, (GanttTimeScale)4);
        }

        [Fact]
        public void GanttDependencyType_SupportsAllEnterpriseLinkTypes()
        {
            Assert.Equal(GanttDependencyType.FinishToStart, (GanttDependencyType)0);
            Assert.Equal(GanttDependencyType.StartToStart, (GanttDependencyType)1);
            Assert.Equal(GanttDependencyType.FinishToFinish, (GanttDependencyType)2);
            Assert.Equal(GanttDependencyType.StartToFinish, (GanttDependencyType)3);
        }
    }
}
