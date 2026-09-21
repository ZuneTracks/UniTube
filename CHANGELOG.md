# Changelog

## Unreleased - Windows 10 Mobile Store install fix

- Fixed the Store packaging wizard producing an ARM-only `.appxbundle` even
  when both ARM and x64 were selected. The `Store|ARM` and `Store|x64`
  configurations pinned `AppxBundlePlatforms` to their own single platform,
  which overrode the project-wide `x86|x64|arm` default and silently discarded
  the architecture selection. Both pins are removed so the selection governs.

- Fixed Store installs terminating immediately after the splash screen on
  Windows 10 Mobile. The package declared a dependency on
  `Microsoft.VCLibs.140.00` with `MinVersion 14.0.33519.0`, which Mobile can
  never provision because it stopped receiving framework-package updates at OS
  build 15254 (Store provisions VCLibs only up to ~`14.0.24217.0`).
- Upgraded `Microsoft.NETCore.UniversalWindowsPlatform` from `5.2.9` to
  `6.2.14` and enabled `UseDotNetNativeToolchain` for non-Debug configurations
  so the .NET Native 1.7 toolchain is used instead of the in-box Windows SDK
  ILC fallback. The package now declares `Microsoft.VCLibs.140.00`
  `14.0.22929.0` with `Microsoft.NET.Native.Framework.1.7` and
  `Microsoft.NET.Native.Runtime.1.7`.
- `TargetPlatformMinVersion` remains `10.0.15063.0`, so Windows 10 Mobile
  support is retained.
- Removed the `ExcludeSdkMetadataFromStorePackage` target, which stripped
  `WinMetadata\Windows.winmd` from Store upload packages. The Store recompiles
  uploaded MSIL in the cloud and cannot resolve Windows Runtime types without
  that file.

## v1.6.7.6 - Region selector release

- Replaced the editable two-letter region code with a selector that lists
  countries supported by YouTube Data API v3.
- Detects the device region, selects it by default, and places it first in the
  bounded country list. United States is used if the device region is not
  supported.
- Uses the selected country for Trending Now, category choices, popular
  category videos, and live-tile regional metadata.
- Updated the package version to `1.6.7.6`.

## v1.6.7.0 - Safe Mode release

- Added a Settings **Safe Mode** toggle, enabled by default, that uses YouTube
  Data API v3 strict SafeSearch for public video searches.
- Documented Safe Mode's scope: it is not a YouTube Kids integration and does
  not filter popular, category, or authenticated account feeds.
- Moved **About** to the top of Settings and **View Diagnostics** to the bottom.
- Added x64 Debug, Release, and Store build configurations alongside the
  existing ARM configurations.
- Updated the package version to `1.6.7.0`.

## v1.6.6.0 - UniTube rebrand

- Renamed the app's public display name and documentation to **UniTube**.
- Updated the development publisher identity to `CN=ZuneTracks`.
- Added full-size, resolution-qualified UniTube tile assets for small, medium,
  wide, and large tiles.

## v1.6.5.0 - User Profile release

- Added an authenticated **Profile** pivot with the authorized channel summary,
  subscriptions, playlists, playlist-video browsing, and liked-video reads.
- Added loading, error, and next-page states for authenticated collections.
- Clearly labels Watch History and Watch Later as unavailable because YouTube Data
  API v3 does not expose them as readable collections.
- Device authorization now requests the existing `youtube.upload` scope plus the
  minimum `youtube.readonly` scope required for account reads. Existing tokens
  must be authorized again after this change.
- Hardened profile loading against unexpected API value types and surfaced
  stage-specific errors in the pivot instead of allowing an async UI exception
  to terminate the app.
- Moved the Upload Video action into the Profile pivot beside Refresh Profile and
  removed the redundant Home pivot Search action.
- Added the authorized channel's uploaded videos through its YouTube-provided
  uploads playlist, including paginated loading.
- Made Uploaded Videos, Subscriptions, Playlists, Playlist Videos, and Liked
  Videos collapsible while keeping Account Summary and unsupported-data guidance
  exposed.
- Collapsed those five data sections by default and kept the Playlist Videos
  heading visible before a playlist is selected.

## v1.6.0.0 - ARM uploader release

- Published the current uploader, device authorization, and local developer
  deployment updates as an ARM Developer Mode sideload release.
- Uses an atomic OAuth credential pair: a complete saved pair takes precedence,
  otherwise a complete local build-default pair is used. Incomplete saved values
  cannot be combined with embedded defaults.
- Added secret-safe OAuth diagnostics that identify only the active configuration
  source and credential lengths.

## v1.5.1.0 - Device authorization and developer deployment

- Added a live device-code countdown, server-timed polling status, visible
  `slow_down` handling, automatic code renewal after expiration, and explicit
  `invalid_client` feedback.
- Clarified the Settings device sign-in and sign-out states, and keeps managed
  local build defaults hidden from editable fields.
- Added an ignored local Visual Studio 2017 package-signing configuration
  template for physical-device debugging builds.

## v1.5.0.0 - Video upload prototype

- Replaced the upload prototype's custom-redirect PKCE sign-in with Google's OAuth
  2.0 device authorization flow for TVs and limited-input devices.
- Removed the redirect URI and protocol-handler setup. The app now asks only for a
  limited-input device client ID and secret, shows Google's verification URL and
  user code, and waits for authorization on another device.
- Kept access and refresh tokens in Windows Credential Locker, preserved foreground
  cancellation, and keeps the user-supplied client secret in Credential Locker.
- Added a persistent, secret-safe diagnostics page for application exceptions and
  OAuth/Credential Locker milestones. The log remains available after a restart
  and can be saved as a text file.
- Restored the Settings **About** flyout from the recovered 1.0.9.0 source.
- Retained the existing pinned live-tile refresh behavior.

## v1.0.9.0 - Store packaging preparation and navigation fix

- Added an explicit ARM Store-upload configuration that creates an unsigned
  `.appxupload` candidate with public symbols.
- Added a build-time guard requiring Visual Studio Store association metadata
  before Store packaging.
- Updated the package version to use a Store-compatible fourth version field of
  `0`.
- Preserved the Search pivot and its prior results when navigating Search ->
  video -> Back by caching `MainPage`; Home and Categories video navigation
  behavior is unchanged.

## v1.0.8.4 - Live tile refresh fix

- Fixed the live-tile regression where a previously pinned app tile would not
  reflect the most recently played video after returning to the Start screen.
- Kept the rotating live-tile behavior but added unique tile update tags so the
  last-played and trending metadata refresh cleanly without being stuck on stale
  content.
- Preserved the fallback sequence: last played, then Trending Now, then the
  branded YourTube tile.
- Updated the ARM Windows 10 Mobile AppX package version to `1.0.8.4`.

## v1.0.8.0 - Rotating live tile release

- Added a foreground live-tile queue that rotates the last video selected for
  playback, the latest **Trending Now** result, and the branded YourTube tile.
- Added local persistence for the last-played and trending tile metadata, so the
  queue can be rebuilt after subsequent public-data or playback actions.
- Preserved the existing Trending Now behavior as the fallback: when no
  last-played video is available, the queue starts with the latest trending result.
- Stored only non-secret public metadata for tile use (video ID, title, channel,
  thumbnail URI, and trending region). API keys, OAuth tokens, and media URLs are
  not persisted or displayed by the tile.
- Updated the ARM Windows 10 Mobile AppX package version to `1.0.8.0`.

## v1.0.7.4 - First public release

- Added a Windows 10 Mobile 15063+ ARM UWP app with a Metro-inspired YourTube UI.
- Added public YouTube Data API v3 search, regional trending, details, channels,
  categories, and inline category expansion.
- Added an official YouTube mobile watch page in-app viewing route with a browser
  fallback and no direct media extraction.
- Added foreground Trending Now live-tile updates.
- Added OAuth 2.0 authorization code plus PKCE architecture with system-browser
  sign-in and Credential Locker storage.
- Removed retained legacy credentials from the recovered source and excluded the
  recovered WP8 projects from the public release repository.

## Security and distribution notes

- No API key, OAuth client ID, OAuth client secret, access token, refresh token, or
  private signing certificate is included.
- The included AppX is for Developer Mode sideloading and is signed with a temporary
  development certificate. It is not a production or Microsoft Store package.
