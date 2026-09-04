# WatchWeaver Jellyfin protocol v1

`POST {watchweaver_url}/api/v1/ingest/jellyfin/events` with `Authorization: Bearer …`, `Idempotency-Key` equal to `event_id`, and JSON content.

The envelope contains `schema_version: 1`, `event_id`, `event_type` (`played` or `marked_played`), `occurred_at`, nested `server`, `plugin`, `user`, `item`, and `playback` objects. Required item fields are `id`, `type`, and `title`; episodes also require `series_title`, `season_number`, `episode_number`, and stable series and season identity. The plugin sends Jellyfin `series_id` and `season_id` plus provider IDs when available; it never derives a show identity from an episode ID or title. `playback.played` must be true. Provider IDs use lowercase `tmdb`, `imdb`, and `tvdb` keys.

For television prompt decisions, `season_episode_count` is the number of released normal episodes visible to Jellyfin, `season_watched_episode_count` is the watched subset for the configured user, `season_future_episode_count` is the number with a future premiere date, and `latest_released_episode_number` is the greatest released episode number. These are library observations, not guesses about unaired episodes absent from Jellyfin metadata.

`202` accepts a new durable event; `200` acknowledges a duplicate. `400`, `401`, and `409` are permanent for that queued event. `429` and `5xx` retry. The receiver may include a JSON `code`; payloads and secrets are never included in diagnostics.
