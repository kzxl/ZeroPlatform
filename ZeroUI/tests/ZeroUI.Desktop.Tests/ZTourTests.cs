using System;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace ZeroUI.Desktop.Tests
{
    public class ZTourTests
    {
        [Fact]
        public void Wpf_ZTour_StepNavigation_And_State_WorksCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var window = new Window();
                var btn1 = new Button { Content = "Target 1" };
                var btn2 = new Button { Content = "Target 2" };
                var btn3 = new Button { Content = "Target 3" };

                var tour = new ZeroUI.Wpf.Overlays.ZTour(window);
                tour.Steps.Add(new ZeroUI.Wpf.Overlays.ZTourStep(btn1, "Bước 1", "Mô tả bước 1", ZeroUI.Wpf.Overlays.ZTourPlacement.Bottom));
                tour.Steps.Add(new ZeroUI.Wpf.Overlays.ZTourStep(btn2, "Bước 2", "Mô tả bước 2", ZeroUI.Wpf.Overlays.ZTourPlacement.Top));
                tour.Steps.Add(new ZeroUI.Wpf.Overlays.ZTourStep(btn3, "Bước 3", "Mô tả bước 3", ZeroUI.Wpf.Overlays.ZTourPlacement.Right));

                Assert.Equal(3, tour.Steps.Count);
                Assert.Equal(-1, tour.CurrentIndex);

                // Giả lập bắt đầu
                var task = tour.StartAsync(0);
                Assert.Equal(0, tour.CurrentIndex);
                Assert.Equal("Bước 1", tour.CurrentStep?.Title);

                // Next sang bước 2
                tour.Next();
                Assert.Equal(1, tour.CurrentIndex);
                Assert.Equal("Bước 2", tour.CurrentStep?.Title);

                // Next sang bước 3
                tour.Next();
                Assert.Equal(2, tour.CurrentIndex);
                Assert.Equal("Bước 3", tour.CurrentStep?.Title);

                // Prev về bước 2
                tour.Previous();
                Assert.Equal(1, tour.CurrentIndex);

                // Close hoàn tất
                tour.Close(completed: true);
                Assert.False(tour.IsActive);
            });
        }

        [Fact]
        public void Wpf_ZTour_StepCallbacks_OnEnter_OnLeave_FireCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var window = new Window();
                var btn = new Button();

                bool step1Entered = false;
                bool step1Left = false;
                bool step2Entered = false;

                var step1 = new ZeroUI.Wpf.Overlays.ZTourStep(btn, "S1", "Desc 1")
                {
                    OnEnter = s => step1Entered = true,
                    OnLeave = s => step1Left = true
                };

                var step2 = new ZeroUI.Wpf.Overlays.ZTourStep(btn, "S2", "Desc 2")
                {
                    OnEnter = s => step2Entered = true
                };

                var tour = new ZeroUI.Wpf.Overlays.ZTour(window, new[] { step1, step2 });
                var task = tour.StartAsync();

                Assert.True(step1Entered);
                Assert.False(step1Left);
                Assert.False(step2Entered);

                tour.Next();

                Assert.True(step1Left);
                Assert.True(step2Entered);

                tour.Close();
            });
        }

        [Fact]
        public void WinForms_ZTour_Navigation_And_State_WorksCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var form = new System.Windows.Forms.Form();
                var btn1 = new System.Windows.Forms.Button();
                var btn2 = new System.Windows.Forms.Button();

                var tour = new ZeroUI.WinForms.Overlays.ZTour(form);
                tour.Steps.Add(new ZeroUI.WinForms.Overlays.ZTourStep(btn1, "WF Bước 1", "Hướng dẫn nút 1"));
                tour.Steps.Add(new ZeroUI.WinForms.Overlays.ZTourStep(btn2, "WF Bước 2", "Hướng dẫn nút 2"));

                Assert.Equal(2, tour.Steps.Count);
                Assert.Equal(-1, tour.CurrentIndex);

                tour.Start();
                Assert.Equal(0, tour.CurrentIndex);
                Assert.Equal("WF Bước 1", tour.CurrentStep?.Title);

                tour.Next();
                Assert.Equal(1, tour.CurrentIndex);
                Assert.Equal("WF Bước 2", tour.CurrentStep?.Title);

                tour.Close(completed: true);
                Assert.False(tour.IsActive);
            });
        }

        [Fact]
        public void ZTour_BackwardCompatibility_Aliases_ResolveCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var window = new Window();
#pragma warning disable CS0618
                var wpfLegacyTour = new ZeroUI.Wpf.Overlays.ZeroTour(window);
                Assert.NotNull(wpfLegacyTour);
                Assert.IsAssignableFrom<ZeroUI.Wpf.Overlays.ZTour>(wpfLegacyTour);

                var form = new System.Windows.Forms.Form();
                var wfLegacyTour = new ZeroUI.WinForms.Overlays.ZeroTour(form);
                Assert.NotNull(wfLegacyTour);
                Assert.IsAssignableFrom<ZeroUI.WinForms.Overlays.ZTour>(wfLegacyTour);
#pragma warning restore CS0618
            });
        }
    }
}
