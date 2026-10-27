#!/usr/bin/env bash
# PreToolUse on Bash. See docs/REVIEW.md → "Hooks".
# Each check_* function returns to allow, and denies through deny(), which ends the script.
. "${0%/*}/common.sh"

main_branch=main
# Must match .github/workflows/pr-title.yml; HookSettingsSpec fails when they differ.
title_types='feat fix docs chore refactor test build ci perf style revert'
title_pattern="^(${title_types// /|})\\((#[1-9][0-9]*(, #[1-9][0-9]*)*)\\): [A-Z].+\$"

working_copy='AGENTS.md → "Working copy"'
receipts='docs/REVIEW.md → "Review receipts"'
receipt_script=$hooks_dir/../../.github/scripts/review-receipt.sh

# One line per switch or checkout earlier in the command line: the checkout's top level, a
# tab, and the branch it moved to; '?' when the hook cannot tell, which allows the call.
switches=''

# What a commit earlier in the line will commit, as review-receipt.sh check measures it:
# --index, --tracked with `commit -a`, or --worktree after a `git add -A`; empty without a commit.
committed=''
added=0
# The last git command earlier in the line that changes what a push sends in a way the hook
# cannot measure: one that moves HEAD, or a `git add` of only some paths.
moved=''

top_of() { git -C "$1" rev-parse --show-toplevel 2>/dev/null; }

branch_in() {
  local top line branch=''
  top=$(top_of "$1")
  while IFS= read -r line; do
    [ "${line%%$'\t'*}" = "$top" ] && branch=${line#*$'\t'}
  done <<<"$switches"
  if [ -n "$branch" ]; then
    echo "$branch"
  else
    git -C "$1" symbolic-ref --short -q HEAD 2>/dev/null
  fi
}

# Records where `git switch` or `git checkout` with these arguments leaves HEAD. A checkout
# with paths, or of a single path, restores files and leaves HEAD where it was; a trailing
# `--` with nothing after it only marks the branch.
note_switch() {
  local dir=$1 sub=$2 first='' count=0 target='' track=0 created=0 a
  shift 2
  while [ $# -gt 0 ] && [ -z "$target" ]; do
    case $1 in
      -c | -C | --create | --force-create | -b | -B) target=$2; created=1 ;;
      --create=* | --force-create=*) target=${1#*=}; created=1 ;;
      -[fqmt][cCbB] | -[fqmt][fqmt][cCbB]) target=$2; created=1 ;;
      -[cCbB]?*) target=${1#-?}; created=1 ;;
      --orphan) target=$2; moved=$sub ;;
      --orphan=*) target=${1#*=}; moved=$sub ;;
      --detach | -d | -) target='?' ;;
      -t | --track | --track=*) track=1 ;;
      -p | --patch) return 0 ;;
      --) [ "$sub" = checkout ] && [ $# -gt 1 ] && return 0 ;;
      -*) ;;
      *) count=$((count + 1)); [ -n "$first" ] || first=$1 ;;
    esac
    shift
  done
  if [ $created = 1 ]; then
    # A start point makes the new branch something other than HEAD.
    [ "${1:-}" = "$target" ] && shift
    for a in "$@"; do
      case $a in -*) ;; *) moved=$sub ;; esac
    done
  fi
  if [ -z "$target" ] && [ -n "$first" ]; then
    if [ $track = 1 ]; then
      # git creates a local branch named after the remote one.
      target=${first#*/}
    elif [ "$sub" = switch ]; then
      target=$first
      case $first in *@\{*) target='?' ;; esac
    elif [ $count = 1 ]; then
      if git -C "$dir" show-ref -q --verify "refs/heads/$first"; then
        target=$first
      elif git -C "$dir" rev-parse -q --verify "$first^{commit}" >/dev/null; then
        target='?'
      elif [ -n "$(git -C "$dir" for-each-ref --count=1 --format=x "refs/remotes/*/$first")" ]; then
        # git creates a local branch tracking the remote one.
        target=$first
      fi
    fi
  fi
  [ -n "$target" ] && switches="$switches
$(top_of "$dir")	$target"
  return 0
}

check_git() {
  local dir=$1 i=0 sub a
  shift
  local -a args=("$@")
  skip_git_options
  sub=${args[$i]}
  args=("${args[@]:$((i + 1))}")
  case $sub in
    stash)
      case ${args[0]} in
        list | show) ;;
        *) deny "git stash is not allowed ($working_copy): never stash or discard uncommitted changes; if they block your work, stop and ask the human." ;;
      esac
      ;;
    worktree)
      [ "${args[0]}" = list ] || deny "git worktree is not allowed ($working_copy): work only in this checkout, on a feature branch."
      ;;
    clone)
      deny "git clone is not allowed ($working_copy): work only in this checkout; do not copy the repository anywhere."
      ;;
    reset)
      for a in "${args[@]}"; do
        [ "$a" = --hard ] && deny "git reset --hard is not allowed ($working_copy): it discards uncommitted changes; stop and ask the human."
      done
      moved=reset
      ;;
    commit)
      for a in "${args[@]}"; do
        [ "$a" = --dry-run ] && return
      done
      [ "$(branch_in "$dir")" = "$main_branch" ] \
        && deny "Do not commit on $main_branch ($working_copy): create a feature branch with git switch -c; $main_branch changes only through a merged PR (docs/REVIEW.md → \"Merge gates\")."
      committed=--index
      [ $added = 1 ] && committed=--worktree
      local value=0
      for a in "${args[@]}"; do
        if [ $value = 1 ]; then
          value=0
          continue
        fi
        case $a in
          --all) [ "$committed" = --worktree ] || committed=--tracked ;;
          # Paths, or --only and --include, commit some files from the working tree.
          --only | --include | --pathspec-from-file*) moved=commit ;;
          --message | --file | --reuse-message | --reedit-message | --template | --author | --date \
            | --cleanup | --fixup | --squash | --trailer) value=1 ;;
          --) moved=commit ;;
          --*) ;;
          -*)
            # Short options combine, as in -sam; letters after one that takes a value are its value.
            a=${a#-}
            case ${a%%[mFCctuS]*} in *a*) [ "$committed" = --worktree ] || committed=--tracked ;; esac
            case ${a%%[mFCctuS]*} in *[oi]*) moved=commit ;; esac
            # A value letter takes the next argument only when nothing follows it.
            case ${a#"${a%%[mFCctuS]*}"} in [mFCct]) value=1 ;; esac
            ;;
          *) moved=commit ;;
        esac
      done
      ;;
    add)
      # Only `git add -A`, `.` or `:/` from the top stages what `review-receipt.sh check --worktree`
      # measures, so the hook can measure the commit that follows.
      local whole=0 paths=0
      for a in "${args[@]}"; do
        case $a in
          -A | --all | :/) whole=1 ;;
          .) [ "$(top_of "$dir")" = "$(cd "$dir" && pwd -P)" ] && whole=1 || paths=1 ;;
          --) ;;
          -*) ;;
          *) paths=1 ;;
        esac
      done
      if [ $whole = 1 ] && [ $paths = 0 ]; then
        added=1
      else
        moved=add
      fi
      ;;
    merge | rebase | cherry-pick | pull | am | revert | restore | rm | mv | apply | update-ref) moved=$sub ;;
    branch)
      for a in "${args[@]}"; do
        case $a in -f | --force | -m | -M | -c | -C | --move | --copy) moved=branch ;; esac
      done
      ;;
    fetch)
      for a in "${args[@]}"; do
        case $a in *:*) moved=fetch ;; esac
      done
      ;;
    push) check_push "$dir" "${args[@]}" ;;
    switch | checkout)
      for a in "${args[@]}"; do
        case $a in -C | -B | --force-create | --force-create=*) moved=$sub ;; esac
      done
      note_switch "$dir" "$sub" "${args[@]}"
      ;;
  esac
}

check_push() {
  local dir=$1 i=0 a all=0 tags=0 skip=0 dst branch
  shift
  local -a args=("$@") positional=()
  while [ $i -lt ${#args[@]} ]; do
    a=${args[$i]}
    case $a in
      --repo | -o | --push-option | --receive-pack | --exec) i=$((i + 1)) ;;
      --all | --mirror | --branches) all=1 ;;
      --tags) tags=1 ;;
      --delete | -d | --dry-run | -n) skip=1 ;;
      --) positional+=("${args[@]:$((i + 1))}"); break ;;
      -*) ;;
      *) positional+=("$a") ;;
    esac
    i=$((i + 1))
  done
  local reason="Do not push to $main_branch ($working_copy): push your feature branch and open a PR; tags and release/vX.Y branches are fine."
  [ $all = 1 ] && deny "$reason"
  branch=$(branch_in "$dir")
  if [ ${#positional[@]} -lt 2 ]; then
    [ $tags = 0 ] || return
    [ "$branch" = "$main_branch" ] && deny "$reason"
    [ $skip = 1 ] || check_receipt "$dir" "$branch"
    return
  fi
  for a in "${positional[@]:1}"; do
    a=${a#+}
    dst=${a#*:}
    case $dst in HEAD | @) dst=$branch ;; esac
    dst=${dst#refs/heads/}
    [ "$dst" = "$main_branch" ] && deny "$reason"
  done
  [ $skip = 1 ] && return
  for a in "${positional[@]:1}"; do
    a=${a#+}
    dst=${a#*:}
    case $dst in refs/tags/* | release/v[0-9]* | refs/heads/release/v[0-9]*) continue ;; esac
    a=${a%%:*}
    case $a in HEAD | @) [ -n "$branch" ] && a=$branch ;; esac
    check_receipt "$dir" "$a"
  done
}

common_dir() { git -C "$1" rev-parse --path-format=absolute --git-common-dir 2>/dev/null; }

# Denies the push of a branch or commit of this repository, other than a release branch,
# unless review-receipt.sh finds a receipt for what it pushes. A commit earlier in the line is
# measured from the index or working tree it will commit; a source the hook cannot tell, a tag,
# or a missing script is allowed, and a receipt check that fails denies with its reason.
check_receipt() {
  local dir=$1 name=${2#refs/heads/} rev='' out status
  case $name in '' | '?' | "$main_branch" | release/v[0-9]* | refs/*) return ;; esac
  [ "$(common_dir "$dir")" = "$(common_dir "$root")" ] || return 0
  [ -n "$moved" ] && deny "Run git push as its own command after git $moved, so the hook can check the review receipt for what it pushes ($receipts)."
  case $name in "$(branch_in "$dir")" | HEAD | @) rev=$committed ;; esac
  if [ -z "$rev" ]; then
    if git -C "$dir" show-ref -q --verify "refs/heads/$name"; then
      rev=refs/heads/$name
    elif git -C "$dir" show-ref -q --verify "refs/tags/$name"; then
      return 0
    elif git -C "$dir" rev-parse -q --verify "$name^{commit}" >/dev/null; then
      # A commit named another way: a SHA, HEAD~1, or HEAD when detached.
      rev=$name
    elif [ "$name" = "$(branch_in "$dir")" ]; then
      # A branch this line creates from HEAD.
      rev=HEAD
    else
      return 0
    fi
  fi
  out=$(bash "$receipt_script" -C "$dir" check "$rev" 2>&1)
  status=$?
  case $status in
    1) deny "No review receipt matches $name ($receipts): run the review-change skill on the change, which records one when the review passes; any change after the review needs a new review." ;;
    2) deny "The review receipt check failed for $name ($receipts): ${out:-no reason given}" ;;
  esac
  return 0
}

check_gh() {
  local i=0 a action=$2 title='' has_title=0
  [ "$1" = pr ] || return
  case $action in create | edit) ;; *) return ;; esac
  shift 2
  local -a args=("$@")
  while [ $i -lt ${#args[@]} ]; do
    a=${args[$i]}
    case $a in
      -t | --title) title=${args[$((i + 1))]}; has_title=1; i=$((i + 1)) ;;
      --title=*) title=${a#--title=}; has_title=1 ;;
      -t?*) title=${a#-t}; has_title=1 ;;
      -b | --body | -F | --body-file | -B | --base | -H | --head | -a | --assignee | -l | --label \
        | -m | --milestone | -p | --project | -r | --reviewer | -R | --repo | -T | --template | --recover \
        | --add-label | --remove-label | --add-reviewer | --remove-reviewer | --add-assignee \
        | --remove-assignee | --add-project | --remove-project) i=$((i + 1)) ;;
    esac
    i=$((i + 1))
  done
  if [ $has_title = 0 ]; then
    [ "$action" = create ] && deny "Pass the PR title with --title so it can be checked: type(#123): Description (docs/REVIEW.md → \"PR title and commit hygiene\")."
    return
  fi
  # A title built at run time is checked by CI only.
  case $title in *'$'* | *'`'*) return ;; esac
  [[ $title =~ $title_pattern ]] \
    || deny "PR title \"$title\" fails CI's PR title check: use type(#123): Description, or type(#123, #124): Description, with a type from: $title_types (docs/REVIEW.md → \"PR title and commit hygiene\")."
}

check_line() {
  local dir=$2 depth=$3 name here
  local -a w
  while IFS=$'\037' read -r -a w; do
    here=$dir
    skip_prefixes
    [ ${#w[@]} -gt 0 ] || continue
    name=${w[0]##*/}
    case $name in
      git) check_git "$here" "${w[@]:1}" ;;
      gh)
        w=("${w[@]:1}")
        while [ ${#w[@]} -gt 0 ]; do
          case ${w[0]} in
            -R | --repo) w=("${w[@]:2}") ;;
            --repo=*) w=("${w[@]:1}") ;;
            *) break ;;
          esac
        done
        check_gh "${w[@]}"
        ;;
      cd) [ ${#w[@]} -gt 1 ] && dir=$(path_in "$here" "${w[1]}") ;;
      bash | sh | zsh)
        if [ "$depth" -lt 3 ]; then
          local j=1
          while [ $j -lt ${#w[@]} ] && [[ ${w[$j]} == -* ]]; do
            if [[ ${w[$j]} == -*c* && ${w[$j]} != --* ]]; then
              check_line "${w[$((j + 1))]}" "$here" $((depth + 1))
              break
            fi
            j=$((j + 1))
          done
        fi
        ;;
    esac
  done < <(printf '%s' "$1" | awk -f "$hooks_dir/words.awk")
}

check_line "$(jq -r '.tool_input.command // empty' <<<"$input")" "$cwd" 0
exit 0
