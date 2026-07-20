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
  folder by date and it reads in posting order.

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

## Tracking upstream

```powershell
git fetch upstream
git rebase upstream/main
```

`upstream` = https://github.com/AAndyProgram/SCrawler.
