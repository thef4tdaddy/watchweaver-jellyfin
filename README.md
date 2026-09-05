# WatchWeaver for Jellyfin

GPL-3.0-or-later Jellyfin plugin that sends completed movie and episode watches to WatchWeaver's durable protocol v1 receiver.

## Install from the Jellyfin catalog

In Jellyfin, open **Dashboard → Plugins → Repositories**, add a repository named **WatchWeaver**, and use:

`https://thef4tdaddy.github.io/watchweaver-jellyfin/manifest.json`

WatchWeaver will then appear in the plugin catalog and future compatible releases can be installed from Jellyfin without manually copying DLL files.

## Compatibility

| Jellyfin | Runtime | Build property | Target ABI |
|---|---|---|---|
| 10.11.x | .NET 9 | `JellyfinLine=10.11` (default) | `10.11.0.0` |
| 12.0.x | .NET 10 | `JellyfinLine=12.0` | `12.0.0.0` |

The 12.0 target is kept explicit and is validated in CI against prerelease packages until Jellyfin 12 is final. It does not change the WatchWeaver protocol.

## Configuration

Install the ABI-appropriate artifact, restart Jellyfin, then open Dashboard → Plugins → WatchWeaver. Enter the WatchWeaver base URL, paste the one-time ingestion token, select users from the checklist, save, and test the connection. A saved token can be tested without pasting it again. Selected users currently share one WatchWeaver history.

Private LAN/VPN destinations are allowed by default. Public destinations must use HTTPS. Tokens are write-only in the dashboard and are redacted from status and logs.

## Delivery behavior

Events are written to a bounded JSON queue before delivery. Transient failures, including `429` and `5xx`, retry with exponential backoff and `Retry-After`. Invalid/conflicting events enter a visible dead-letter list. Queue overflow is reported and the newest event is rejected rather than silently deleting older history. Playback stop and user-data signals are correlated into one stable event; real rewatches receive different event IDs.

A periodic recovery scan checks recent played-state changes for selected users and catches missed first-watch or mark-played transitions. Jellyfin exposes current played state rather than a complete rewatch ledger, so reconciliation intentionally does not manufacture historical rewatches.

## Development

```sh
dotnet test tests/WatchWeaver.Jellyfin.Tests/WatchWeaver.Jellyfin.Tests.csproj
dotnet build src/WatchWeaver.Jellyfin/WatchWeaver.Jellyfin.csproj -p:JellyfinLine=10.11
dotnet build src/WatchWeaver.Jellyfin/WatchWeaver.Jellyfin.csproj -p:JellyfinLine=12.0
```

Protocol details are mirrored in [`docs/protocol-v1.md`](docs/protocol-v1.md). No Jellyfin database access is used.
