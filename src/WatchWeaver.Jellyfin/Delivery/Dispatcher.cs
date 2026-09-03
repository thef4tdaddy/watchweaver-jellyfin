using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WatchWeaver.Jellyfin.Protocol;
using WatchWeaver.Jellyfin.Queue;

namespace WatchWeaver.Jellyfin.Delivery;
public sealed class Dispatcher(HttpClient client,OutboundQueue queue,Func<(Uri? Url,string Token)> configuration)
{
    public async Task<bool> DeliverOneAsync(DateTimeOffset now,CancellationToken ct=default){var entry=await queue.NextAsync(now,ct);if(entry is null)return false;var (url,token)=configuration();if(url is null||string.IsNullOrWhiteSpace(token)){await queue.RetryAsync(entry.Event.EventId,now.AddMinutes(1),"not_configured",ct);return true;}using var req=new HttpRequestMessage(HttpMethod.Post,new Uri(url,"api/v1/ingest/jellyfin/events")){Content=JsonContent.Create(entry.Event)};req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);req.Headers.TryAddWithoutValidation("Idempotency-Key",entry.Event.EventId);req.Headers.UserAgent.ParseAdd("WatchWeaver-Jellyfin/0.1.0");try{using var res=await client.SendAsync(req,ct);if(res.IsSuccessStatusCode){await queue.DeliveredAsync(entry.Event.EventId,ct);return true;}var code=((int)res.StatusCode).ToString();if(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Conflict){await queue.DeadLetterAsync(entry.Event.EventId,"http_"+code,ct);return true;}var delay=RetryAfter(res,now)??Backoff(entry.Attempts);await queue.RetryAsync(entry.Event.EventId,now+delay,"http_"+code,ct);}catch(HttpRequestException){await queue.RetryAsync(entry.Event.EventId,now+Backoff(entry.Attempts),"network_error",ct);}return true;}
    internal static TimeSpan Backoff(int attempts)=>TimeSpan.FromSeconds(Math.Min(3600,Math.Pow(2,Math.Min(attempts,10))*5));
    internal static TimeSpan? RetryAfter(HttpResponseMessage r,DateTimeOffset now){var h=r.Headers.RetryAfter;if(h?.Delta is not null)return h.Delta;if(h?.Date is not null)return h.Date-now;return null;}
}
