using System.Security.Cryptography;
using System.Text;

namespace WatchWeaver.Jellyfin.Capture;
public sealed class EventCorrelation(TimeSpan? window=null)
{
    private readonly TimeSpan _window=window??TimeSpan.FromSeconds(30);private readonly Dictionary<string,(string Id,DateTimeOffset At)> _seen=[];
    public string GetEventId(string serverId,string userId,string itemId,int playCount,DateTimeOffset occurredAt,string signal){var key=$"{serverId}|{userId}|{itemId}|{playCount}";lock(_seen){if(_seen.TryGetValue(key,out var old)&&occurredAt-old.At<=_window)return old.Id;var seed=$"{key}|{occurredAt.UtcTicks}|{signal}";var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();_seen[key]=(id,occurredAt);foreach(var stale in _seen.Where(x=>occurredAt-x.Value.At>_window*4).Select(x=>x.Key).ToArray())_seen.Remove(stale);return id;}}
}
