---
codex: 1
project: MindAttic.Media
code: MAM
layer: stories
status: living
updated: 2026-10-12
---

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
| MAM-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| MAM-T2 | 2026-10-14 | chore | open | NUnit tests for MediaStreams.CopyAndHashAsync and MediaNaming.SanitizeFileName: hash and length match SHA256 of the source for empty, single-byte, exactly-buffer-size, and multi-buffer payloads, lowercase hex output, bufferSize of 0 or negative falls back to the default, cancellation propagates, a non-seekable source is read once; SanitizeFileName strips directories, replaces invalid chars with _, and returns file for empty/whitespace/dir-only input - these back every upload's hash and stored file name | — |
| MAM-T3 | 2026-10-14 | chore | open | NUnit tests for AzureBlobUrlSigner user-delegation path (no Azurite needed, mock BlobServiceClient/container via AzureBlobContainerFactory seam): when the blob cannot generate a key SAS, a user-delegation SAS is built with Read permission, Resource b, StartsOn 5 min back, ContentType from the item and the account name; the delegation key is fetched once and reused while it has more than 5 min of validity, refreshed after, and concurrent callers share a single fetch; a null key declines (returns null); PublicRead with PublicBaseUri rebases onto the CDN host keeping the blob path, without it returns the raw blob URI - Entra-configured deployments rely on this branch and it is untested | — |
| MAM-T4 | 2026-10-14 | chore | open | NUnit tests for AddMedia/AddMediaAzure DI wiring: AddMedia registers a scoped LocalDiskMediaStore and applies configureOptions (or defaults when null); AddMediaAzure replaces a previously registered IMediaStore and IMediaUrlSigner rather than adding a second, registers AzureBlobContainerFactory as a singleton once even when called twice, and mirrors MediaRoot/InlineThresholdBytes/SignedUrlLifetime/CacheMaxAgeSeconds/CopyBufferSize from AzureMediaOptions into MediaStoreOptions so the /_media endpoint sees the same knobs; AzureBlobContainerFactory prefers ConnectionString over BlobServiceUri - a mismatch here silently serves wrong cache/lifetime values | — |
| MAM-T5 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | WF-T14 |
