# Shared by the repository hooks (docs/REVIEW.md → "Hooks"). Sourced, never run.
# The hooks never exit non-zero, and allow the call when a tool they need is missing.
export LC_ALL=C
command -v jq >/dev/null 2>&1 || exit 0
hooks_dir=$(cd "${BASH_SOURCE[0]%/*}" && pwd)
input=$(cat)
cwd=$(jq -r '.cwd // empty' <<<"$input")
[ -d "$cwd" ] || cwd=$PWD
root=${CLAUDE_PROJECT_DIR:-$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null)}
root=$(cd "${root:-$cwd}" 2>/dev/null && pwd) || root=$cwd

# Denies the call and shows the agent the reason.
deny() {
  jq -nc --arg r "$1" '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'
  exit 0
}

# This session's state folder inside the git dir, so it stays out of the working tree.
state_dir() {
  local git_dir id
  git_dir=$(git -C "$root" rev-parse --absolute-git-dir 2>/dev/null) || return 1
  id=$(jq -r '.session_id // empty' <<<"$input" | tr -cd 'A-Za-z0-9_-')
  [ -n "$id" ] || return 1
  echo "$git_dir/agent-hooks/$id"
}

# PIDs of this user's running Android emulators: processes whose program is qemu-system*.
emulator_pids() {
  ps -U "$(id -u)" -o pid=,args= | awk '{ pid = $1; sub(/^ *[0-9]+ +/, ""); if (/^([^ ]*\/)?qemu-system[^\/ ]*( |$)/) print pid }'
}

path_in() {
  case $2 in
    /*) echo "$2" ;;
    *) echo "$1/$2" ;;
  esac
}

# Drops the words before a simple command's program from the caller's array w, as read by
# words.awk: assignments, prefixes such as command or if, and env with its options. env -C
# changes the caller's here, the directory, for its one command only.
skip_prefixes() {
  local dir=$here
  while [ ${#w[@]} -gt 0 ]; do
    case ${w[0]} in
      [A-Za-z_]*=*) [[ ${w[0]} =~ ^[A-Za-z_][A-Za-z0-9_]*= ]] || break ;;
      command | builtin | exec | nohup | time | sudo | if | then | else | elif | do | while | until | '!' | '{') ;;
      env)
        while [ ${#w[@]} -gt 1 ]; do
          case ${w[1]} in
            -u | --unset) w=("${w[@]:1}") ;;
            -C | --chdir) here=$(path_in "$dir" "${w[2]}"); w=("${w[@]:1}") ;;
            --chdir=*) here=$(path_in "$dir" "${w[1]#--chdir=}") ;;
            -C?*) here=$(path_in "$dir" "${w[1]#-C}") ;;
            -* | [A-Za-z_]*=*) ;;
            *) break ;;
          esac
          w=("${w[@]:1}")
        done
        ;;
      *) break ;;
    esac
    w=("${w[@]:1}")
  done
}

# Skips git's global options in the caller's array args, the words after git, leaving i at the
# subcommand; git -C changes the caller's dir.
skip_git_options() {
  i=0
  while [ $i -lt ${#args[@]} ]; do
    case ${args[$i]} in
      -C) dir=$(path_in "$dir" "${args[$((i + 1))]}"); i=$((i + 2)) ;;
      -c | --git-dir | --work-tree | --namespace | --exec-path | --config-env | --super-prefix) i=$((i + 2)) ;;
      -*) i=$((i + 1)) ;;
      *) break ;;
    esac
  done
}
