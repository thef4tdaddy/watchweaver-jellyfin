using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using WatchWeaver.Jellyfin.Capture;
namespace WatchWeaver.Jellyfin;
public sealed class PluginServiceRegistrator:IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services,IServerApplicationHost host){services.AddSingleton<EventCorrelation>();services.AddScoped<IEventConsumer<PlaybackStopEventArgs>,PlaybackStopConsumer>();services.AddHostedService<WatchWeaverHostedService>();}
}
