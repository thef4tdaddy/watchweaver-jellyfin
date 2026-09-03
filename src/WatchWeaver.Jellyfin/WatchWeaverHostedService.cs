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
public sealed class WatchWeaverHostedService:BackgroundService
{
    private readonly IServerApplicationHost _host;private readonly IUserDataManager _userData;private readonly ILogger<WatchWeaverHostedService> _log;private OutboundQueue? _queue;private Dispatcher? _dispatcher;private readonly EventCorrelation _correlation;public static WatchWeaverHostedService? Instance{get;private set;}
    public WatchWeaverHostedService(IServerApplicationHost host,IUserDataManager userData,EventCorrelation correlation,ILogger<WatchWeaverHostedService> log){_host=host;_userData=userData;_correlation=correlation;_log=log;Instance=this;}
    public override async Task StartAsync(CancellationToken ct){var folder=Path.Combine(_host.ApplicationPaths.PluginConfigurationsPath,"watchweaver");_queue=new(Path.Combine(folder,"outbound-queue.json"),Plugin.Instance?.Configuration.QueueCapacity??10000);await _queue.LoadAsync(ct);_dispatcher=new(new HttpClient{Timeout=TimeSpan.FromSeconds(20)},_queue,Configuration);_userData.UserDataSaved+=OnUserDataSaved;await base.StartAsync(ct);}
    public override Task StopAsync(CancellationToken ct){_userData.UserDataSaved-=OnUserDataSaved;return base.StopAsync(ct);}
    protected override async Task ExecuteAsync(CancellationToken ct){while(!ct.IsCancellationRequested){try{if(_dispatcher is null||!await _dispatcher.DeliverOneAsync(DateTimeOffset.UtcNow,ct))await Task.Delay(TimeSpan.FromSeconds(5),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver delivery cycle failed without exposing event data");await Task.Delay(TimeSpan.FromSeconds(15),ct);}}}
    private void OnUserDataSaved(object? sender,UserDataSaveEventArgs e){try{if(e.Item is not null&&e.UserData.Played&&e.SaveReason.ToString().Contains("UpdateUserRating",StringComparison.OrdinalIgnoreCase))Capture(e.Item,new global::Jellyfin.Database.Implementations.Entities.User{Id=e.UserId},null,_correlation,"marked_played");}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver manual watched-state capture failed");}}
    public async void Capture(BaseItem item,global::Jellyfin.Database.Implementations.Entities.User user,SessionInfo? session,EventCorrelation correlation,string type){try{var cfg=Plugin.Instance?.Configuration;if(cfg is null||_queue is null||!cfg.AllowedUserIds.Contains(user.Id.ToString(),StringComparer.OrdinalIgnoreCase))return;var data=_userData.GetUserData(user,item);var now=DateTimeOffset.UtcNow;var eventId=correlation.GetEventId(_host.SystemId,user.Id.ToString(),item.Id.ToString(),data.PlayCount,now,type);var isEpisode=item is MediaBrowser.Controller.Entities.TV.Episode;var episode=item as MediaBrowser.Controller.Entities.TV.Episode;var providers=item.ProviderIds.ToDictionary(x=>x.Key.ToLowerInvariant(),x=>x.Value);var envelope=new EventEnvelope(1,eventId,type,now,new(_host.SystemId,_host.ApplicationVersionString),new(typeof(Plugin).Assembly.GetName().Version?.ToString()??"0.1.0",TargetAbi()),new(user.Id.ToString(),user.Username),new(item.Id.ToString(),isEpisode?"episode":"movie",item.Name,item.ProductionYear,episode?.SeriesName,episode?.ParentIndexNumber,episode?.IndexNumber,providers),new(true,data.PlaybackPositionTicks,item.RunTimeTicks,item.RunTimeTicks>0?100d*data.PlaybackPositionTicks/item.RunTimeTicks:data.Played?100:0,data.PlayCount,session?.Client,session?.DeviceName));if(!await _queue.EnqueueAsync(envelope))_log.LogError("WatchWeaver outbound queue is full; event was not accepted into the queue");}catch(Exception ex){_log.LogWarning(ex,"WatchWeaver event capture failed without exposing event data");}}
    private static(string Token,Uri? Url) Raw(){var c=Plugin.Instance?.Configuration;return(c?.ConnectionToken??"",Uri.TryCreate(c?.WatchWeaverUrl,UriKind.Absolute,out var u)?u:null);}private static(Uri? Url,string Token) Configuration(){var x=Raw();return(x.Url,x.Token);}private static string TargetAbi()=>Environment.Version.Major>=10?"12.0.0.0":"10.11.0.0";
}
