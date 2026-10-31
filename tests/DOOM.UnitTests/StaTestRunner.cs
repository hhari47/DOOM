namespace DOOM.UnitTests;

/// <summary>
/// Helper to execute WPF Window creation tests on an STA thread.
/// </summary>
public static class StaTestRunner
{
    public static void Run(Action testAction)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                testAction();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (caught != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(caught).Throw();
        }
    }
}

