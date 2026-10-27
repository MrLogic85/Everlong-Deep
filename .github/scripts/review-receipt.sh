#!/usr/bin/env bash
# Review receipts: the review-change skill writes one when the review gate passes, and
# .claude/hooks/guard-bash.sh denies a push of a branch without one. docs/REVIEW.md →
# "Review receipts" owns the rules.
#
#   review-receipt.sh [-C <dir>] write --reviewers <a,b,...> <findings.md>
#       Records a receipt for the reviewed change as staged; untracked files stay out.
#   review-receipt.sh [-C <dir>] check <rev> | --index | --tracked | --worktree
#       Exits 0 when a receipt matches or the change is empty, 1 when none matches, and 2
#       when it cannot tell.
#   review-receipt.sh [-C <dir>] base
#       Prints where HEAD left main, the commit to diff the change against.
#
# The key hashes the raw diff (modes, blob ids and paths) from where the change left main to
# the reviewed tree, so committing or rebasing the same change keeps the receipt valid, and
# any other change needs a new review.
set -u
export LC_ALL=C
script_dir=$(cd "${0%/*}" && pwd)

fail() {
  echo "review-receipt: $1" >&2
  exit 2
}

if [ "${1:-}" = -C ]; then
  cd "${2:-}" 2>/dev/null || fail "no such directory: ${2:-}"
  shift 2
fi
git rev-parse --git-dir >/dev/null 2>&1 || fail "not in a git repository"
receipts=$(git rev-parse --path-format=absolute --git-common-dir)/review-receipts

# Where <rev> left main: its newest common ancestor with the local or the remote main.
base_of() {
  local -a mains=()
  local ref
  for ref in refs/heads/main refs/remotes/origin/main; do
    git show-ref -q --verify "$ref" && mains+=("$ref")
  done
  [ ${#mains[@]} -gt 0 ] || return 1
  git merge-base "$1" "${mains[@]}"
}

# The tree `git add <mode>` would stage, without touching the index: -A for the whole working
# tree, -u for its tracked files only, as `git commit -a` takes them.
staged_tree() {
  local index tmp tree status err
  index=$(git rev-parse --path-format=absolute --git-path index) || return 1
  tmp=$index.review-receipt.$$
  if [ -f "$index" ]; then
    # Keeping its mtime lets git spot a file changed within the same second as the index.
    cp -p "$index" "$tmp" || return 1
  fi
  # git's warnings stay out of a successful run; its reason for a failure goes to stderr.
  if err=$(GIT_INDEX_FILE=$tmp git add "$1" 2>&1 >/dev/null); then
    tree=$(GIT_INDEX_FILE=$tmp git write-tree)
    status=$?
  else
    status=1
    printf '%s\n' "$err" >&2
  fi
  rm -f "$tmp"
  [ $status = 0 ] && echo "$tree"
}

# Sets base, tree and key for: a revision, --index, --tracked or --worktree. An empty key means an empty
# change.
measure() {
  local rev=HEAD diff
  case $1 in
    --index) tree=$(git write-tree) || return 1 ;;
    --tracked) tree=$(staged_tree -u) || return 1 ;;
    --worktree) tree=$(staged_tree -A) || return 1 ;;
    *) rev=$1; tree=$(git rev-parse -q --verify "$rev^{tree}") || return 1 ;;
  esac
  base=$(base_of "$rev") || return 1
  diff=$(git diff-tree -r --raw --no-abbrev --no-renames "$base" "$tree") || return 1
  key=''
  [ -z "$diff" ] || key=$(printf '%s\n' "$diff" | git hash-object --stdin)
}

write() {
  local reviewers='' findings=''
  while [ $# -gt 0 ]; do
    case $1 in
      --reviewers) reviewers=${2:-}; shift ;;
      --reviewers=*) reviewers=${1#--reviewers=} ;;
      -*) fail "unknown option: $1" ;;
      *) findings=$1 ;;
    esac
    shift
  done
  [ -n "$reviewers" ] || fail "name the reviewers that ran: --reviewers a,b"
  local name
  for name in ${reviewers//,/ }; do
    [ -f "$script_dir/../../.claude/agents/$name.md" ] || fail "no review agent named $name in .claude/agents"
  done
  [ -f "$findings" ] && grep -q '[^[:space:]]' "$findings" \
    || fail "give the file with each finding and its disposition, or \"No findings\""
  measure --index || fail "cannot measure the index against main"
  # Untracked files stay out, as they stay out of the commit; a tracked file must be staged.
  git diff --quiet
  case $? in
    0) ;;
    1) fail "stage every change to tracked files (git add -A, or name the paths), so the receipt matches what was reviewed" ;;
    *) fail "cannot compare the working tree with the index" ;;
  esac
  if [ -z "$key" ]; then
    echo "review-receipt: the change from main is empty; nothing to record."
    return 0
  fi
  mkdir -p "$receipts" || fail "cannot create $receipts"
  find "$receipts" -name '*.md' -mtime +30 -exec rm -f {} + 2>/dev/null
  # Written aside and moved into place, so a check never sees half a receipt.
  local tmp=$receipts/.$key.$$
  {
    echo "# Review receipt"
    echo
    echo "- Date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "- Branch: $(git symbolic-ref --short -q HEAD || echo detached)"
    echo "- Base: $base"
    echo "- Tree: $tree"
    echo "- Reviewers: $reviewers"
    echo
    echo "## Findings"
    echo
    cat "$findings"
  } >"$tmp" && mv -f "$tmp" "$receipts/$key.md" || {
    rm -f "$tmp"
    fail "cannot write $receipts/$key.md"
  }
  echo "$receipts/$key.md"
}

check() {
  [ $# = 1 ] || fail "usage: check <rev> | --index | --tracked | --worktree"
  measure "$1" || fail "cannot measure $1 against main"
  [ -z "$key" ] || [ -f "$receipts/$key.md" ] || exit 1
}

case ${1:-} in
  write) shift; write "$@" ;;
  check) shift; check "$@" ;;
  base) base_of HEAD || fail "cannot find where HEAD left main" ;;
  *) fail "usage: review-receipt.sh [-C <dir>] write --reviewers <a,b> <findings.md> | check <rev> | --index | --tracked | --worktree | base" ;;
esac
