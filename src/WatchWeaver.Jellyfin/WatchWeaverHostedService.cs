using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
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
    private readonly ILogger<WatchWeaverHostedService> _log; private readonly EventCorrelation _correlation;
    private OutboundQueue? _queue; private Dispatcher? _dispatcher;
    public static WatchWeaverHostedService? Instance { get; private set; }
    public WatchWeaverHostedService(IServerApplicationHost host,IApplicationPaths paths,IUserDataManager userData,IUserManager users,EventCorrelation correlation,ILogger<WatchWeaverHostedService> log)
    { _host=host;_paths=paths;_userData=userData;_users=users;_correlation=correlation;_log=log;Instance=this; }
    public override async Task StartAsync(CancellationToken ct)
    { var folder=Path.Combine(_paths.PluginConfigurationsPath,"watchweaver");_queue=new(Path.Combine(folder,"outbound-queue.json"),Plugin.Instance?.Configuration.QueueCapacity??10000);await _queue.LoadAsync(ct);_dispatcher=new(new HttpClient{Timeout=TimeSpan.FromSeconds(20)},_queue,Configuration);_userData.UserDataSaved+=OnUserDataSaved;await base.StartAsync(ct); }
    public override Task StopAsync(CancellationToken ct){_userData.UserDataSaved-=OnUserDataSaved;return base.StopAsync(ct);}
    protected override async Task ExecuteAsync(CancellationToken ct)
    { while(!ct.IsCancellationRequested){try{if(_dispatcher is null||!await _dispatcher.DeliverOneAsync(DateTimeOffset.UtcNow,ct))await Task.Delay(TimeSpan.FromSeconds(5),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver delivery cycle failed without exposing event data");await Task.Delay(TimeSpan.FromSeconds(15),ct);}} }
    private void OnUserDataSaved(object? sender,UserDataSaveEventArgs e)
    { try{var user=_users.GetUserById(e.UserId);if(e.Item is not null&&user is not null&&e.UserData.Played&&e.SaveReason.ToString().Contains("UpdateUserRating",StringComparison.OrdinalIgnoreCase))Capture(e.Item,user,null,_correlation,"marked_played");}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver manual watched-state capture failed");} }
    public async void Capture(BaseItem item,global::Jellyfin.Database.Implementations.Entities.User user,SessionInfo? session,EventCorrelation correlation,string type)
    { try{var cfg=Plugin.Instance?.Configuration;var queue=_queue;if(cfg is null||queue is null||!cfg.AllowedUserIds.Contains(user.Id.ToString(),StringComparer.OrdinalIgnoreCase))return;var data=_userData.GetUserData(user,item);if(data is null)return;var now=DateTimeOffset.UtcNow;var eventId=correlation.GetEventId(_host.SystemId,user.Id.ToString(),item.Id.ToString(),data.PlayCount,now,type);var episode=item as MediaBrowser.Controller.Entities.TV.Episode;var providers=(item.ProviderIds??new Dictionary<string,string>()).Where(x=>x.Value is not null).ToDictionary(x=>x.Key.ToLowerInvariant(),x=>x.Value!);var runtime=item.RunTimeTicks.GetValueOrDefault();var progress=runtime>0?100d*data.PlaybackPositionTicks/runtime:data.Played?100:0;var envelope=new EventEnvelope(1,eventId,type,now,new(_host.SystemId,_host.ApplicationVersionString),new(typeof(Plugin).Assembly.GetName().Version?.ToString()??"0.1.0",TargetAbi()),new(user.Id.ToString(),user.Username),new(item.Id.ToString(),episode is null?"movie":"episode",item.Name??"Unknown",item.ProductionYear,episode?.SeriesName,episode?.ParentIndexNumber,episode?.IndexNumber,providers),new(true,data.PlaybackPositionTicks,item.RunTimeTicks,progress,data.PlayCount,session?.Client,session?.DeviceName));if(!await queue.EnqueueAsync(envelope))_log.LogError("WatchWeaver outbound queue is full; event was not accepted into the queue");}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver event capture failed without exposing event data");} }
    private static (Uri? Url,string Token) Configuration(){var c=Plugin.Instance?.Configuration;return(Uri.TryCreate(c?.WatchWeaverUrl,UriKind.Absolute,out var u)?u:null,c?.ConnectionToken??"");}
    private static string TargetAbi()=>Environment.Version.Major>=10?"12.0.0.0":"10.11.0.0";
}
