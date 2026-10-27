#!/usr/bin/env bash
# SessionEnd. See docs/REVIEW.md → "Hooks".
. "${0%/*}/common.sh"

state=$(state_dir) && rm -rf "$state"
exit 0
