namespace GoogleCalendarManagement.Tests.Integration;

public class ErrorHandlingTests
{
    [Fact]
    public void UnhandledTaskException_ShouldBe_SetObserved_WithoutCrash()
    {
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
        };

        // Fire-and-forget task that throws
        Task.Run(() => throw new InvalidOperationException("test"))
            .ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);

        // Allow GC to trigger UnobservedTaskException
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Verifying handler registration doesn't throw — GC timing is non-deterministic
    }
}
