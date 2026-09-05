using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WatchWeaver.Jellyfin.Streaming;

[Authorize]
[ApiController]
[Route("api/watchweaver/events")]
public sealed class EventsController(EventBroadcaster broadcaster, ILogger<EventsController> log) : ControllerBase
{
    [HttpGet]
    [Produces("text/event-stream")]
    public async Task GetEvents()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";

        var (id, reader) = broadcaster.Subscribe();
        log.LogInformation("WatchWeaver event-stream client connected; subscriber_count={SubscriberCount}", broadcaster.SubscriberCount);
        try
        {
            await WriteFrameAsync("hello", new
            {
                protocol_version = 1,
                plugin_version = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0.1.0",
                server = "jellyfin"
            }, HttpContext.RequestAborted);

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                var read = reader.WaitToReadAsync(HttpContext.RequestAborted).AsTask();
                var ping = Task.Delay(TimeSpan.FromSeconds(20), HttpContext.RequestAborted);
                var completed = await Task.WhenAny(read, ping);
                if (completed == ping)
                {
                    await ping;
                    await WriteFrameAsync("ping", new { at = DateTimeOffset.UtcNow }, HttpContext.RequestAborted);
                    continue;
                }
                if (!await read) break;
                while (reader.TryRead(out var value))
                    await WriteFrameAsync("watchweaver.event", value, HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { }
        finally
        {
            broadcaster.Unsubscribe(id);
            log.LogInformation("WatchWeaver event-stream client disconnected; subscriber_count={SubscriberCount}", broadcaster.SubscriberCount);
        }
    }

    private async Task WriteFrameAsync(string eventName, object value, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {eventName}\ndata: {JsonSerializer.Serialize(value)}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
