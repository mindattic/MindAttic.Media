---
codex: 1
project: MindAttic.Media
code: M.MDIA
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Workflow/data/workflow.db by `workflow db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the workflow CLI. -->

# MindAttic.Media — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| M.MDIA-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| M.MDIA-T2 | 2026-10-14 | chore | closed | NUnit tests for MediaStreams.CopyAndHashAsync and MediaNaming.SanitizeFileName: hash and length match SHA256 of the source for empty, single-byte, exactly-buffer-size, and multi-buffer payloads, lowercase hex output, bufferSize of 0 or negative falls back to the default, cancellation propagates, a non-seekable source is read once; SanitizeFileName strips directories, replaces invalid chars with _, and returns file for empty/whitespace/dir-only input - these back every upload's hash and stored file name | 2bc3ccf |
| M.MDIA-T3 | 2026-10-14 | chore | open | NUnit tests for AzureBlobUrlSigner user-delegation path (no Azurite needed, mock BlobServiceClient/container via AzureBlobContainerFactory seam): when the blob cannot generate a key SAS, a user-delegation SAS is built with Read permission, Resource b, StartsOn 5 min back, ContentType from the item and the account name; the delegation key is fetched once and reused while it has more than 5 min of validity, refreshed after, and concurrent callers share a single fetch; a null key declines (returns null); PublicRead with PublicBaseUri rebases onto the CDN host keeping the blob path, without it returns the raw blob URI - Entra-configured deployments rely on this branch and it is untested | — |
| M.MDIA-T4 | 2026-10-14 | chore | open | NUnit tests for AddMedia/AddMediaAzure DI wiring: AddMedia registers a scoped LocalDiskMediaStore and applies configureOptions (or defaults when null); AddMediaAzure replaces a previously registered IMediaStore and IMediaUrlSigner rather than adding a second, registers AzureBlobContainerFactory as a singleton once even when called twice, and mirrors MediaRoot/InlineThresholdBytes/SignedUrlLifetime/CacheMaxAgeSeconds/CopyBufferSize from AzureMediaOptions into MediaStoreOptions so the /_media endpoint sees the same knobs; AzureBlobContainerFactory prefers ConnectionString over BlobServiceUri - a mismatch here silently serves wrong cache/lifetime values | — |
| M.MDIA-T5 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | M.FLOW-T14 |
| M.MDIA-T6 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 9 .prose/commands stubs for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| M.MDIA-T7 | 2026-10-16 | chore | open | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | user request 2026-10-16: approved repo sweep |
| M.MDIA-T8 | 2026-10-16 | chore | open | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | user request 2026-10-16: approved repo sweep |
| M.MDIA-T9 | 2026-10-16 | chore | open | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | user request 2026-10-16: approved repo sweep |
| M.MDIA-T10 | 2026-10-16 | docs | open | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | user request 2026-10-16: approved repo sweep |
| M.MDIA-T11 | 2026-10-16 | docs | closed | Update USER_STORIES.md | — |
