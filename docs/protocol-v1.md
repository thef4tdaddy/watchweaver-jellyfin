# WatchWeaver Jellyfin protocol v1

`POST {watchweaver_url}/api/v1/ingest/jellyfin/events` with `Authorization: Bearer …`, `Idempotency-Key` equal to `event_id`, and JSON content.

The envelope contains `schema_version: 1`, `event_id`, `event_type` (`played` or `marked_played`), `occurred_at`, nested `server`, `plugin`, `user`, `item`, and `playback` objects. Required item fields are `id`, `type`, and `title`; episodes also require `series_title`, `season_number`, and `episode_number`. `playback.played` must be true. Provider IDs use lowercase `tmdb`, `imdb`, and `tvdb` keys. `series_provider_ids` may identify an episode's series.

`202` accepts a new durable event; `200` acknowledges a duplicate. `400`, `401`, and `409` are permanent for that queued event. `429` and `5xx` retry. The receiver may include a JSON `code`; payloads and secrets are never included in diagnostics.
