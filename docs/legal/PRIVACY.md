# Privacy Policy

**Last updated:** 2026-05-21

## Overview

This privacy policy applies to the `ToddStudio Upload` application
("the App"), a single-creator content automation tool that publishes
the developer's own original music videos to the developer's own
content channels via official platform APIs (YouTube Data API v3,
TikTok Content Posting API).

The App is a personal-use tool with one user (the developer/operator).
It is **not** distributed publicly and does not serve other users'
data.

## Data the App accesses

When granted access via OAuth, the App may read and write the
following on the developer's own platform accounts:

- **Upload videos** (the developer's own content) to the developer's
  own channel.
- **Read video metadata** (titles, descriptions, scheduled times,
  view counts) for the developer's own uploads, for analytics
  purposes.
- **Update scheduled publish times** on the developer's own videos.

The App **does not**:

- Read or write to any other user's account.
- Collect, store, or transmit any third-party user data.
- Sell or share data with any third party.
- Display ads or run analytics on end-viewers.

## Storage

OAuth refresh tokens are stored locally on the developer's machine
under `~/.config/` (filesystem permissions restricted to the local
user account).  No cloud storage of credentials, no remote servers.

## Revocation

The developer can revoke the App's access at any time:

- YouTube: <https://myaccount.google.com/permissions>
- TikTok: TikTok app → Settings → Connected apps → Remove

Revocation immediately invalidates all tokens; the App stops
functioning until re-authorized.

## Contact

Issues, questions, or revocation problems: open an issue at
<https://github.com/MelonS/MelonS-Agents/issues>.
