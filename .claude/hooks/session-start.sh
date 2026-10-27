#!/usr/bin/env bash
# SessionStart. See docs/REVIEW.md → "Hooks".
command -v jq >/dev/null 2>&1 || {
  echo 'jq is not installed, so the repository hooks are off (README.md → "Tools").'
  exit 0
}
. "${0%/*}/common.sh"

note=''
# A resumed or compacted session keeps its state, and its changes are its own.
case $(jq -r '.source // empty' <<<"$input") in resume | compact) fresh=0 ;; *) fresh=1 ;; esac
if state=$(state_dir); then
  if [ -f "$state/started" ]; then
    fresh=0
  else
    find "${state%/*}" -mindepth 1 -maxdepth 1 -type d -mmin +2880 -exec rm -rf {} + 2>/dev/null
    mkdir -p "$state" && emulator_pids >"$state/emulators" && : >"$state/warned" && touch "$state/started"
  fi
fi
[ -n "$state" ] && [ -f "$state/started" ] \
  || note="
Could not record the session's state, so the after-push check for emulators and APKs is off."

# The C# language server the LSP tool starts is a local dotnet tool. Restoring it takes a
# fraction of a second once it is in the NuGet cache, but a download can stall, so the hook waits
# at most EVERLONG_DEEP_RESTORE_WAIT seconds and leaves the restore running after that.
if [ $fresh = 1 ] && [ -f "$root/dotnet-tools.json" ] && command -v dotnet >/dev/null 2>&1; then
  (cd "$root" && exec dotnet tool restore) </dev/null >/dev/null 2>&1 &
  restore=$! tenths=0 limit=$((${EVERLONG_DEEP_RESTORE_WAIT:-10} * 10))
  while kill -0 $restore 2>/dev/null && [ $tenths -lt $limit ]; do
    sleep 0.1
    tenths=$((tenths + 1))
  done
  if kill -0 $restore 2>/dev/null; then
    note="${note:-}
\`dotnet tool restore\` is still running, so the LSP tool may fail to start the C# language server for a while; find callers and definitions with \`git grep\` until it works."
  elif ! wait $restore; then
    note="${note:-}
\`dotnet tool restore\` failed, so the LSP tool cannot start the C# language server; find callers and definitions with \`git grep\` instead."
  fi
fi

if ! changes=$(git -C "$root" status --porcelain 2>/dev/null); then
  context="Could not read the working copy (git status failed in $root). Ask the human before starting work (AGENTS.md → \"Working copy\")."
else
  branch=$(git -C "$root" symbolic-ref --short -q HEAD) || branch='(detached HEAD)'
  if [ -z "$changes" ]; then
    summary="no uncommitted changes."
  else
    count=$(printf '%s\n' "$changes" | wc -l | tr -d ' ')
    summary="$count uncommitted changes:
$(printf '%s\n' "$changes" | head -n 10)"
    [ "$count" -gt 10 ] && summary="$summary
… and $((count - 10)) more."
  fi
  if [ $fresh = 1 ]; then
    context="Working copy at session start: branch $branch, $summary"
    [ -n "$changes" ] && context="$context
If these are not yours, or this is not the branch for your task, stop and ask the human before starting work (AGENTS.md → \"Working copy\")."
  else
    context="Working copy now: branch $branch, $summary"
  fi
fi
context="$context${note:-}"
jq -nc --arg c "$context" '{hookSpecificOutput:{hookEventName:"SessionStart",additionalContext:$c}}'
exit 0
