using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using WatchWeaver.Jellyfin.Capture;
using WatchWeaver.Jellyfin.Streaming;
namespace WatchWeaver.Jellyfin;
public sealed class PluginServiceRegistrator:IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services,IServerApplicationHost host){services.AddSingleton<EventCorrelation>();services.AddSingleton<EventBroadcaster>();services.AddScoped<IEventConsumer<PlaybackStopEventArgs>,PlaybackStopConsumer>();services.AddHostedService<WatchWeaverHostedService>();}
}
