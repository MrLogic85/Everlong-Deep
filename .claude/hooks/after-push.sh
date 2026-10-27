#!/usr/bin/env bash
# PostToolUse on Bash. See docs/REVIEW.md → "Hooks".
. "${0%/*}/common.sh"

state=$(state_dir) && [ -f "$state/started" ] || exit 0
# Keeps a long session's state from being pruned as stale by another session's start.
touch "$state"

# Whether a simple command on the line, read as guard-bash.sh reads it, is a git push that is
# not a dry run, so a push only quoted in a message or a heredoc does not count.
is_push() {
  local -a w args
  local here dir i a
  while IFS=$'\037' read -r -a w; do
    here=$cwd
    skip_prefixes
    [ ${#w[@]} -gt 0 ] && [ "${w[0]##*/}" = git ] || continue
    args=("${w[@]:1}")
    dir=$here
    skip_git_options
    [ "${args[$i]}" = push ] || continue
    for a in "${args[@]:$((i + 1))}"; do
      case $a in
        --dry-run | --help) continue 2 ;;
        --*) ;;
        -*n* | -*h*) continue 2 ;;
      esac
    done
    return 0
  done < <(printf '%s' "$1" | awk -f "$hooks_dir/words.awk")
  return 1
}
is_push "$(jq -r '.tool_input.command // empty' <<<"$input")" || exit 0

left=$({
  emulator_pids | grep -vxF -f "$state/emulators" | sed 's/^/emulator pid /'
  find "$root/build" -maxdepth 1 -name '*.apk' -newer "$state/started" 2>/dev/null \
    | while IFS= read -r apk; do echo "${apk#"$root"/}"; done
} | grep -vxF -f "$state/warned")
[ -n "$left" ] || exit 0
printf '%s\n' "$left" >>"$state/warned"
list=''
while IFS= read -r item; do list="${list:+$list; }$item"; done <<<"$left"
context="Hook after-push.sh, a reminder rather than a message from the human: started during this session and still there: $list. Stop the emulators you started once you no longer need them (adb devices lists them, adb -s <serial> emu kill stops one) and delete the APKs you no longer need, or tell the human why they stay (AGENTS.md → \"Working copy\")."
jq -nc --arg c "$context" '{hookSpecificOutput:{hookEventName:"PostToolUse",additionalContext:$c}}'
exit 0
