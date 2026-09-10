# UniTube UWP

`YouTube.Uwp.sln` contains the UniTube UWP app for Windows 10 Mobile Creators Update
(10.0.15063.0) and later. The recovered WP8 projects remain in their original
folders and are not referenced by the UWP project. Only the existing image files
are linked as package assets; no recovered application code, token handling, stream
handling, or binary libraries are reused.

[Terms of Service](docs/terms-of-service.md) | [Privacy Policy](docs/privacy-policy.md)

## Current release: v1.6.7.6

v1.6.7.6 replaces the editable region code with a selector of countries
supported by YouTube. It starts with the device's active region, places that
country first, and uses United States when the device region is unsupported.
Safe Mode remains enabled by default and uses YouTube Data API v3's strict
SafeSearch setting for public video searches.

The ARM Windows 10 Mobile Developer Mode sideload release supports devices
running 10.0.15063.0 or later. The pinned live-tile refresh behavior from
[`v1.0.8.4`](https://github.com/ZuneTracks/YourTube-UWP/releases/tag/v1.0.8.4)
remains included: after playback, the Start tile updates with the latest video
metadata when returning to the home screen. Release assets include the AppX, its
public development certificate, required ARM framework packages, and deployment
instructions. See [CHANGELOG.md](CHANGELOG.md) for the complete release history.

This is not a Microsoft Store or production-signed distribution: the package uses
the documented `ZuneTracks` temporary development certificate. Do not
redistribute a private PFX file. Before publishing a production distribution,
replace the publisher identity and development certificate with your own production
signing configuration.

## Build prerequisites

Install Visual Studio with the **Universal Windows Platform development** workload
and the Windows 10 SDK version **10.0.15063.0**. Open `YouTube.Uwp.sln`, select
`Debug | ARM`, then build and deploy to a physical Windows 10 Mobile 15063+ device.
ARM is the default project and package architecture because Windows 10 Mobile
devices are ARM-based. `Debug | Any CPU` remains available for architecture-neutral
development or emulator workflows, but use the ARM configuration for device
packages. The package manifest intentionally contains a placeholder publisher and
debug packaging uses the development publisher identity. Replace the publisher identity
and configure a production signing certificate before distribution.

The project deliberately uses the classic UWP project system instead of a current
Windows App SDK dependency, so every app API used is available on 15063.

## Mobile UI

The UWP UI intentionally echoes the recovered Windows Phone application without
reusing its unsupported Silverlight controls: a dark header, red accent strip,
pivot-style **Home**, **Search**, **Categories**, and **Profile** sections, an active-pivot
underline, large thumbnail cards, and compact metadata/detail pages. The header's
**Settings** button opens the separate credentials page. It uses only UWP XAML
controls supported by Windows 10 Mobile 15063.

Selecting a category expands it inline and loads up to 25 current popular videos
under that header. Selecting it again collapses the section; expanding another
category collapses the prior section while retaining its loaded results.

The package artwork is an original flat two-color Metro identity: a coral `Y`
monogram and lightweight wordmark on the app's charcoal mobile palette. It
deliberately does not reuse the recovered YouTube logo, badge, or wordmark
styling.

## Google Cloud configuration

1. Create a Google Cloud project and enable **YouTube Data API v3**.
2. Create an API key, restrict it to YouTube Data API v3, and apply the narrowest
   viable quota and application restrictions. Enter it at runtime in the app. It is
   stored in Windows Credential Locker, is sent only as the `key` parameter on
   public read-only v3 requests, and is never used for account authorization.
3. Create a **TVs and limited-input devices** OAuth client. Do not configure a
   redirect URI. Configure its generated client ID and client secret at runtime in
   the app; the secret is stored only in Windows Credential Locker and is never
   included in the package. Changing either value clears the existing
   authorization, so complete sign-in again.
4. Ensure the OAuth consent screen, test users, scopes, and any verification
   requirements are completed in Google Cloud before production use.

No API key, OAuth client ID, OAuth client secret, developer key, analytics key, or
token is included in the UWP project. User-entered API keys and OAuth tokens are
kept separately in Credential Locker. The original recovered credential literals
have been replaced with explicit removal markers and must be treated as compromised:
revoke or rotate them in their original provider consoles.

### Local Visual Studio 2017 build defaults

This support is included in `v1.6.7.6`. Work from an up-to-date `main`
checkout, not an older source archive or a generated deployment ZIP. In Visual
Studio 2017, expand **YourTube > Services** and open
`LocalBuildConfiguration.cs.template`. If it is not visible, select **Show All Files**
in Solution Explorer or use **File > Open > File** and open
`YouTube.Uwp\Services\LocalBuildConfiguration.cs.template`.

Use **File > Save ... As** to save that file in the same `Services` folder as
`LocalBuildConfiguration.cs`; do not use **Add > New Item**, because the project
already conditionally includes that filename. Replace all three `REPLACE_WITH_...`
constants with values from your Google project, then reload the project or reopen the
solution before building. `.gitignore` prevents the local file from being committed.
At runtime, a value entered in Settings and stored in Credential Locker takes
precedence only when both OAuth values are stored as a complete pair; an otherwise
missing or incomplete saved pair uses the non-placeholder local default pair. This
prevents a stale saved client ID or secret from being combined with an embedded value.
Settings leaves managed values blank and never displays the client secret. Its status
identifies when the app is using built-in local configuration.

Do not use this mechanism for public, shared, or Store builds: compiled AppX files can
be inspected. Leave the local file absent or keep all placeholders unchanged for builds
without embedded development credentials.

### Local Visual Studio 2017 package signing and deployment

The checked-in project intentionally does not contain a private signing certificate, so
its default local AppX output is unsigned. To deploy with the Visual Studio debugger:

1. In **YourTube > Properties > Packaging**, create a test certificate whose subject
   is `CN=ZuneTracks`, install it in the current user's certificate store,
   and copy its thumbprint.
2. Copy `YouTube.Uwp\LocalPackageSigning.props.template` to
   `YouTube.Uwp\LocalPackageSigning.props`, replace
   `REPLACE_WITH_TEST_CERTIFICATE_THUMBPRINT`, and reload the project.
3. Select **Debug | ARM**, select the developer-unlocked Windows 10 Mobile device,
   then use **Build > Deploy Solution** or **Start Debugging**.

`LocalPackageSigning.props` is ignored by Git. If `DEP0001` reports
`0x80070490` (**Element not found**), remove an older developer/sideload
installation of UniTube from the phone, restart it, confirm both ARM framework
dependencies are installed, and deploy again. The source package version is `1.6.7.6`;
it must be newer than any installed UniTube package. The published release deployment
ZIP installs its matching certificate and dependencies automatically.

The app is packaged as **UniTube**. Its package identity, development publisher,
and Credential Locker resource names are separate from earlier
`YouTubeReconstructed` development packages, so re-enter runtime configuration
after installing UniTube.

## Localization

All app-authored UWP interface text is localized through the standard UWP
layout `YouTube.Uwp\Strings\language-<specific-BCP-47-tag>\Resources.resw`.
`language-` is the documented MakePRI folder qualifier; its suffix is the
resource language. Every resource qualifier must be a canonical, specific
Windows culture, never a neutral YouTube `i18nLanguages` code. There are 82
package resource sets for the 87 source codes: four duplicate generic/specific
pairs share one locale (`de`/`de-DE`, `en`/`en-US`, `es`/`es-ES`, and
`pl`/`pl-PL`), and Burmese `my` has no Microsoft Store-supported package
language. The complete machine-readable source-code-to-resource-locale
catalog is `$youtubeLocaleMap` in
[`tools\ValidateUwpResources.ps1`](tools/ValidateUwpResources.ps1).

Notable canonical mappings are `az` → `az-Latn-AZ`, `no` → `nb-NO`, `pt` →
`pt-BR`, `sr` → `sr-Cyrl-RS`, `sr-Latn` → `sr-Latn-RS`, `zh-CN` →
`zh-Hans-CN`, `zh-HK` → `zh-Hant-HK`, and `zh-TW` → `zh-Hant-TW`. Existing
regional source codes remain regional when valid. The default source codes `en`
and `en-US` both use the one required
`Strings\language-en-US\Resources.resw` set, matching `DefaultLanguage`.
The legacy YouTube API code `iw`, when encountered upstream, is normalized to
the standard `he` source code before this catalog is applied.
XAML uses `x:Uid` property
resources, while code-driven status, validation, dialog, and live-tile text uses
the `Localizer` helper with semantic resource names. Video, channel, playlist,
and region text returned by YouTube remains API content and is not translated by
the app.

When changing user-facing text, add its semantic key and English value to
`Strings\language-en-US\Resources.resw`, use the matching XAML property convention
(`.Text`, `.Content`, `.Header`, or `.PlaceholderText`), and add an accurate,
nonempty native-language value for every existing locale resource. Keep each
resource file's key set identical to `en-US`; do not add locale files without
also registering them as exactly one `PRIResource` entry in
`YouTube.Uwp.csproj` using the same
`Strings\language-<specific-BCP-47-tag>\Resources.resw` path.
Run
`powershell -ExecutionPolicy Bypass -File .\tools\ValidateUwpResources.ps1`
to validate independently; non-design-time MSBuild invocations run it before
building. It verifies full YouTube catalog coverage, item/file mapping, specific
canonical Windows BCP-47 qualifiers, qualifier uniqueness, per-file duplicate
keys, and key parity with `en-US`. Preserve format placeholders such as `{0:N0}`
exactly, because `Localizer.Format` applies them using the current culture.

## Implemented public API v3 mappings

| App function | Official YouTube Data API v3 request |
| --- | --- |
| Search public videos | `GET https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&q=...&safeSearch=moderate` (`strict` when Safe Mode is enabled) |
| Trending / popular by region | `GET https://www.googleapis.com/youtube/v3/videos?part=snippet,contentDetails,statistics,status&chart=mostPopular&regionCode=...` |
| Supported country selector | `GET https://www.googleapis.com/youtube/v3/i18nRegions?part=snippet` |
| Category choices by region | `GET https://www.googleapis.com/youtube/v3/videoCategories?part=snippet&regionCode=...` |
| Popular videos in a selected category | `GET https://www.googleapis.com/youtube/v3/videos?part=snippet,contentDetails,statistics,status&chart=mostPopular&videoCategoryId=...&regionCode=...` |
| Video details | `GET https://www.googleapis.com/youtube/v3/videos?part=snippet,contentDetails,statistics,status&id=...` |
| Channel details | `GET https://www.googleapis.com/youtube/v3/channels?part=snippet,contentDetails,statistics&id=...` |

The public client maps results into platform-independent `VideoSummary`,
`VideoDetails`, and `ChannelDetails` DTOs. This replaces the recovered model types
that inherit WP8 `ResultItem`, dispatch through WP8 view-model singletons, and
contain platform-specific commands and transfer objects.

### Safe Mode

Settings includes a persisted **Safe Mode** toggle, enabled by default, that changes public video searches
to YouTube Data API v3's `safeSearch=strict` filter. It is not a YouTube Kids
integration and cannot filter popular, category, or authenticated account feeds,
because those endpoints do not support the `safeSearch` parameter.

### Authenticated Profile pivot

After device authorization, the Profile pivot reads only data exposed by the
authenticated YouTube Data API v3 connection:

| Profile data | Official YouTube Data API v3 request |
| --- | --- |
| Authorized channel/account summary | `channels.list?part=snippet,contentDetails,statistics&mine=true` |
| Uploaded videos | `playlistItems.list?part=snippet,contentDetails&playlistId={channel.uploads}` |
| Subscriptions | `subscriptions.list?part=snippet,contentDetails&mine=true` |
| Playlists | `playlists.list?part=snippet,contentDetails&mine=true` |
| Playlist videos | `playlistItems.list?part=snippet,contentDetails&playlistId=...` |
| Liked videos | `videos.list?part=snippet,contentDetails,statistics,status&myRating=like` |

Collections load in pages of up to 25 items and expose a load-more action when
the API returns a next-page token. Watch History and Watch Later are explicitly
unsupported: YouTube Data API v3 does not expose either as a readable
collection, so the app does not pretend to provide them.

### Trending live tile

After a successful **Trending Now** request, UniTube persists the leading public
result and refreshes a UWP live-tile queue. After a user starts playback, that
queue rotates the last-played video, the latest Trending Now result, and a branded
UniTube app-icon tile. When there is no last-played metadata, the tile falls back
to the latest Trending Now result and the branded tile. Only public video metadata
(ID, title, channel, and thumbnail URI) is stored in local app settings; API keys,
OAuth tokens, and media URLs are never included.

The tile does not fetch or refresh data in the background: automatic polling would
need a separate UWP background-execution design and must not rely on a stored user
API key or on the retired WP8 scheduled agent.

## Account authorization architecture

`OAuthDeviceAuthorizationService` uses Google OAuth 2.0 device authorization with
the `https://www.googleapis.com/auth/youtube.upload` and
`https://www.googleapis.com/auth/youtube.readonly` scopes:

1. It obtains a short-lived device code and displays Google's verification URI and
   user code. The user completes authorization in a browser on another device; the
   app does not embed a sign-in page or receive a redirect. Settings displays the
   remaining code lifetime and Google's current polling interval.
2. It polls `https://oauth2.googleapis.com/token` at Google's requested interval
   until authorization succeeds, expires, or is canceled, supplying the
   user-configured limited-input device client ID and secret only to Google.
   A `slow_down` response increases the displayed interval. When the code expires,
   the open Settings flow automatically obtains and displays a replacement code.
3. Access token, refresh token, and expiry are kept as separate Credential Locker
   entries, avoiding mobile vault value-size limits. `GetValidAccessTokenAsync`
   refreshes an expiring access token without adding an API key.

The Profile pivot uses the read-only scope to request the authorized channel,
subscriptions, playlists, playlist videos, and liked videos. The upload scope is
retained for the existing uploader. Tokens issued before the Profile pivot do
not gain the new scope automatically; sign out and complete device sign-in again
if Profile reports an authorization or insufficient-permission error.

The **Upload** page uses a brokered UWP file picker, sends `snippet` and `status`
metadata to `videos.insert`, and uploads the selected file in 256 KiB resumable
chunks. It reports determinate byte progress and permits cancellation between
chunks. The uploader requires completed OAuth authorization and never uses the
public API key. The Profile pivot uses `youtube.readonly` for the authorized
channel, subscriptions, playlists, playlist videos, and liked videos. Other
account features must explicitly request the minimum OAuth scope and use a bearer
access token. Examples include:

### Uploading a test video

1. In **Settings**, save a user-created **TVs and limited-input devices** OAuth
   client ID and client secret, use **Start Google sign-in**, and enter the displayed Google
   code at the displayed verification URL from another phone, tablet, or computer
   with a current browser.
   The Windows 10 Mobile browser is not used for this step.
2. Open **Upload**, select a local `.mp4`, `.wmv`, `.mov`, `.avi`, or `.mkv` file,
   enter a title, optional description, and privacy level, then select **Start
   upload**.
3. Select **Cancel** to stop the foreground transfer. A canceled session is not
   persisted or resumed after leaving the page.

Use **Settings > Sign out** to remove the stored Google access and refresh-token
metadata before authorizing another account.

### Diagnosing sign-in failures

If the app closes during sign-in, reopen it and select **Settings** >
**View diagnostics**. The page retains recent application exception HRESULTs and
OAuth/Credential Locker milestones across restarts, and can save them as a text
file. It deliberately excludes API keys, OAuth client credentials, device codes,
access tokens, and refresh tokens.

| Feature | v3 resource | OAuth required |
| --- | --- | --- |
| Subscribe / unsubscribe | `subscriptions.insert` / `subscriptions.delete` | Yes |
| Manage playlists or playlist items | `playlists.*` / `playlistItems.*` | Yes |
| Rate a video | `videos.rate` | Yes |
| Post comments | `commentThreads.insert` / `comments.*` | Yes |
| Upload video | resumable `videos.insert` | Yes |

## Deliberate migration boundaries

The legacy app called retired GData v2 Atom/XML feeds, exchanged tokens with a
client secret, used a WP scheduled agent/background transfer APIs, and scraped
watch-page responses (`get_video_info`, `fmt_stream_map`, and signed media URLs).
Those paths are not carried forward.

* **Direct media URLs and downloads are removed.** Data API v3 provides metadata,
  not media streams. The details page opens YouTube's official HTTPS mobile watch
  page in a UWP `WebView`, avoiding iframe-player configuration requirements on
  Windows 10 Mobile. The normal YouTube watch URI remains the browser fallback. No
  playback URL is extracted.
* **Watch History and Watch Later have no equivalent public v3 collection API.**
  They require a product redesign (for example, app-local state or a user-created
  playlist where allowed); they are not silently emulated.
* **Favorites is not migrated as a global legacy feed.** Use a supported playlist
  workflow only after an explicit OAuth implementation.
* **WP8 scheduled agents, Silverlight pages, BackgroundTransferService, Phone tasks,
  and unsupported player libraries are not referenced.** The UWP live tile is
  updated only after a foreground Trending Now request; any future background work
  must be redesigned around UWP background-execution limits and UWP APIs available
  in the selected target version.

## Recovered-source audit

The retained WP8 source is intentionally excluded from `YouTube.Uwp.sln`. The audit
found retired GData v2 usage in `YouTube.ViewModel` request/model classes (including
search, videos, channels, comments, playlists, subscriptions, ratings, uploads,
watch history, watch later, and favorites); old OAuth/token exchange in
`YouTube.ViewModel.Helpers\AuthenticationHelper.cs`; direct-media extraction in
`YouTube.ViewModel.Helpers\YoutubeHelper.cs`; and WP platform/background code in
`YouTube.UI` and `YouTube.TaskAgent`.

Credential-bearing constants or request values were removed from the legacy
constants, authentication, comments, upload, view-model, and task-agent paths.
Their removal markers document why that recovered code must not be built, shipped,
or used as a credential source.
