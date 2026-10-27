using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ZeroUI.Desktop.Tests
{
    /// <summary>
    /// Executes test logic on an STA (Single Threaded Apartment) thread,
    /// essential for WPF and Windows Forms UI control automation tests.
    /// </summary>
    public static class StaTestRunner
    {
        public static void Run(Action testAction)
        {
            if (testAction == null) throw new ArgumentNullException(nameof(testAction));

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                testAction();
                return;
            }

            Exception? caughtException = null;
            var thread = new Thread(() =>
            {
                try
                {
                    testAction();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caughtException != null)
            {
                ExceptionDispatchInfo.Capture(caughtException).Throw();
            }
        }
    }
}
