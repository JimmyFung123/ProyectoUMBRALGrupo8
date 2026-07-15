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
        // 30s de techo (no 10s): la entrega + consumo sobre RabbitMQ real puede
        // tardar más en runners de CI cargados/lentos, y una espera corta hace
        // flaky a los tests de fan-out/relay. El poll retorna apenas se cumple la
        // condición, así que subir el techo NO ralentiza los casos que pasan.
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
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
