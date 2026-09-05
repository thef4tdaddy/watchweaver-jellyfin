using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WatchWeaver.Jellyfin.Capture;
using WatchWeaver.Jellyfin.Delivery;
using WatchWeaver.Jellyfin.Protocol;
using WatchWeaver.Jellyfin.Queue;

namespace WatchWeaver.Jellyfin;
public sealed class WatchWeaverHostedService : BackgroundService
{
    private readonly IServerApplicationHost _host; private readonly IApplicationPaths _paths;
    private readonly IUserDataManager _userData; private readonly IUserManager _users;
    private readonly ILibraryManager _library;
    private readonly ILogger<WatchWeaverHostedService> _log; private readonly EventCorrelation _correlation;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private OutboundQueue? _queue; private Dispatcher? _dispatcher; private ReconciliationState? _reconciliation;
    public static WatchWeaverHostedService? Instance { get; private set; }
    public WatchWeaverHostedService(IServerApplicationHost host,IApplicationPaths paths,IUserDataManager userData,IUserManager users,ILibraryManager library,EventCorrelation correlation,ILogger<WatchWeaverHostedService> log)
    { _host=host;_paths=paths;_userData=userData;_users=users;_library=library;_correlation=correlation;_log=log;Instance=this; }
    public override async Task StartAsync(CancellationToken ct)
    { var folder=Path.Combine(_paths.PluginConfigurationsPath,"watchweaver");_queue=new(Path.Combine(folder,"outbound-queue.json"),Plugin.Instance?.Configuration.QueueCapacity??10000);await _queue.LoadAsync(ct);_reconciliation=new(Path.Combine(folder,"reconciliation-state.json"));await _reconciliation.LoadAsync(ct);_dispatcher=new(new HttpClient{Timeout=TimeSpan.FromSeconds(20)},_queue,Configuration);_userData.UserDataSaved+=OnUserDataSaved;await base.StartAsync(ct); }
    public override Task StopAsync(CancellationToken ct){_userData.UserDataSaved-=OnUserDataSaved;return base.StopAsync(ct);}
    protected override async Task ExecuteAsync(CancellationToken ct)
    { var nextProbe=DateTimeOffset.MinValue;var nextReconciliation=DateTimeOffset.UtcNow.AddMinutes(1);while(!ct.IsCancellationRequested){try{var now=DateTimeOffset.UtcNow;if(_dispatcher is not null&&now>=nextProbe){await _dispatcher.ProbeAsync(_host.ApplicationVersionString,typeof(Plugin).Assembly.GetName().Version?.ToString()??"0.1.0",ct);nextProbe=now.AddMinutes(5);}if(now>=nextReconciliation){await ReconcileAsync(now,ct);nextReconciliation=now.AddMinutes(Math.Clamp(Plugin.Instance?.Configuration.ReconciliationIntervalMinutes??15,5,1440));}if(_dispatcher is null||!await _dispatcher.DeliverOneAsync(now,ct))await _wake.WaitAsync(TimeSpan.FromSeconds(5),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver delivery cycle failed without exposing event data");await Task.Delay(TimeSpan.FromSeconds(15),ct);}} }
    private async Task ReconcileAsync(DateTimeOffset now,CancellationToken ct)
    {
        var cfg=Plugin.Instance?.Configuration;var state=_reconciliation;if(cfg is null||state is null)return;var lookback=TimeSpan.FromHours(Math.Clamp(cfg.ReconciliationLookbackHours,1,168));
        foreach(var userId in cfg.AllowedUserIds){if(!Guid.TryParse(userId,out var id))continue;var user=_users.GetUserById(id);if(user is null)continue;var items=_library.GetItemList(new InternalItemsQuery(user){Recursive=true,IsPlayed=true,IncludeItemTypes=[BaseItemKind.Movie,BaseItemKind.Episode],MinDateLastSavedForUser=now.Subtract(lookback).UtcDateTime});foreach(var item in items){var data=_userData.GetUserData(user,item);if(data?.Played!=true)continue;var last=AsUtc(data.LastPlayedDate);if(await state.ObserveAsync(userId,item.Id.ToString(),data.PlayCount,last,now,lookback,ct))await CaptureAsync(item,user,null,_correlation,"reconciled",last??now,ct);}}
    }
    private void OnUserDataSaved(object? sender,UserDataSaveEventArgs e)
    { try{var user=_users.GetUserById(e.UserId);if(e.Item is not null&&user is not null&&IsManualPlayedChange(e.SaveReason.ToString(),e.UserData.Played))Capture(e.Item,user,null,_correlation,"marked_played");}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver manual watched-state capture failed");} }
    internal static bool IsManualPlayedChange(string reason,bool played)=>played&&string.Equals(reason,"TogglePlayed",StringComparison.Ordinal);
    public async void Capture(BaseItem item,global::Jellyfin.Database.Implementations.Entities.User user,SessionInfo? session,EventCorrelation correlation,string type)=>await CaptureAsync(item,user,session,correlation,type,DateTimeOffset.UtcNow,CancellationToken.None);
    private async Task CaptureAsync(BaseItem item,global::Jellyfin.Database.Implementations.Entities.User user,SessionInfo? session,EventCorrelation correlation,string type,DateTimeOffset now,CancellationToken ct)
    {
        try
        {
            var cfg=Plugin.Instance?.Configuration;var queue=_queue;
            if(cfg is null||queue is null||!cfg.AllowedUserIds.Contains(user.Id.ToString(),StringComparer.OrdinalIgnoreCase))return;
            var data=_userData.GetUserData(user,item);if(data is null)return;
            var eventId=correlation.GetEventId(_host.SystemId,user.Id.ToString(),item.Id.ToString(),data.PlayCount,now,type);
            var runtime=item.RunTimeTicks.GetValueOrDefault();
            var progress=runtime>0?100d*data.PlaybackPositionTicks/runtime:data.Played?100:0;
            var envelope=new EventEnvelope(1,eventId,type,now,new(_host.SystemId,_host.ApplicationVersionString),new(typeof(Plugin).Assembly.GetName().Version?.ToString()??"0.1.0",TargetAbi()),new(user.Id.ToString(),user.Username),BuildItem(item,user,now),new(true,data.PlaybackPositionTicks,item.RunTimeTicks,progress,data.PlayCount,session?.Client,session?.DeviceName));
            if(!await queue.EnqueueAsync(envelope))_log.LogError("WatchWeaver outbound queue is full; event was not accepted into the queue");
            else {if(_reconciliation is not null)await _reconciliation.ObserveAsync(user.Id.ToString(),item.Id.ToString(),data.PlayCount,AsUtc(data.LastPlayedDate),DateTimeOffset.UtcNow,TimeSpan.FromDays(7),ct);if(_wake.CurrentCount==0)_wake.Release();}
        }
        catch(Exception ex){_log.LogWarning(ex,"WatchWeaver event capture failed without exposing event data");}
    }
    private ItemInfo BuildItem(BaseItem item,global::Jellyfin.Database.Implementations.Entities.User user,DateTimeOffset now)
    {
        var providers=ProviderIds(item);
        if(item is not Episode episode)return new(item.Id.ToString(),"movie",item.Name??"Unknown",item.ProductionYear,ProviderIds:providers);
        var series=episode.Series;var season=episode.Season;
        var seriesId=episode.SeriesId==Guid.Empty?episode.FindSeriesId():episode.SeriesId;
        var seasonId=episode.SeasonId==Guid.Empty?episode.FindSeasonId():episode.SeasonId;
        var inventory=season is null?null:BuildSeasonInventory(season.GetEpisodes().OfType<Episode>(),user,now);
        return new(episode.Id.ToString(),"episode",episode.Name??"Unknown",episode.ProductionYear,episode.SeriesName,
            seriesId==Guid.Empty?null:seriesId.ToString(),seasonId==Guid.Empty?null:seasonId.ToString(),episode.ParentIndexNumber,episode.IndexNumber,
            providers,series is null?null:ProviderIds(series),season is null?null:ProviderIds(season),
            SeasonEpisodeCount:inventory?.ReleasedCount,SeasonWatchedEpisodeCount:inventory?.WatchedReleasedCount,
            SeasonFutureEpisodeCount:inventory?.FutureCount,LatestReleasedEpisodeNumber:inventory?.LatestReleasedEpisodeNumber);
    }
    private SeasonInventory BuildSeasonInventory(IEnumerable<Episode> episodes,global::Jellyfin.Database.Implementations.Entities.User user,DateTimeOffset now)
    {
        var observations=episodes.Where(x=>x.IndexNumber is >0).Select(x=>new SeasonEpisodeObservation(
            x.IndexNumber!.Value,x.PremiereDate.HasValue&&x.PremiereDate.Value.ToUniversalTime()>now.UtcDateTime,
            _userData.GetUserData(user,x)?.Played==true));
        return SeasonInventory.From(observations);
    }
    private static IReadOnlyDictionary<string,string> ProviderIds(BaseItem item)=>(item.ProviderIds??new Dictionary<string,string>())
        .Where(x=>!string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x=>x.Key.ToLowerInvariant(),x=>x.Value);
    private static (Uri? Url,string Token) Configuration(){var c=Plugin.Instance?.Configuration;return(Uri.TryCreate(c?.WatchWeaverUrl,UriKind.Absolute,out var u)?u:null,c?.ConnectionToken??"");}
    private static string TargetAbi()=>Environment.Version.Major>=10?"12.0.0.0":"10.11.0.0";
    private static DateTimeOffset? AsUtc(DateTime? value)=>value.HasValue?new DateTimeOffset(DateTime.SpecifyKind(value.Value,DateTimeKind.Utc)):null;
}
