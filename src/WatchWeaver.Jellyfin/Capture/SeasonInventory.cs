namespace WatchWeaver.Jellyfin.Capture;

public sealed record SeasonEpisodeObservation(int Number, bool Future, bool Watched);

public sealed record SeasonInventory(int ReleasedCount, int WatchedReleasedCount, int FutureCount, int? LatestReleasedEpisodeNumber)
{
    public static SeasonInventory From(IEnumerable<SeasonEpisodeObservation> episodes)
    {
        var materialized = episodes.ToArray();
        var released = materialized.Where(x => !x.Future).ToArray();
        return new SeasonInventory(
            released.Length,
            released.Count(x => x.Watched),
            materialized.Count(x => x.Future),
            released.Length == 0 ? null : released.Max(x => x.Number));
    }
}
