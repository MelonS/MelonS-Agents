#!/usr/bin/env bash
# scripts/apply-assist.sh — apply-link helpers.
#
# Different KR job boards expose different "apply" surfaces:
#
#   - 원티드 (Wanted): each posting has a per-posting URL that
#     redirects to an authenticated apply flow when clicked.
#     The skill surfaces the posting URL directly.
#   - 프로그래머스 (Programmers): apply happens through
#     programmers.co.kr/job/<id>/apply.  When the source's raw
#     JSON does not include an apply_url, this script derives it
#     from the posting URL by pattern.
#   - 잡코리아 / 사람인: apply requires login + per-posting
#     interaction.  The skill surfaces the listing URL; the
#     "apply" word in the digest is descriptive, not deep-linked.
#
# This script provides a single derive_apply_url() function the
# orchestrator can call when a source's raw JSON omits
# apply_url.  It is purely string-rewriting; no network.
#
# Usage: dot-sourced by scripts/run.sh.

# shellcheck shell=bash

derive_apply_url() {
  # Args: $1 = source name, $2 = posting URL
  # Echoes the apply URL on stdout.  Falls back to the posting URL
  # if no rewrite rule matches.
  local source="$1"
  local posting_url="$2"

  case "$source" in
    kr-wanted)
      # Wanted: posting URL already routes to the apply flow when
      # the user is logged in.  No rewrite.
      echo "$posting_url"
      ;;
    kr-programmers)
      # Programmers: programmers.co.kr/job/<id> → /job/<id>/apply
      if [[ "$posting_url" =~ programmers\.co\.kr/(job|company/jobs)/([0-9]+) ]]; then
        echo "https://programmers.co.kr/job/${BASH_REMATCH[2]}/apply"
      else
        echo "$posting_url"
      fi
      ;;
    kr-jobkorea)
      # JobKorea: posting URL → posting URL + ?action=apply (no
      # deep-link guarantee; site requires login).  Best-effort.
      echo "${posting_url}?action=apply"
      ;;
    kr-saramin)
      # Saramin: same posting URL serves listing + apply (login
      # required).  No rewrite.
      echo "$posting_url"
      ;;
    _mock)
      # Mock source supplies its own apply URLs; this path is
      # only hit when the raw JSON omits one.  Use the posting URL.
      echo "$posting_url"
      ;;
    *)
      echo "$posting_url"
      ;;
  esac
}
