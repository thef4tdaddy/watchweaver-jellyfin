using System.Text.Json;
namespace WatchWeaver.Jellyfin.Capture;
public sealed class ReconciliationState(string path)
{
    private readonly SemaphoreSlim _gate=new(1,1);private Dictionary<string,Observation> _items=new(StringComparer.OrdinalIgnoreCase);
    public async Task LoadAsync(CancellationToken ct=default){await _gate.WaitAsync(ct);try{if(!File.Exists(path))return;await using var stream=File.OpenRead(path);_items=await JsonSerializer.DeserializeAsync<Dictionary<string,Observation>>(stream,cancellationToken:ct)??new(StringComparer.OrdinalIgnoreCase);}finally{_gate.Release();}}
    public async Task<bool> ObserveAsync(string userId,string itemId,int playCount,DateTimeOffset? lastPlayedAt,DateTimeOffset now,TimeSpan lookback,CancellationToken ct=default){await _gate.WaitAsync(ct);try{var key=$"{userId}|{itemId}";var current=new Observation(playCount,lastPlayedAt);var changed=!_items.TryGetValue(key,out var previous)||current.PlayCount>previous.PlayCount||(current.LastPlayedAt.HasValue&&current.LastPlayedAt>previous.LastPlayedAt);_items[key]=current;await SaveAsync(ct);return changed&&lastPlayedAt.HasValue&&lastPlayedAt>=now.Subtract(lookback);}finally{_gate.Release();}}
    private async Task SaveAsync(CancellationToken ct){Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temporary=path+".tmp";await using(var stream=File.Create(temporary))await JsonSerializer.SerializeAsync(stream,_items,cancellationToken:ct);File.Move(temporary,path,true);}
    public sealed record Observation(int PlayCount,DateTimeOffset? LastPlayedAt);
}
