# Fork Release Sync Workflow

This fork-oriented workflow keeps `upstream-main` aligned with `babalae/better-genshin-impact` and writes a simple update report back into your fork.

## Branch assumptions

- `upstream-main`: clean mirror of `babalae/main`
- `release`: your real development / release branch

The workflow does **not** auto-merge into `release`. It only refreshes `upstream-main` and reports how far `release` has diverged.

## Version strategy

Use the upstream version as the base, then append the fork suffix for the multi-account line:

- upstream `0.59.2` -> fork `0.59.2-multi-account.1`
- upstream `0.59.2-alpha.2` -> fork `0.59.2-alpha.2.multi-account.1`

This keeps the upstream lineage visible while making the fork feature line explicit.

## Added workflow

File: `.github/workflows/fork_upstream_sync_report.yml`

It runs on:

- manual trigger (`workflow_dispatch`)
- daily schedule

It will:

1. fetch `upstream/main` and tags from `babalae/better-genshin-impact`
2. force-update `origin/upstream-main`
3. calculate how many new upstream commits arrived since the last sync
4. compare `release` against `upstream-main`
5. update or create an issue named `[bot] Upstream Sync Report`

## Expected report contents

The report issue includes:

- current upstream mirror SHA
- current upstream version
- suggested fork version for the multi-account line
- previous mirror SHA
- number of newly synced upstream commits
- latest upstream tag found on `main`
- `release` behind / ahead counts
- compare links for quick review
- recent upstream commit list

## Recommended release routine

1. Let the workflow refresh `upstream-main`
2. Read the updated sync report issue
3. Checkout `release`
4. Merge or rebase `upstream-main` into `release`
5. Update your fork release version to the reported `Suggested fork version`
6. Build and test locally
7. Publish your fork release from `release`

## Notes

- The workflow only runs on forks because it skips `babalae/better-genshin-impact`.
- It uses the default `GITHUB_TOKEN`, so no extra secret is required for the same repository.
- If your fork uses a different working branch name, update `RELEASE_BRANCH` in the workflow.
