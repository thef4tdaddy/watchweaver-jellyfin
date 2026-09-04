using System.Text.Json;
using WatchWeaver.Jellyfin.Protocol;

namespace WatchWeaver.Jellyfin.Queue;
public sealed record QueueEntry(EventEnvelope Event,int Attempts,DateTimeOffset NextAttempt,string? LastCode=null);
public sealed record DeadLetter(EventEnvelope Event,string Code,DateTimeOffset FailedAt);
public sealed record QueueStatus(int Pending,int DeadLetters,long OverflowCount,DateTimeOffset? OldestPendingAt,DateTimeOffset? LastDeliveredAt,string? LastErrorCode);
public sealed class OutboundQueue
{
    private readonly string _path; private readonly int _capacity; private readonly SemaphoreSlim _gate=new(1,1);
    private State _state=new();
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    public OutboundQueue(string path,int capacity=10000){_path=path;_capacity=capacity;}
    public async Task LoadAsync(CancellationToken ct=default){await _gate.WaitAsync(ct);try{if(File.Exists(_path)){await using var s=File.OpenRead(_path);_state=await JsonSerializer.DeserializeAsync<State>(s,Json,ct)??new();}}finally{_gate.Release();}}
    public async Task<bool> EnqueueAsync(EventEnvelope value,CancellationToken ct=default){await _gate.WaitAsync(ct);try{if(_state.Pending.Any(x=>x.Event.EventId==value.EventId))return true;if(_state.Pending.Count>=_capacity){_state.OverflowCount++;await Save(ct);return false;}_state.Pending.Add(new(value,0,DateTimeOffset.UtcNow));await Save(ct);return true;}finally{_gate.Release();}}
    public async Task<QueueEntry?> NextAsync(DateTimeOffset now,CancellationToken ct=default){await _gate.WaitAsync(ct);try{return _state.Pending.Where(x=>x.NextAttempt<=now).OrderBy(x=>x.NextAttempt).FirstOrDefault();}finally{_gate.Release();}}
    public Task DeliveredAsync(string id,CancellationToken ct=default)=>Mutate(id,e=>{_state.Pending.Remove(e);_state.LastDeliveredAt=DateTimeOffset.UtcNow;_state.LastErrorCode=null;},ct);
    public Task RetryAsync(string id,DateTimeOffset next,string code,CancellationToken ct=default)=>Mutate(id,e=>{var i=_state.Pending.IndexOf(e);_state.Pending[i]=e with{Attempts=e.Attempts+1,NextAttempt=next,LastCode=code};_state.LastErrorCode=code;},ct);
    public Task DeadLetterAsync(string id,string code,CancellationToken ct=default)=>Mutate(id,e=>{_state.Pending.Remove(e);_state.DeadLetters.Add(new(e.Event,code,DateTimeOffset.UtcNow));_state.LastErrorCode=code;},ct);
    public async Task<QueueStatus> StatusAsync(CancellationToken ct=default){await _gate.WaitAsync(ct);try{return new(_state.Pending.Count,_state.DeadLetters.Count,_state.OverflowCount,_state.Pending.Count==0?null:_state.Pending.Min(x=>x.Event.OccurredAt),_state.LastDeliveredAt,_state.LastErrorCode);}finally{_gate.Release();}}
    private async Task Mutate(string id,Action<QueueEntry> action,CancellationToken ct){await _gate.WaitAsync(ct);try{var e=_state.Pending.FirstOrDefault(x=>x.Event.EventId==id);if(e is not null){action(e);await Save(ct);}}finally{_gate.Release();}}
    private async Task Save(CancellationToken ct){Directory.CreateDirectory(Path.GetDirectoryName(_path)!);var tmp=_path+".tmp";await using(var s=File.Create(tmp)){await JsonSerializer.SerializeAsync(s,_state,Json,ct);await s.FlushAsync(ct);}File.Move(tmp,_path,true);}
    public sealed class State{public List<QueueEntry> Pending{get;set;}=[];public List<DeadLetter> DeadLetters{get;set;}=[];public long OverflowCount{get;set;}public DateTimeOffset? LastDeliveredAt{get;set;}public string? LastErrorCode{get;set;}}
}
