using System.Text.Json;
namespace WatchWeaver.Jellyfin.Capture;
public sealed class ReconciliationState(string path)
{
    private readonly SemaphoreSlim _gate=new(1,1);private Dictionary<string,Observation> _items=new(StringComparer.OrdinalIgnoreCase);
    public async Task LoadAsync(CancellationToken ct=default){await _gate.WaitAsync(ct);try{if(!File.Exists(path))return;await using var stream=File.OpenRead(path);var loaded=await JsonSerializer.DeserializeAsync<Dictionary<string,Observation>>(stream,cancellationToken:ct)??[];_items=loaded.GroupBy(x=>NormalizeStoredKey(x.Key),StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>x.OrderByDescending(v=>v.Value.PlayCount).ThenByDescending(v=>v.Value.LastPlayedAt).First().Value,StringComparer.OrdinalIgnoreCase);}finally{_gate.Release();}}
    public async Task<bool> ShouldCaptureAsync(string userId,string itemId,int playCount,DateTimeOffset? lastPlayedAt,DateTimeOffset now,TimeSpan lookback,CancellationToken ct=default){await _gate.WaitAsync(ct);try{var key=Key(userId,itemId);var current=new Observation(playCount,lastPlayedAt);var changed=!_items.TryGetValue(key,out var previous)||current.PlayCount>previous.PlayCount||(current.LastPlayedAt.HasValue&&current.LastPlayedAt>previous.LastPlayedAt);return changed&&lastPlayedAt.HasValue&&lastPlayedAt>=now.Subtract(lookback);}finally{_gate.Release();}}
    public async Task RecordAsync(string userId,string itemId,int playCount,DateTimeOffset? lastPlayedAt,CancellationToken ct=default){await _gate.WaitAsync(ct);try{_items[Key(userId,itemId)]=new(playCount,lastPlayedAt);await SaveAsync(ct);}finally{_gate.Release();}}
    private static string Key(string userId,string itemId)=>$"{NormalizeUserId(userId)}|{itemId}";
    private static string NormalizeStoredKey(string key){var split=key.IndexOf('|');return split<0?key:Key(key[..split],key[(split+1)..]);}
    private static string NormalizeUserId(string value)=>Guid.TryParse(value,out var id)?id.ToString("N"):value;
    private async Task SaveAsync(CancellationToken ct){Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temporary=path+".tmp";await using(var stream=File.Create(temporary))await JsonSerializer.SerializeAsync(stream,_items,cancellationToken:ct);File.Move(temporary,path,true);}
    public sealed record Observation(int PlayCount,DateTimeOffset? LastPlayedAt);
}
