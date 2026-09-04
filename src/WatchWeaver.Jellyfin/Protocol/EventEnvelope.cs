using System.Text.Json.Serialization;

namespace WatchWeaver.Jellyfin.Protocol;
public sealed record ServerInfo([property:JsonPropertyName("id")] string Id,[property:JsonPropertyName("version")] string Version);
public sealed record PluginInfo([property:JsonPropertyName("version")] string Version,[property:JsonPropertyName("target_abi")] string TargetAbi);
public sealed record UserInfo([property:JsonPropertyName("id")] string Id,[property:JsonPropertyName("name")] string? Name=null);
public sealed record ItemInfo(
    [property:JsonPropertyName("id")] string Id,[property:JsonPropertyName("type")] string Type,[property:JsonPropertyName("title")] string Title,
    [property:JsonPropertyName("year")] int? Year=null,[property:JsonPropertyName("series_title")] string? SeriesTitle=null,
    [property:JsonPropertyName("series_id")] string? SeriesId=null,[property:JsonPropertyName("season_id")] string? SeasonId=null,
    [property:JsonPropertyName("season_number")] int? SeasonNumber=null,[property:JsonPropertyName("episode_number")] int? EpisodeNumber=null,
    [property:JsonPropertyName("provider_ids")] IReadOnlyDictionary<string,string>? ProviderIds=null,
    [property:JsonPropertyName("series_provider_ids")] IReadOnlyDictionary<string,string>? SeriesProviderIds=null,
    [property:JsonPropertyName("season_provider_ids")] IReadOnlyDictionary<string,string>? SeasonProviderIds=null,
    [property:JsonPropertyName("episode_type")] string? EpisodeType=null,
    [property:JsonPropertyName("season_episode_count")] int? SeasonEpisodeCount=null,
    [property:JsonPropertyName("season_watched_episode_count")] int? SeasonWatchedEpisodeCount=null,
    [property:JsonPropertyName("season_future_episode_count")] int? SeasonFutureEpisodeCount=null,
    [property:JsonPropertyName("latest_released_episode_number")] int? LatestReleasedEpisodeNumber=null);
public sealed record PlaybackInfo(
    [property:JsonPropertyName("played")] bool Played,[property:JsonPropertyName("position_ticks")] long? PositionTicks=null,
    [property:JsonPropertyName("runtime_ticks")] long? RuntimeTicks=null,[property:JsonPropertyName("progress_percent")] double? ProgressPercent=null,
    [property:JsonPropertyName("play_count")] int? PlayCount=null,[property:JsonPropertyName("client")] string? Client=null,[property:JsonPropertyName("device")] string? Device=null);
public sealed record EventEnvelope(
    [property:JsonPropertyName("schema_version")] int SchemaVersion,[property:JsonPropertyName("event_id")] string EventId,
    [property:JsonPropertyName("event_type")] string EventType,[property:JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt,
    [property:JsonPropertyName("server")] ServerInfo Server,[property:JsonPropertyName("plugin")] PluginInfo Plugin,
    [property:JsonPropertyName("user")] UserInfo User,[property:JsonPropertyName("item")] ItemInfo Item,
    [property:JsonPropertyName("playback")] PlaybackInfo Playback);
