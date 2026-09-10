# About this fork

This is a bugfix and hardening fork of [AAndyProgram/SCrawler](https://github.com/AAndyProgram/SCrawler),
produced by a systematic module-by-module code review (performed with Claude, an AI assistant, in
2026). The review covered the download core, the main UI, the Reddit / RedGifs / Instagram / TikTok
site modules, and the Feed; the full working ledger — every finding, fix, and deliberate
won't-fix with reasons — is in [REVIEW.md](REVIEW.md).

Not reviewed (out of scope): the YouTube subsystem, Automation, Groups/Channels, the plugin
projects, and the remaining site modules.

## New features

- **Activity log window** (Info menu → *Activity log*): a live, rolling feed of what the program
  is doing right now — job start/finish, per-user "download starting / completed — N new file(s)",
  per-file progress, and skip/failure reasons that were previously silent.
- **Instagram "Download missing posts" support**: Instagram had no missing-post recovery at all
  (the menu action silently did nothing for Instagram users). Missing posts are now re-fetched
  individually with fresh media URLs, with a 10-attempt give-up budget and immediate cleanup of
  posts Instagram reports as deleted.
- **Network circuit breaker**: repeated DNS failures (connection loss/saturation) pause the
  download queue and probe for connectivity instead of burning through every queued user with
  guaranteed-failure requests.
- **Chronological file dates**: downloaded files get their Created/Modified timestamps set to the
  post's own date (from the site API), and content downloads oldest-post-first. Sort any download
  folder by date and it reads in posting order. Times are written as UTC instants, so Windows shows
  them in your local time and historical daylight-saving is handled correctly.
- **De-duplication that works on video**: the built-in duplicate check only ever hashed images, so the
  same video arriving twice was never caught. Media is now matched on a stable identity derived from
  its URL, which catches a post reached from two different sections of the same profile (a reel also
  appears in the profile grid) and, for accounts grouped in one collection, the same RedGifs video
  arriving via both Reddit and RedGifs — keeping the RedGifs copy and recycling the duplicate.
- **RedGifs account discovery**: Reddit posters often link videos from their own RedGifs account.
  Those accounts are now spotted automatically (at no extra API cost — the creator is already in a
  response SCrawler makes), collected in *Info → Discovered RedGifs accounts* with the evidence for
  each, and can be added and grouped into a collection in one click.
- **Automatic RedGifs token refresh**: the temporary token only refreshed on a timer, so a token
  invalidated early left every request failing for the rest of the run. A rejected request now
  refreshes once and retries.
- **Visible pauses**: Instagram's rate-limit pacing could stall a run for minutes with nothing on
  screen. Every wait now reports what it is waiting for and for how long, with a per-profile total.

## Fix highlights

The short version — see [REVIEW.md](REVIEW.md) and the commit history for the details:

- **Download scheduler**: users on an unavailable host were falsely marked "completed" (with
  side effects that corrupted Instagram's rate-limit bookkeeping); one site filling its task limit
  stopped the whole batch, effectively serializing all other sites behind it; a self-`Thread.Abort`
  in job cleanup made every host's active-task counter drift upward forever.
- **Missing-post recovery (all sites)**: permanently deleted posts were retried forever on every
  run (Reddit / RedGifs / TikTok now share a 10-attempt give-up budget); TikTok's recovery
  *deleted* missing records without downloading their replacements; several sites re-checked
  missing posts of accounts that no longer exist on every run.
- **Silent failures**: expired tokens, empty API responses, and rate limits frequently produced
  "successful" runs that downloaded nothing, with no log entry — these paths now log with HTTP
  status codes.
- **Feed**: deleted special feeds came back from the dead (stale menu entries could resurrect the
  deleted XML on disk); a forced full garbage collection ran on the UI thread on every page flip;
  every video tile created its own never-disposed native VLC engine; page changes stole focus from
  other applications; unsynchronized cross-thread access to the shared feed data list.
- **Main UI**: a stale selection silently dropped the whole user selection; removing a user could
  corrupt other users' icons in picture view; landscape thumbnails were stretched.
- **Instagram**: a single malformed reply (an HTTP 200 carrying a web page instead of JSON) was read
  as "your credentials expired" and switched off every Instagram download option in the saved
  settings, skipping the rest of the batch; a post already downloaded as a reel made the profile
  timeline look fully downloaded, so profiles whose reels were fetched first could never scan their
  timeline at all; and a per-post lookup scanned whole lists repeatedly, turning a large profile's
  catch-up into tens of minutes of pegged CPU.
- Assorted correctness fixes in Reddit, RedGifs, Instagram, and TikTok parsing (wrong JSON node
  for TikTok repost dates, an Instagram width/height copy-paste, RedGifs post-ID corruption
  producing malformed API URLs, and more).

## Building from source

Upstream references `PersonalUtilities` (a closed-source library by the upstream author) as
sibling source projects that are not publicly available. This fork references the pre-built DLLs
committed in `lib\` instead, so it builds standalone — after one piece of one-time setup:

- **Discord webhook stub** (gitignored because upstream's real file contains a secret): create
  `SCrawler.YouTube\Editors\BugReporterFormDiscordWebHook.vb` with:

  ```vb
  Namespace Editors
      Partial Public Class BugReporterForm
          Private Const DiscordWebHook As String = ""
      End Class
  End Namespace
  ```

Prerequisites: Visual Studio Build Tools (MSBuild) with the **.NET Framework 4.6.1 targeting
pack**, and `nuget restore SCrawler.sln` once. Then:

```powershell
MSBuild.exe SCrawler.sln /p:Configuration=Release "/p:Platform=Any CPU" /t:Build /m
```

The main executable lands in `SCrawler\bin\Release\SCrawler.exe`.

The `lib\` DLLs are taken from an official release build and must match the upstream release this
fork has merged (see below) — they are a versioned dependency, not a one-time copy.

## Tracking upstream

Upstream publishes each release as a single squashed commit, so merges are release-sized but
infrequent. Merge (don't rebase — rebasing 20+ commits over a squashed release is needless pain):

```powershell
git fetch upstream
git merge upstream/main
```

**Expect to refresh `lib\` as part of the merge.** Upstream builds against their own
PersonalUtilities source and changes member accessibility between releases, so new upstream code
often will not compile against older DLLs — the 2026.8.7.0 merge failed with a single
`BC30451: '_Cookies' is not declared` until the DLLs were updated. Copy the three
`PersonalUtilities*.dll` files from the root of that release's zip into `lib\`, then rebuild. When
deploying, ship the refreshed DLLs alongside `SCrawler.exe`.

Merge history is recorded in [REVIEW.md](REVIEW.md) under *Upstream merges*, including the
conflict-resolution details for each release.

`upstream` = https://github.com/AAndyProgram/SCrawler.
