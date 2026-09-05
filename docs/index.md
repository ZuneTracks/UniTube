---
title: UniTube UWP
---

# UniTube UWP

UniTube is an original UWP client for Windows 10 Mobile 10.0.15063 (Creators
Update) and later. It is designed for ARM devices and uses the official YouTube
Data API v3 for public discovery features.

## Latest release: v1.6.7.6

[Download the ARM Developer Mode sideload package](https://github.com/ZuneTracks/UniTube/releases/tag/v1.6.7.6)

The `v1.6.7.6` release replaces the editable region code with a country
selector sourced from YouTube's supported regions. It defaults to the device
region, places that country first, and has a bounded list; **United States** is
used when a device region is unsupported. It retains Safe Mode, the
authenticated User Profile pivot, foreground video-upload prototype with Google
limited-input-device authorization, and rotating live-tile behavior for the
latest video metadata:

- The last video selected for playback.
- The latest public Trending Now result.
- A branded UniTube app-icon frame.

Without a previously played video, the tile starts with Trending Now. The tile
stores only public video metadata locally and does not expose API keys, OAuth
tokens, or media URLs.

## Legal

Use of the App is governed by the [Terms of Service](terms-of-service.md) and
[Privacy Policy](privacy-policy.md).

## Installing

Download either the optimized **Release | ARM** AppX and its matching
`YourTubeReleaseDevelopment.cer`, or the Release deployment ZIP and run its
`Add-AppDevPackage.ps1` script. Install the certificate before the AppX. The
deployment ZIP includes the required ARM framework dependencies.

To enable uploads, configure your own Google **TVs and limited-input devices**
OAuth client ID and client secret in **Settings**, then use the displayed
verification URL and code from another current browser. No redirect URI is
used. The release page contains the complete setup and troubleshooting guide.

## Important notes

- This is a Developer Mode sideload package, not a Microsoft Store package.
- No private signing key, API key, OAuth client secret, or user token is published.
- Playback uses YouTube's official mobile watch page; the app does not extract or
  download media streams.

[View the full changelog](https://github.com/ZuneTracks/YourTube-UWP/blob/main/CHANGELOG.md)
