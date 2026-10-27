#!/usr/bin/env bash
# PreToolUse on the edit tools. See docs/REVIEW.md → "Hooks".
. "${0%/*}/common.sh"

# Write and Edit send file_path, NotebookEdit notebook_path.
targets() {
  jq -r '.tool_input | (.file_path, .notebook_path) | strings' <<<"$input"
}

# Resolves . and .. without touching the file system, since the file may not exist yet.
normalised() {
  local path=$1 part out=''
  set -f
  case $path in /*) ;; *) path="$cwd/$path" ;; esac
  local IFS=/
  for part in $path; do
    case $part in
      '' | .) ;;
      ..) out=${out%/*} ;;
      *) out="$out/$part" ;;
    esac
  done
  set +f
  echo "${out:-/}"
}

while IFS= read -r target; do
  [ -n "$target" ] || continue
  path=$(normalised "$target")
  case $path in
    /tmp | /tmp/* | /private/tmp | /private/tmp/*)
      deny "Do not write scratch files to /tmp: use .scratch/ in this checkout and delete them when done (AGENTS.md → \"Working copy\")."
      ;;
  esac
done < <(targets)
exit 0
