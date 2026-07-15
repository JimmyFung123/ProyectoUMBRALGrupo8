namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

/// <summary>
/// Shared polling helper for Level-2 real-broker tests. Consumption over a real broker is
/// asynchronous — an immediate assert right after publish is flaky by construction, so every
/// state read in a Level-2 test should go through this helper instead. Extracted from the
/// original private copy in <c>StageAddedFanOutTests</c> once more test classes needed it.
/// </summary>
internal static class Polling
{
    public static async Task<T> PollAsync<T>(
        Func<Task<T>> probe,
        Func<T, bool> isDone,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var delay = interval ?? TimeSpan.FromMilliseconds(300);

        var last = await probe();
        while (!isDone(last) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(delay);
            last = await probe();
        }

        return last;
    }
}
