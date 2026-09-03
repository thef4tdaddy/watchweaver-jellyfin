using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using WatchWeaver.Jellyfin.Configuration;
namespace WatchWeaver.Jellyfin;
public sealed class Plugin:BasePlugin<PluginConfiguration>,IHasWebPages
{
    public Plugin(IApplicationPaths paths,IXmlSerializer xml):base(paths,xml){Instance=this;}
    public static Plugin? Instance{get;private set;} public override string Name=>"WatchWeaver";public override Guid Id=>Guid.Parse("5f36de72-9df2-4a06-b5e7-d55fe8f50158");
    public IEnumerable<PluginPageInfo> GetPages()=>[new(){Name="watchweaver",EmbeddedResourcePath=GetType().Namespace+".Configuration.Web.config.html"},new(){Name="watchweaverjs",EmbeddedResourcePath=GetType().Namespace+".Configuration.Web.config.js"}];
}
