using System.Net;using WatchWeaver.Jellyfin.Capture;using WatchWeaver.Jellyfin.Delivery;using WatchWeaver.Jellyfin.Protocol;using WatchWeaver.Jellyfin.Queue;
namespace WatchWeaver.Jellyfin.Tests;
public sealed class CoreTests
{
    private static EventEnvelope E(string id="one")=>new(1,id,"played",DateTimeOffset.Parse("2026-09-03T16:00:00Z"),new("s","10.11.0"),new("0.1.0","10.11.0.0"),new("u"),new("i","movie","Movie"),new(true));
    [Fact]public void OverlappingSignalsShareIdButRewatchDoesNot(){var c=new EventCorrelation(TimeSpan.FromSeconds(30));var at=DateTimeOffset.UtcNow;var a=c.GetEventId("s","u","i",1,at,"played");Assert.Equal(a,c.GetEventId("s","u","i",1,at.AddSeconds(2),"marked_played"));Assert.NotEqual(a,c.GetEventId("s","u","i",2,at.AddMinutes(5),"played"));}
    [Fact]public async Task QueueSurvivesRestartAndReportsOverflow(){var p=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");try{var q=new OutboundQueue(p,1);Assert.True(await q.EnqueueAsync(E()));Assert.False(await q.EnqueueAsync(E("two")));var q2=new OutboundQueue(p,1);await q2.LoadAsync();var s=await q2.StatusAsync();Assert.Equal(1,s.Pending);Assert.Equal(1,s.OverflowCount);}finally{File.Delete(p);}}
    [Fact]public async Task PermanentFailureDeadLettersWithoutLeakingToken(){var p=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");try{var q=new OutboundQueue(p);await q.EnqueueAsync(E());var handler=new Handler(_=>new(HttpStatusCode.BadRequest));var d=new Dispatcher(new HttpClient(handler),q,()=>(new Uri("http://127.0.0.1:1/"),"secret-token"));await d.DeliverOneAsync(DateTimeOffset.UtcNow);var s=await q.StatusAsync();Assert.Equal(0,s.Pending);Assert.Equal(1,s.DeadLetters);Assert.DoesNotContain("secret",s.LastErrorCode);}finally{File.Delete(p);}}
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> f):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>Task.FromResult(f(r));}
}
