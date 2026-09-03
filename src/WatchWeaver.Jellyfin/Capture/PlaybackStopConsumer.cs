using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
namespace WatchWeaver.Jellyfin.Capture;
public sealed class PlaybackStopConsumer:IEventConsumer<PlaybackStopEventArgs>
{
    private readonly EventCorrelation _correlation;public PlaybackStopConsumer(EventCorrelation correlation)=>_correlation=correlation;
    public Task OnEvent(PlaybackStopEventArgs args){if(args.Item is null||!args.PlayedToCompletion||args.Users.Count==0)return Task.CompletedTask;foreach(var user in args.Users)WatchWeaverHostedService.Instance?.Capture(args.Item,user,args.Session,_correlation,"played");return Task.CompletedTask;}
}
