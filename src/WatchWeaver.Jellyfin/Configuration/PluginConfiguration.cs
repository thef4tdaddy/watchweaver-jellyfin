using MediaBrowser.Model.Plugins;
namespace WatchWeaver.Jellyfin.Configuration;
public sealed class PluginConfiguration:BasePluginConfiguration
{
    public string WatchWeaverUrl{get;set;}="";
    public string ConnectionToken{get;set;}="";
    public string[] AllowedUserIds{get;set;}=[];
    public int QueueCapacity{get;set;}=10000;
    public string RedactedToken=>string.IsNullOrEmpty(ConnectionToken)?"":"••••••••";
}
