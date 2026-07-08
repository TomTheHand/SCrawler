# SCrawler Incremental Review Ledger

Persistent state for the multi-session code review + hardening effort.
Any Claude session (any model) resumes from this file — read it fully before touching code.

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
| 5 | `Download\Feed` — responsiveness/jank focus | 6.1k | pending |
| 6 | Ledger sweep: resolve all remaining Open Suspicions | — | pending |

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
- Feed reads `Downloader.Files` without `FeedDataLock` — readers still unsynchronized (chunk 5).
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
- *(this commit)* — Chunk 4: Instagram width/height copy-paste in ObtainMedia_SetReelsFunc; TikTok
  repost createTime read from document root instead of post item; TikTok ReparseMissing overhaul
  (Missing-state stamping so DownloadMissingOnly doesn't discard replacements while deleting the
  originals, MaxReparseMissingAttempts=10 give-up budget, per-post success tracking for multi-image
  sibling removal).

## Open Suspicions

*(one half of a cross-module interaction seen; verify when the other half is read)*

- ~~`DownloadMissingOnly`/`UserExists` audit~~ **RESOLVED (chunk 4)**: Reddit + Redgifs carry the probe; TikTok can't (no UserExists mechanism — attempts budget bounds it instead); Instagram is moot (no ReparseMissing override at all — see chunk 4 notes; possible user-approved feature).
- The ReparseMissing fixes (Mastodon/OnlyFans/ThreadsNet/Twitter/Reddit/Redgifs) each hand-roll the existence-probe pattern; a shared base hook remains a chunk-6 refactor option, but no NEW site needed the probe in chunk 4, so pressure is low.
- Chunk 5: check whether Feed code compensates for the old broken `UserMedia.New(EContainer)` path reconstruction (fixed in 53ca6a5) — a compensating hack there would now double-correct.

## PersonalUtilities Hazards (closed-source, work around only)

- `Responser._ErrorProcessor` is left uninitialised → its own catch block throws NullReferenceException on HTTP-level errors. Workaround: `SafeGetResponse` in `UserDataBase.vb`.
- (append others as discovered)
