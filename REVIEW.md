# SCrawler Incremental Review Ledger

Persistent state for the multi-session code review + hardening effort.
Any Claude session (any model) resumes from this file — read it fully before touching code.

**STATUS: the planned review is COMPLETE** (chunks 1–6 + 5.5, finished 2026-07-09). No Open
Suspicions remain. This file stays as the authoritative record of what was reviewed, fixed, and
deliberately left alone; [FORK.md](FORK.md) is the user-facing summary. Remaining open item
(user decision, not review work): the Feed async/placeholder tile-loading redesign — see the
chunk-5 notes.

## Ground rules

- **Fix confirmed bugs immediately**; build (`msbuild`), deploy to `D:\Utilities\SCrawler\`, commit per chunk.
- **Never speculatively edit** based on a suspicion — record it in *Open Suspicions* instead, and resolve it when the other half of the interaction is read.
- `BugReporterFormDiscordWebHook.vb` is gitignored (contains a Discord webhook secret) — never commit it. It's why `BugReporterForm` is a `Partial Class`.
- PersonalUtilities (`lib/*.dll`) is closed-source — cannot review; its known sharp edges are listed under *PersonalUtilities Hazards*.
- `SFile` is a **struct** — never use `Is`/`IsNot Nothing`; use `.IsEmptyString`.
- Git: `origin` = TomTheHand fork (push here), `upstream` = AAndyProgram.
- Use the PowerShell tool for git/msbuild, not Bash.

## Scope decisions (from user, 2026-07-07)

- Sites used: **Reddit + RedGIFs (top priority)**, Instagram, TikTok. All other site modules: **skip**.
- **Skip entirely**: SCrawler.YouTube subsystem (~12k lines), Automation, Groups/Channels, plugin projects, `My Project`/designer boilerplate.
- Feed: second-priority review — user finds it unresponsive/janky; goal is usability, not just bug-hunting.
- **Feature request**: live activity log — real-time visibility into what the app is doing
  ("User X completed → User Y: N posts, M new → downloading file 1 of M: remote → local").
  User delegated the design. Implement during chunks 1–2 while the download core is in context.

## Roadmap

| Chunk | Target | ~Lines | Status |
|-------|--------|--------|--------|
| 1 | `SCrawler\API\Base` + `API\BaseObjects` + `SCrawler\Download` core; start activity-log instrumentation | 8k | **done** (2026-07-07) |
| 2 | Top-level `SCrawler\` (settings, MainFrame plumbing); finish activity-log UI | 6.7k | **done** (2026-07-07) |
| 3 | `API\Reddit` + `API\Redgifs` + `API\Imgur` + `API\Gfycat` | 4.3k | **done** (2026-07-07) |
| 4 | `API\Instagram` + `API\TikTok` | 3.8k | **done** (2026-07-07) |
| 5 | `Download\Feed` — responsiveness/jank focus | 6.1k | **done** (2026-07-08) |
| 5.5 | Instagram `ReparseMissing` (user-approved feature, 2026-07-09) | — | **done** (2026-07-09) |
| 6 | Ledger sweep: resolve all remaining Open Suspicions + note triage | — | **done** (2026-07-09) |

## Reviewed

*(module → date → outcome; append as chunks complete)*

### Chunk 1 (2026-07-07) — API\Base + API\BaseObjects + Download core

Read in full: `TDownloader.vb`, `UserDataBase.vb`, `Structures.vb`, `SiteSettingsBase.vb`, `WebClient2.vb`,
`TokenBatch.vb`, `NetworkBreaker.vb`, `ProfileSaved.vb`, `IUserData.vb`, `DownDetector.vb`, `M3U8Base.vb`,
`MissingPostsForm.vb`, `DownloadProgress.vb`, `DownloadedInfoForm.vb`, `UserDownloadQueueForm.vb`,
`ActiveDownloadingProgress.vb`, `DownloadSavedPostsForm.vb`, `InternalSettingsForm.vb`, `DomainsContainer.vb`,
`Declarations.vb`, `DeclaredNames.vb`, `GDL.vb`, `YTDLP.vb`, `EditorExchangeOptionsBase*.vb`,
`SplitCollectionUserInfo.vb`. New file: `Download\ActivityLog.vb`.

Bugs fixed (see *Fixes applied*): UserMediaD.Equals wrong-type cast; Job.Finish self-thread-abort
skipping `DownloadDone` (host active-task counter drifted up forever, availability cache never reset);
unsynchronized shared `Files`/`Downloaded` lists (added `FeedDataLock` + lock-aware helpers
`DownloadedSnapshot/DownloadedClear/DownloadedRemoveAll/FilesAddRange/FilesSort`; converted readers in
DownloadedInfoForm + AutoDownloader and producers in ProfileSaved); per-file DNS failures never fed
NetworkBreaker + tripped breaker now pauses the file loop; `UserMedia.New(EContainer)` path
reconstruction diverged from downloader placement rules (untrimmed `*` marker, unconditional `Video\`).

Notes (no action, for later chunks):
- `TDownloader.FilesSave` spin-waits (`Thread.Sleep(100)` loop) — Feed jank candidate (chunk 5).
- ~~Feed reads `Downloader.Files` without `FeedDataLock` — readers still unsynchronized (chunk 5).~~ **Converted in chunk 5.**
- Unavailable-host users go to `Keys` but not `KeysSkipped` → removed from `_Job.Items` without downloading.
- One host hitting its task limit exits the whole batch loop (`Exit For` in TDownloader).
- Totals recount in UserDataBase (~1375): pictures mask top-dir-only + includes webm; videos recursive — asymmetric, author intent unclear.
- `Continue For` in the stxt text-post block (~1926) skips `_ContentNew(i) = v` write-back and `Progress.Perform`.
- `ProfileSaved`: `_Unavailable`/`_NotReady` counters swapped vs. their messages (only ever summed — cosmetic).
- `DownloadProgress.Dispose` never disposes `PR_PRE` and never unhooks `Job.Progress` handlers; `ActiveDownloadingProgress` disposes DownloadProgress objects without unhooking — leak-class only.
- `DownloadProgress.DownloadData` would NRE on `BTT_START` for a `Download.Main`-type job (buttons only exist for SavedPosts) — currently unreachable, don't extend.
- `M3U8Base.Download`: `Throw ex` resets stack traces; `Cache.DisposeIfReady` in Finally relies on extension-method Nothing-tolerance; passing `ExistingCache` gets it disposed on the callee side.
- `UserMedia.GetHashCode` built on mutable fields; event raisers swallow exceptions broadly.

### Chunk 2 (2026-07-07) — top-level SCrawler + activity-log viewer UI

Read in full: `MainFrame.vb`, `SettingsCLS.vb`, `MainMod.vb`, `MainFrameObjects.vb`, `UserInfo.vb`,
`UserImage.vb`, `MyProgressExt.vb`, `UserFinder.vb`, `ListImagesLoader.vb`, `UserSearchForm.vb`,
`LabelsKeeper.vb`, `UserBan.vb`, `ToolStripKeysButton.vb`. New file: `Download\ActivityLogForm.vb`
(viewer for the chunk-1 ActivityLog module; "Activity log" item added to the Info menu).

Bugs fixed (see *Fixes applied*): MainFrame.GetSelectedUserArray checked the loop counter instead of
the FindIndex result (a stale selected key threw → outer Catch returned an empty list → whole selection
silently dropped); MainFrame.RemoveUserFromList removed the user's icon at the ListView item index
instead of the image-list index (corrupted other users' icons in picture view); UserSearchForm
SearchResult.CompareTo returned `CompareTo() = 0` (Boolean→Integer coercion — mode sorting never worked
and the comparer was inconsistent, likely the reason for the masking `Catch ArgumentOutOfRangeException`
in SearchUser); UserFinder import log labeled the Skipped section "Duplicates:".

Notes (no action, for later chunks / awareness):
- `SettingsCLS.UpdateUsersList` only writes Users.xml when the list is non-empty; deleting the LAST user
  is persisted only by `Dispose` on clean exit (crash before exit resurrects the user). `LoadUsers` wraps
  everything in a swallow-all Catch.
- `ListImagesLoader.Update` (non-FastProfilesLoading branch): `Task.WhenAll(...)` result is discarded, so
  it doesn't wait — but "fixing" it to WaitAll would deadlock (tasks Invoke back to the UI thread that's
  waiting). Also: `Thread.Abort` in InterruptUpdate, `Application.DoEvents` per item, and a masking
  `Catch ArgumentOutOfRangeException` — this class is the main-list jank; revisit with Feed in chunk 5.
- `ListImagesLoader.UpdateImages` "background thread" BeginInvokes the entire image loop onto the UI
  thread (with DoEvents per item) — icon loading effectively runs on the UI thread.
- `UserImage.GetImage` assumes the last `ResizedImages` key is the just-fitted image (closed-source
  ordering assumption).
- `UserFinder.Verify`: IgnoredCollections `Contains(x.ToLower)` vs `Add(x)` (original case) — mostly
  masked by the UsersList existence clause, left alone.
- `MyProgressExt` parameterless ctor never creates `PR_PRE` → NRE on any `*0` member if that ctor is ever
  used (PreProgress only type-checks).
- FALSE ALARM (do not "fix"): `MainFrame.MyMissingPosts`/`MyUserMetrics` are never assigned in SCrawler
  code, but `FormShow` is a PersonalUtilities extension that takes `Me` ByRef and instantiates the field —
  evidenced by the IDE0044 suppressions in GlobalSuppressions.vb.

### Chunk 3 (2026-07-07) — API\Reddit + API\Redgifs + API\Imgur + API\Gfycat

Read in full: `Reddit\UserData.vb`, `Reddit\SiteSettings.vb`, `Reddit\M3U8.vb`, `Reddit\Declarations.vb`,
`Redgifs\UserData.vb`, `Redgifs\SiteSettings.vb`, `Redgifs\Declarations.vb`, `Imgur\Envir.vb`,
`Gfycat\Envir.vb`. Skipped per scope (Channels feature unused): `Reddit\Channel.vb`,
`Reddit\ChannelsCollection.vb`, `Reddit\IChannelLimits.vb`, `Reddit\IRedditView.vb`,
`Reddit\RedditViewSettingsForm*.vb`.

Bug fixed (see *Fixes applied*): Redgifs `ReparseMissing` had no give-up budget — `u.Attempts`
incremented on failure but was never checked, never persisted (`_ForceSaveUserData` never set), and
410/404 (RedGifs' own "gone for good" signal) wasn't treated as permanent; a deleted gif was
re-fetched every run forever. Also: parse-succeeded-but-no-"gif"-node was a silent no-op (no attempt
counted). Now mirrors Reddit: `MaxReparseMissingAttempts = 10` budget, 410/404 short-circuit, all
failure paths count an attempt + force save, Finally bounds guard on rList removal.

Notes (no action, for awareness):
- Reddit `ReparseVideo` ~1038: `Const v2 = UTypes.VideoPre + UTypes.m3u8` = 110 matches no enum value —
  the `p.Type = v2` conditions are dead code (m3u8 items download via `DownloadM3U8` anyway).
- Reddit `ReparseVideo` ~1083: when the reparse fetch fails (empty response), the slot keeps an empty
  `New UserMedia` (Type=Undefined, URL="") instead of being removed — relies on downstream skipping
  empty URLs. Upstream behaviour, left alone.
- Reddit `DownloadDataChannel` catch ~616: `ElseIf errValue = 429 And round = 0` can never be true
  (round is incremented before the Try) — dead branch; also DownloadingException throws on 429 rather
  than returning it. Left alone.
- Reddit `M3U8.MergeFiles`: in the video-only path, if `IndexReindex` returned a conflict-renamed `f`
  and the move succeeds, `OutFile` is NOT updated to `f` — the returned path then points at the
  pre-existing conflicting file. Edge case; needs IndexReindex semantics (closed-source) to confirm.
- Redgifs `GetDataFromUrlId` catch: `Responser.Client.StatusCode` at the 401 check would NRE if the
  `Responser` parameter were Nothing — all current callers pass non-Nothing.
- Redgifs `SiteSettings._TokenUpdating` spin-wait is not a real lock (two threads can both pass the
  While) — worst case is a redundant token refresh; benign.
- Reddit `SiteSettings.UpdateToken` curl path embeds credentials unquoted in the curl argument string —
  breaks (only) if a password contains `"`; note if token refresh ever fails mysteriously.

### Chunk 4 (2026-07-07) — API\Instagram + API\TikTok

Read in full: `Instagram\UserData.vb`, `Instagram\UserData.GQL.vb`, `Instagram\SiteSettings.vb`,
`Instagram\Declarations.vb`, `Instagram\EditorExchangeOptions.vb`, `TikTok\UserData.vb`,
`TikTok\SiteSettings.vb`, `TikTok\Declarations.vb`, `TikTok\UserExchangeOptions.vb`.

Bugs fixed (see *Fixes applied*):
- Instagram `ObtainMedia_SetReelsFunc` picture-size lambda checked `width` twice and `height` never
  (copy-paste) — height-only size entries fell into the URL-regex fallback instead of computing the size.
- TikTok reposts: `postDate` was read from the JSON document root (`j.Value("createTime")`) instead of
  the post item (`.Value("createTime")`) — repost dates were always Nothing, so date limits never
  applied to reposts and their posts carried no date.
- TikTok `ReparseMissing`, three defects: (1) reparsed replacement items were added to `_TempMediaList`
  with State=Unknown, so in DownloadMissingOnly mode the base filter
  (`_TempMediaList.RemoveAll(Not Missing)`, UserDataBase ~1333) silently discarded them AFTER the
  originals were already queued for removal from `_ContentList` — "Download missing posts" deleted the
  missing records without downloading anything; now replacements are stamped Missing and carry over the
  original Post info + attempt count (mirrors Reddit/Redgifs, which pass `State:=Missing` via
  ObtainMedia). (2) No give-up budget — a permanently deleted post spawned a yt-dlp/gallery-dl process
  every run forever; ported `MaxReparseMissingAttempts = 10` with attempt counting on failure +
  `_ForceSaveUserData` (same pattern as Reddit/Redgifs). (3) Sibling images of a multi-image post were
  removed from `_ContentList` unconditionally, even when the post's reparse had failed — `picIDs` is now
  a Dictionary(post → success) and siblings are removed only on success.

DownloadMissingOnly/UserExists audit conclusion (closes the chunk-3 Open Suspicion):
- **TikTok**: no `UserExists` mechanism exists at all (`DownloadingException` returns 0 uncondition-
  ally; yt-dlp/gallery-dl exit codes aren't surfaced), so an existence probe can't be ported — the
  attempts budget above bounds the damage instead.
- **Instagram**: moot — Instagram has NO `ReparseMissing` override (inherits the empty base no-op), so
  "Download missing posts" for an Instagram user silently does nothing (nothing is reparsed, base
  filter leaves `_TempMediaList` empty, no records are harmed). Missing Instagram items are simply
  never retried, in routine mode or missing-only mode. Implementing an Instagram reparse (the
  `/api/v1/media/{id}/info/` endpoint via the existing `DownloadPosts` machinery would fit) is a
  **feature decision for the user**, not a silent fix — raised in the chunk-4 report.

Notes (no action, for awareness):
- Instagram `SiteSettings.ReadyToDownload` requires `CBool(DownloadTimeline.Value)` — turning off the
  site-level "Download timeline" option gates off ALL Instagram downloads (stories/reels/tagged too).
- Instagram Tagged-limit check (~866–872) is redundant/overlapping (`Not HasValue OrElse ... < limit`
  else-branch throws) but functionally harmless.
- Instagram `DownloadPosts` (~952) `If Index > 0 Then ProgressPre.ChangeMax(1)` — `Index` here is the
  loop-external member, looks intentional (progress only for indexed/paged runs).
- Instagram `ParseTokens` heuristic: a scraped token containing ":" is dtsg, else lsd — fragile but
  upstream, and failures raise ExitException with a clear message.
- Instagram `ValidateExtension`: heic pictures get both a heic and a jpg entry (struct copy) — upstream
  intent (download both, keep what works).
- TikTok `ReparseMissing` reparses only Video and Picture items — any other Missing type is skipped
  (never attempted, never given up). Harmless today: TikTok only ever creates Video/Picture items.
- TikTok posts-file IDs: photos are persisted as `photo_{id}` in `_TempPostsList` but `_ContentList`
  Post.IDs are unprefixed; base line ~1315 re-adds unprefixed IDs. Dedupe still works because discovery
  checks the prefixed form against the persisted file, but the two ID namespaces coexist — don't
  "unify" them.

### Chunk 5 (2026-07-08) — Download\Feed (responsiveness + correctness)

Read in full: `DownloadFeedForm.vb`, `FeedMedia.vb`, `FeedSpecial.vb`, `FeedSpecialCollection.vb`,
`FeedVideo.vb`, `FeedFilter.vb`, `FeedFilterCollection.vb`, `FeedFilterForm.vb`, `FeedCopyToForm.vb`,
`FeedView.vb`. (FeedFilterForm/FeedCopyToForm: clean.)

Bugs fixed (see *Fixes applied*):
- **Deleted special feeds became zombies.** Three interlocking defects: (1) both `Feed_FeedRemoved`
  fan-out handlers (DownloadFeedForm + FeedMedia context menu) omitted `BTT_FEED_ADD_SPEC_REMOVE` —
  the author's classic "handles the list but omits the adjacent entry" pattern, since `Feed_FeedAdded`
  populates it in both — so deleting a feed left a live item in both "Add & remove" dropdowns;
  (2) `FeedSpecialCollection.Delete` disposed the feed via a `FindIndex` that always returned -1
  (the `FeedDeleted` event, raised inside `FeedSpecial.Delete`, had already removed the feed from
  `Feeds` by the time `FindIndex` ran) — dead code, so no delete path ever disposed a feed, and direct
  `f.Delete()` callers (Feed_SPEC_DELETE, BTT_FEED_DELETE_SPEC_Click) bypassed `Collection.Delete`
  entirely; (3) `FeedSpecialCollection.Add` and the `Favorite` factory never attached the
  `FeedDeleted` handler that `Load` attaches — a feed created during the session never notified the
  collection at all when deleted. Since `FeedSpecial.File` lazily regenerates its path from `Name`,
  clicking the stale menu item resurrected the deleted feed's XML on disk. Fix: dispose (non-favorite)
  feeds centrally in `Feeds_FeedDeleted` (covers every delete path), simplify `Collection.Delete`,
  attach the handler in `Add`/`Favorite`, and add the missing dropdown to both removal fan-outs.
- **Blocking full GC on the UI thread on every page flip**: `ClearTable` ran
  `GC.Collect + WaitForPendingFinalizers + WaitForFullGCComplete` inside `ControlInvoke(TP_DATA, …)`
  each time the page changed — a large chunk of the per-page-flip freeze. Removed.
- **A full LibVLC engine per video tile, never disposed**: `FeedVideo.New` did
  `New MediaPlayer(New Media(New LibVLC(...), …))` — native library init per tile on the UI thread,
  and neither the LibVLC nor the Media was ever disposed (only the MediaPlayer). Now one shared
  `Lazy(Of LibVLC)` engine for all tiles; the `Media` is kept in a field and disposed with the tile.
- **Feed readers of the shared `Downloader.Files` list were unsynchronized** (the chunk-1 note):
  converted all DownloadFeedForm sites to the FeedDataLock helpers — new TDownloader helpers
  `FilesSnapshot/FilesRemoveAll/FilesClear/FilesLocked`; RefillList now filters a snapshot,
  FeedRemoveCheckedMedia/BTT_CLEAR_DAILY/MoveCopyFiles' find-and-replace run under the lock.

Notes (no action / report-only):
- Feed jank root cause #2 (not fixed — needs a redesign decision): `FeedMedia.New` does ALL tile work
  synchronously on the UI thread per page flip — full image decode + optional WebP conversion,
  text→bitmap rendering, subscription HTTP `GetWebFile`, and (when UseM3U8) a synchronous
  `FFMPEG.TakeSnapshot` per video tile in `FeedVideo.New`. Async/placeholder loading is the real fix;
  raised with the user as a design question.
- `MyRange_IndexChanged` Finally block calls `Activate() : Focus()` — the Feed steals focus on every
  page change (including endless-scroll auto-flips). Report note.
- `FeedMedia` Dispose unsubscribes `Settings.Feeds` events only if `FeedShowSpecialFeedsMediaItem` is
  still true — toggling the setting off while tiles are alive leaks handlers (collection holds dead
  tiles until the swallow-all raisers eat their exceptions). Minor.
- `FeedFilter.Sites` is saved in filter XML but only narrows the user-picker list in FeedFilterForm —
  `DataFilterPredicate` checks Types/Users only, so a Sites-only filter filters nothing. Usability quirk.
- `FeedSpecialCollection.UpdateUsers` reads `Downloader.Files.Count` without the lock — benign
  (approximate count check).
- `FeedSpecial.RemoveNotExist` runs once per feed lifetime (`_NotExistRemoved` flag) — missing-file
  purge only happens on the first load of a special feed per app run. Author intent, left alone.

### Chunk 5.5 (2026-07-09) — Instagram ReparseMissing (user-approved feature)

The chunk-4 report raised implementing "Download missing posts" for Instagram as a feature decision
(it was a silent no-op — no `ReparseMissing` override). User approved 2026-07-09 ("Yes, please fix that!").

New `ReparseMissing` override in `Instagram\UserData.vb` (before the Standalone-downloader region):
- Per missing item, fetches `/api/v1/media/{id}/info/` — the same endpoint + throttle machinery
  (`Ready()`/`ReconfigureAwaiter`/`NextRequest`/`UpdateRequestNumber`, non-GQL responser mode) the
  tagged-post reparse (`DownloadPosts`) and standalone downloader already use. Stored `Post.ID` in
  `mediaID_userID` form is trimmed to the media ID for the URL.
- `MaxReparseMissingAttempts = 10` give-up budget (Reddit/Redgifs/TikTok pattern), attempts persisted
  via `_ForceSaveUserData`; one request per unique post ID with sibling resolution via a
  post→removeOriginal dictionary (a successful fetch re-adds every media of the post).
- Replacements are stamped State=Missing carrying original Post + Attempts AFTER the ObtainMedia calls
  (NOT via ObtainMedia's State parameter — its gallery branch drops State/Attempts for carousel
  children, the same latent bug class as TikTok chunk-4; noted below).
- Per-item HTTP errors handled locally with `EDP.ReturnValue` + StatusCode switch, NOT via
  ProcessException: `DownloadingException`'s 404 branch is account-oriented (sets UserExists=False /
  triggers a username recheck) and a deleted post must not flag a live account. 404 → permanent-gone
  immediate removal; 429 → engage `TooManyRequests` machinery and retry via the outer `Ready()` loop;
  400/401 → credentials log + abort loop (item still gets an attempt++ so a media-gone-400 is
  eventually budgeted out); 500/560 → `SkipUntilNextSession` + abort; else attempt++.
- DownloadMissingOnly mode skips `DownloadDataF`, so the override does its own minimal session setup
  (error-field reset, `_DownloadingInProgress`, ResponseReceived handler, `ChangeResponserMode(False)`)
  and tears down via `UpdateResponser()` in Finally; guarded by `SkipUntilNextSession` early-exit.
  Finally also carries the rList bounds guard (chunk-3 pattern).

Note (no action): `ObtainMedia`'s gallery branch (~line 1272) forwards neither `State` nor `Attempts`
to carousel children — latent only, since nothing passes State=Missing into ObtainMedia today (the new
reparse deliberately stamps after the fact). If a future caller relies on the State parameter for
gallery posts, this is the TikTok chunk-4 bug again.

### Chunk 6 (2026-07-09) — ledger sweep: Open Suspicions + note triage

Closed the last Open Suspicion (existence-probe base hook → won't-do, see below) and triaged every
note-level finding from chunks 1–5. Six promoted to fixes:

1. **TDownloader batch loop — unavailable-host users falsely "completed"** (chunk-1 note, worse than
   noted): users whose host/account failed `AvailablePartial` went to `Keys` but not `KeysSkipped`, so
   the cleanup loop gave them the full completed treatment — `AfterDownload` WITHOUT its paired
   `BeforeStartDownload` (Instagram's AfterDownload writes the user's `RequestsCountSession` into
   `MyLastRequestsCount`, corrupting the 429-avoidance carry-over for the next user; OnlyFans' pushes a
   possibly-Nothing `CCookie` into the shared responser) plus a false "completed — 0 new file(s)"
   activity entry. Now they're added to `KeysSkipped` (skip log, no completed treatment, still removed
   from the job — correct, since `HostsUnavailableIndexes` only clears at `DownloadDone`).
2. **TDownloader batch loop — one full host stopped the whole batch** (chunk-1 note): the per-host
   task-limit check was `Exit For` after starting the limit-th task, so batch-building stopped at the
   first host to fill up — with many queued users on one site, all other sites downloaded almost
   serially behind it. Now a pre-check `Continue For` skips only that host's users (they stay in the
   job for the next batch; NOT added to `Keys`, so cleanup doesn't drop them) and the batch keeps
   filling with other hosts' users. `Math.Max(limit.Limit, 1)` preserves the original
   at-least-one-per-batch guarantee (a TaskCount=0 host would otherwise spin the outer `Do While`).
3. **UserDataBase `stxt` block `Continue For`** (chunk-1 note): a text post with no file and no
   derivable destination skipped the `_ContentNew(i) = v` write-back, `Progress.Perform`, and left the
   item Unknown. Now marks it `Skipped` and falls through (a `textTargetOk` flag replaces the jump);
   `dCount` still not incremented (nothing was saved).
4. **ProfileSaved swapped counters** (chunk-1 note): `_Unavailable += 1` sat in the "is not ready"
   branch and `_NotReady += 1` in the "is unavailable" branch. Swapped (they're only ever summed —
   cosmetic, but free).
5. **Feed focus steal** (chunk-5 note): `MyRange_IndexChanged`'s Finally called `Activate() : Focus()`
   unconditionally on every page change. Now gated on `Form.ActiveForm Is Me` — refocuses only when
   the feed is already the active window.
6. **FeedMedia dispose-handler leak** (chunk-5 note): the Disposed handler removed the
   `Settings.Feeds` event handlers only `If Settings.FeedShowSpecialFeedsMediaItem` — toggling the
   setting off with tiles alive leaked them. RemoveHandler is now unconditional (no-op if never
   attached).

Existence-probe base hook — **RESOLVED: won't-do.** Each site's probe is ~90% site-specific request
mechanics (Reddit about.json, Mastodon lookup URL, Redgifs token+API, OnlyFans cookies, ThreadsNet
inline retry-loop check); a shared base hook would save ~4 lines per site while adding an abstraction
layer, and no new site has needed the probe since. If a future site needs one, copy the Redgifs
version (~UserData.vb:160-193) — it's the cleanest.

Notes NOT promoted (stand as recorded, with reasons): FilesSave spin-wait + ListImagesLoader jank
(subsumed by the async-loading redesign question, open with user); UpdateUsersList last-user-delete
persistence (niche, crash-window only); DownloadProgress/ActiveDownloadingProgress dispose leaks
(leak-class, forms live for the app's lifetime); totals-recount asymmetry, M3U8 MergeFiles OutFile
edge, Reddit dead branches, Redgifs token spin-wait, curl quoting (upstream intent / needs
closed-source confirmation / dead code); Instagram ReadyToDownload timeline-gates-everything +
FeedFilter.Sites picker-only (behavior changes = feature decisions, not bugs); TikTok ID namespaces
(don't unify); UserMedia.GetHashCode mutability (no observed harm); Instagram ObtainMedia gallery
State/Attempts drop (latent — guarded against in the 5.5 implementation, noted there).

Pre-ledger work (earlier sessions, already committed to fork):
- `Download\NetworkBreaker.vb` — new DNS-failure circuit breaker (written by Claude, reviewed).
- `TDownloader.vb` — breaker integration + observability logging (partial review only; full review due in chunk 1).
- `API\Base\UserDataBase.vb` — `SafeGetResponse` added (partial review only; full review due in chunk 1).
- Site fixes applied: Reddit (ParseContainer ext fallback, MediaFromData guard), Instagram/Redgifs (SafeGetResponse), Mastodon/OnlyFans/ThreadsNet/Twitter (ReparseMissing existence probes + early-exit). These were targeted fixes, not full module reviews.

## Fixes applied

*(append per chunk: commit hash — summary)*

- `27cb8bf` — Chunk 1 (part 1): TDownloader fixes (UserMediaD.Equals cast, Job.Finish self-abort guard,
  FeedDataLock for Files/Downloaded producers, DNS→NetworkBreaker feed + tripped-breaker pause in
  DownloadContentDefault) + ActivityLog module and full producer instrumentation.
- `53ca6a5` — Structures.vb: saved-post path reconstruction mirrors DownloadContentDefault placement rules.
- `6af363e` — FeedDataLock helpers on TDownloader; converted unsynchronized consumers
  (DownloadedInfoForm enumerate/clear, AutoDownloader RemoveAll, ProfileSaved Files.AddRange/Sort).
- `b658aa2` — Chunk 2: GetSelectedUserArray index fix, RemoveUserFromList image-index fix,
  UserSearchForm comparer fix, UserFinder log label fix; new ActivityLogForm (live activity-log viewer,
  Info menu → "Activity log": Snapshot backfill on show, EntryAdded via BeginInvoke while visible,
  autoscroll/copy/clear, hide-on-close, disposed with MainFrame).
- `cf7235c` — Chunk 3: Redgifs ReparseMissing give-up budget (MaxReparseMissingAttempts=10,
  410/404 permanent-gone short-circuit, attempt counting on all failure paths, _ForceSaveUserData
  persistence, Finally bounds guard) — mirrors the Reddit mechanism.
- `ea8ca84` — Chunk 4: Instagram width/height copy-paste in ObtainMedia_SetReelsFunc; TikTok
  repost createTime read from document root instead of post item; TikTok ReparseMissing overhaul
  (Missing-state stamping so DownloadMissingOnly doesn't discard replacements while deleting the
  originals, MaxReparseMissingAttempts=10 give-up budget, per-post success tracking for multi-image
  sibling removal).
- `edc6dda` — Chunk 5: zombie special feeds (missing BTT_FEED_ADD_SPEC_REMOVE in both
  Feed_FeedRemoved fan-outs, centralized feed disposal in Feeds_FeedDeleted, FeedDeleted handler
  attached in Collection.Add/Favorite, dead FindIndex/Dispose block removed from Collection.Delete);
  removed blocking full GC from ClearTable (per page flip, UI thread); shared Lazy LibVLC engine +
  Media disposal in FeedVideo (was: one never-disposed native engine per video tile); Feed readers of
  Downloader.Files converted to FeedDataLock via new TDownloader helpers
  FilesSnapshot/FilesRemoveAll/FilesClear/FilesLocked.
- `bed3d11` — Chunk 5.5: Instagram ReparseMissing override (user-approved feature) — see the
  chunk-5.5 Reviewed section for design details.
- `5cf7433` — Post-review feature (user request): **file timestamps set to the post date**.
  New `UserDataBase.SetContentFileDate(f, PostDate)` stamps a downloaded file's Created + Modified
  time with `UserPost.Date` (already parsed from every site's API — Reddit `created`, RedGifs
  `createDate`, IG `taken_at`, TikTok `createTime`), called at the download-completion point in
  `DownloadContentDefault` for the media file + any text sidecar. Makes date-sorted folders match
  post chronology independent of download order. Chose post date over the literal HTTP Last-Modified:
  the CDN header is not cleanly retrievable through the closed-source download client (`WebClient2`
  captures no response headers) and often reflects cache/processing time, not post time. No-ops on
  date-less items (keep download time), deduped files, or OS-rejected values (year ≤ 1601 guard);
  wrapped in try/catch so a timestamp failure never disturbs the download. Global default per user.
- `975b23b` — Post-review feature (user request): download content **oldest-first**. Sites
  discover newest-first, so `_ContentNew` was assembled + downloaded newest→oldest; now reversed
  once in `UserDataBase.DownloadData` (non-subscription branch, before `DownloadContent`) so oldest
  posts hit disk first (file timestamps follow post chronology; filenames unaffected — URL-derived).
  `DownloadTopCount` ("newest N") preserved by trimming to the newest N (front of the list) BEFORE
  reversing, since `DownloadContentDefault` enforces the limit in list order. Scope: all normal
  timeline sites (incl. Reddit/RedGifs/Instagram/TikTok); subscription sites (OnlyFans/JFF/etc.)
  deliberately untouched — their `_ContentNew` is doubled with Downloaded copies, which a flat
  reverse would interleave. Chosen as global default (no toggle) per user. Minor accepted nuances:
  (a) within one gallery post, image order flips too (same instant — chronologically irrelevant);
  (b) `DownloadTopCount` with mid-batch failures now attempts at most the newest N rather than
  topping up from older posts to reach N successes.
- `612ebbf` — Post-review (user request): cooldown/pause events now go to the ACTIVITY LOG,
  not MyMainLOG — convention: MyMainLOG = errors, ActivityLog = live health. Moved: Instagram
  Ready() rate-limit wait + NextRequest self-throttle, Reddit Wait429 self-throttle (these three
  were review-era MyMainLOG additions). NetworkBreaker trip/restore/give-up now ALSO write activity
  lines (MyMainLOG kept there — connectivity loss is an abnormal event, not a planned cooldown).
- `3994234` — Chunk 6: TDownloader unavailable-host users → KeysSkipped (no more unpaired
  AfterDownload / false "completed"); per-host batch fill instead of Exit For at first full host;
  UserDataBase stxt Continue For → Skipped fall-through; ProfileSaved counter swap; Feed focus steal
  gated on ActiveForm; FeedMedia unconditional event unsubscribe on dispose.

## Open Suspicions

*(one half of a cross-module interaction seen; verify when the other half is read)*

- ~~`DownloadMissingOnly`/`UserExists` audit~~ **RESOLVED (chunk 4)**: Reddit + Redgifs carry the probe; TikTok can't (no UserExists mechanism — attempts budget bounds it instead); Instagram is moot (no ReparseMissing override at all — see chunk 4 notes; possible user-approved feature).
- ~~The ReparseMissing fixes (Mastodon/OnlyFans/ThreadsNet/Twitter/Reddit/Redgifs) each hand-roll the existence-probe pattern; a shared base hook remains a chunk-6 refactor option, but no NEW site needed the probe in chunk 4, so pressure is low.~~ **RESOLVED (chunk 6): won't-do** — see chunk-6 section. *(No Open Suspicions remain.)*
- ~~Chunk 5: check whether Feed code compensates for the old broken `UserMedia.New(EContainer)` path reconstruction (fixed in 53ca6a5)~~ **RESOLVED (chunk 5): no double-correction** — the only compensation is `FeedMedia.FileCheckSpecialFolders`, gated by `If Not File.Exists`: when the 53ca6a5 reconstruction is right it never runs; when the file is genuinely missing, appending another folder still yields a nonexistent path and the tile is discarded exactly as before. Harmless legacy fallback for pre-fix session XMLs; left in place.

## PersonalUtilities Hazards (closed-source, work around only)

- `Responser._ErrorProcessor` is left uninitialised → its own catch block throws NullReferenceException on HTTP-level errors. Workaround: `SafeGetResponse` in `UserDataBase.vb`.
- (append others as discovered)
