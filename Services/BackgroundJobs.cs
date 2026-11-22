using System.Threading.Channels;

namespace PraffLeather.Services;

/// <summary>
/// Uzun süren işler (toplu e-posta) için basit arka plan kuyruğu. İş, isteğin bitmesini beklemez;
/// her iş kendi DI kapsamında çalışır. Uygulama yeniden başlarsa kuyruktaki işler kaybolur.
/// </summary>
public class BackgroundJobQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel =
        Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

    public ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> job) => _channel.Writer.WriteAsync(job);
    public IAsyncEnumerable<Func<IServiceProvider, CancellationToken, Task>> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public class BackgroundJobRunner(BackgroundJobQueue queue, IServiceScopeFactory scopes, ILogger<BackgroundJobRunner> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await job(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Arka plan işi başarısız oldu"); }
        }
    }
}
