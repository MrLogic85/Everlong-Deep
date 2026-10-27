#!/usr/bin/env bash
# PreToolUse on Bash in the review agents. See docs/REVIEW.md → "Hooks".
. "${0%/*}/common.sh"

refuse() {
  deny "${1%"${1##*[! ]}"} is not allowed for a review agent, which only reads (docs/REVIEW.md → \"Hooks\"): use a reading command, or report what should be run instead."
}

# Shell variables that make an allowed command run something else.
check_assignment() {
  local name=${1%%=*} value=${1#*=}
  case $name in
    *PAGER) [ -z "$value" ] || [ "$value" = cat ] || refuse "Setting $name" ;;
    GIT_* | EDITOR | VISUAL | *BROWSER | *PATH | LD_* | DYLD_* | BASH_ENV | ENV | SHELLOPTS | BASHOPTS \
      | PROMPT_COMMAND | PS4 | IFS | DOTNET_* | MSBUILD* | EVERLONG_DEEP_*)
      refuse "Setting $name" ;;
  esac
}

check_redirect() {
  local mark=${1#$'\036'} op target
  op=${mark%%$'\036'*}
  target=${mark#*$'\036'}
  case $target in /dev/null | /dev/stdout | /dev/stderr) return ;; esac
  # A duplicated descriptor, as in 2>&1, writes no file.
  case $op in *'>&') case $target in [0-9] | [0-9][0-9]) return ;; esac ;; esac
  refuse "Writing to $target"
}

check_git() {
  local sub a list=0 value=0 mode=0
  while [ $# -gt 0 ]; do
    case $1 in
      -C) shift ;;
      --no-pager | -P | --no-optional-locks | --literal-pathspecs | --glob-pathspecs | --noglob-pathspecs | --icase-pathspecs | --no-replace-objects) ;;
      --version | -v) return ;;
      -*) refuse "git $1" ;;
      *) break ;;
    esac
    shift
  done
  sub=${1:-}
  shift
  for a in "$@"; do
    case $a in
      --output | --output=*) refuse "git $sub $a" ;;
    esac
  done
  case $sub in
    '' | diff | log | show | blame | annotate | status | ls-files | ls-tree | rev-parse | rev-list | merge-base | cat-file \
      | describe | shortlog | for-each-ref | show-ref | name-rev | range-diff | diff-tree | diff-index | diff-files \
      | check-ignore | check-attr | count-objects | var | whatchanged | version | help | patch-id | cherry \
      | ls-remote) ;;
    reflog) case ${1:-} in expire | delete) refuse "git reflog $1" ;; esac ;;
    grep)
      for a in "$@"; do
        if [ $value = 1 ]; then
          value=0
          continue
        fi
        [ "$a" = -- ] && break
        case $a in
          -e | -f) value=1 ;;
          # git takes an abbreviated long option, so --op= is --open-files-in-pager=.
          -O* | -[!-]*O* | --op*) refuse "git grep $a" ;;
        esac
      done
      ;;
    branch | tag)
      for a in "$@"; do
        if [ $value = 1 ]; then
          value=0
          case $a in -*) ;; *) continue ;; esac
        fi
        case $a in
          -l | --list) list=1 ;;
          -v | --verify) [ "$sub" = tag ] && list=1 ;;
          --contains | --no-contains | --merged | --no-merged | --points-at | --sort | --format) value=1 ;;
          -a | --all | -r | --remotes | -vv | --verbose | --show-current | --contains=* | --no-contains=* \
            | --merged=* | --no-merged=* | --points-at=* | --sort=* | --format=* | --color* | --no-color \
            | --column* | --no-column | -i | --ignore-case | --abbrev=* | --no-abbrev | -q | --quiet | -n*) ;;
          -*) refuse "git $sub $a" ;;
          *) [ $list = 1 ] || refuse "git $sub $a" ;;
        esac
      done
      ;;
    config)
      # The get or list subcommand counts only as the first word that is not an option.
      for a in "$@"; do
        case $a in
          --get | --get-all | --get-regexp | --get-urlmatch | --get-color | --get-colorbool | -l | --list) mode=1 ;;
          --show-origin | --show-scope | --name-only | -z | --null | --global | --system | --local | --worktree \
            | --includes | --no-includes | --type=* | --bool | --int | --path | --default=*) ;;
          -*) refuse "git config $a" ;;
          *)
            if [ $list = 0 ]; then
              list=1
              case $a in get | list) mode=1 ;; esac
            fi
            ;;
        esac
      done
      [ $mode = 1 ] || refuse "git config without --get or --list"
      ;;
    remote)
      while [ "${1:-}" = -v ] || [ "${1:-}" = --verbose ]; do
        shift
      done
      case ${1:-} in '' | get-url | show) ;; *) refuse "git remote $1" ;; esac
      ;;
    stash) case ${1:-} in list | show) ;; *) refuse "git stash ${1:-}" ;; esac ;;
    worktree) [ "${1:-}" = list ] || refuse "git worktree ${1:-}" ;;
    *) refuse "git $sub" ;;
  esac
}

check_gh_api() {
  local path='' method='' fields=0 a
  while [ $# -gt 0 ]; do
    a=$1
    case $a in
      -X | --method) method=${2:-}; shift ;;
      --method=*) method=${a#--method=} ;;
      -X?*) method=${a#-X} ;;
      -f | -F | --field | --raw-field) fields=1; a=${2:-}; shift ;;
      --field=* | --raw-field=* | -f?* | -F?*) fields=1 ;;
      --input | --input=*) refuse "gh api --input" ;;
      -H | --header | -q | --jq | -t | --template | --hostname | --cache | -p | --preview) shift ;;
      -*) ;;
      *) [ -n "$path" ] || path=$a ;;
    esac
    case $a in *[Mm][Uu][Tt][Aa][Tt][Ii][Oo][Nn]*) refuse "A GraphQL mutation" ;; esac
    shift
  done
  [ "$path" = graphql ] && return
  case $method in '' | GET | get) ;; *) refuse "gh api -X $method" ;; esac
  [ $fields = 1 ] && [ -z "$method" ] && refuse "gh api with -f or -F but no -X GET"
  return 0
}

check_gh() {
  while [ $# -gt 0 ]; do
    case $1 in
      -R | --repo)
        [ $# -ge 2 ] || refuse "gh $1"
        shift 2
        ;;
      --repo=*) shift ;;
      *) break ;;
    esac
  done
  case "${1:-} ${2:-}" in
    'issue view' | 'issue list' | 'issue status' | 'pr view' | 'pr diff' | 'pr list' | 'pr checks' | 'pr status' \
      | 'run view' | 'run list' | 'workflow list' | 'workflow view' | 'repo view' | 'release view' | 'release list' \
      | 'label list' | 'project view' | 'project list' | 'project item-list' | 'project field-list' \
      | 'search '* | '--version '*) ;;
    'auth status')
      for a in "$@"; do
        case $a in -t | --show-token) refuse "gh auth status $a" ;; esac
      done
      ;;
    'api '*) shift; check_gh_api "$@" ;;
    *) refuse "gh ${1:-} ${2:-}" ;;
  esac
}

check_find() {
  local a
  for a in "$@"; do
    case $a in -exec | -execdir | -ok | -okdir | -delete | -fprint | -fprint0 | -fprintf | -fls) refuse "find $a" ;; esac
  done
}

# Only `sed -n` with scripts that print or quit at line numbers, so no script can write.
check_sed() {
  local quiet=0 script='' a
  local re='^(([0-9]+|\$)(,([0-9]+|\$))?)?[pq](;(([0-9]+|\$)(,([0-9]+|\$))?)?[pq])*;?$'
  while [ $# -gt 0 ]; do
    a=$1
    case $a in
      -n | --quiet | --silent) quiet=1 ;;
      -E | -r | --regexp-extended) ;;
      -nE | -En | -nr | -rn) quiet=1 ;;
      -e) [[ ${2:-} =~ $re ]] || refuse "sed -e '${2:-}'"; script=1; shift ;;
      -*) refuse "sed $a" ;;
      *)
        if [ -z "$script" ]; then
          [[ $a =~ $re ]] || refuse "sed '$a'"
          script=1
        fi
        ;;
    esac
    shift
  done
  [ $quiet = 1 ] || refuse "sed without -n"
}

check_command() {
  local -a w=()
  local a name sub count=0 value=0
  for a in "$@"; do
    case $a in
      $'\036'*) check_redirect "$a" ;;
      *) w+=("$a") ;;
    esac
  done
  while [ ${#w[@]} -gt 0 ]; do
    case ${w[0]} in
      [A-Za-z_]*=*)
        [[ ${w[0]} =~ ^[A-Za-z_][A-Za-z0-9_]*= ]] || break
        check_assignment "${w[0]}"
        ;;
      if | then | else | elif | fi | do | done | while | until | '!' | '{' | '}' | time) ;;
      # Arithmetic, whose substitutions words.awk prints to be read in turn.
      '(('*) ;;
      *) break ;;
    esac
    w=("${w[@]:1}")
  done
  [ ${#w[@]} -gt 0 ] || return
  name=${w[0]##*/}
  # A path runs the named program only from a system folder.
  case ${w[0]} in
    */*)
      case ${w[0]%/*} in
        /bin | /usr/bin | /usr/local/bin | /opt/homebrew/bin | /usr/local/share/dotnet) ;;
        *) [ "$name" = review-receipt.sh ] || refuse "${w[0]}" ;;
      esac
      ;;
  esac
  set -- "${w[@]:1}"
  case $name in
    git) check_git "$@" ;;
    gh) check_gh "$@" ;;
    rg)
      for a in "$@"; do
        case $a in --pre | --pre=* | --hostname-bin*) refuse "rg ${a%%=*}" ;; esac
      done
      ;;
    find) check_find "$@" ;;
    sed) check_sed "$@" ;;
    sort)
      for a in "$@"; do
        case $a in --o* | --compress* | -o* | -[!-]*o*) refuse "sort $a" ;; esac
      done
      ;;
    uniq)
      for a in "$@"; do
        if [ $value = 1 ]; then
          value=0
          continue
        fi
        case $a in
          -f | -s | --skip-fields | --skip-chars | -w | --check-chars) value=1 ;;
          -) count=$((count + 1)) ;;
          -*) ;;
          *) count=$((count + 1)) ;;
        esac
      done
      [ $count -le 1 ] || refuse "uniq with an output file"
      ;;
    file)
      for a in "$@"; do
        case $a in -C | -[!-]*C* | --c*) refuse "file $a" ;; esac
      done
      ;;
    export | set)
      # Without arguments, or with export -p, these print the environment, tokens included.
      [ $# -gt 0 ] || refuse "$name without arguments"
      for a in "$@"; do
        case $name$a in
          export-*) refuse "export $a" ;;
          *=*) check_assignment "$a" ;;
        esac
      done
      ;;
    command) case ${1:-} in -v | -V) ;; *) refuse "command ${1:-}" ;; esac ;;
    dotnet)
      case ${1:-} in
        build | test)
          # Only options that select, filter or quiet the run; others can run a command, load
          # code or write elsewhere.
          sub=$1
          shift
          for a in "$@"; do
            if [ $value = 1 ]; then
              value=0
              continue
            fi
            if [ $value = 2 ]; then
              value=0
              case $a in console | console\;*) continue ;; *) refuse "dotnet $sub --logger $a" ;; esac
            fi
            case $a in
              --filter | -c | --configuration | -f | --framework | -v | --verbosity) value=1 ;;
              # Only the console logger, which writes no file.
              --logger | -l) value=2 ;;
              --no-build | --no-restore | --nologo | -warnaserror | --list-tests | --no-incremental \
                | --no-dependencies | -m | -m:* | --configuration=* | --framework=* | --verbosity=* | --filter=*) ;;
              -* | /*) refuse "dotnet $sub ${a%%[:=]*}" ;;
            esac
          done
          ;;
        --version | --info | --list-sdks | --list-runtimes) ;;
        format)
          shift
          for a in "$@"; do
            if [ $value = 1 ]; then
              value=0
              continue
            fi
            case $a in
              --verify-no-changes) count=1 ;;
              --include | --exclude | --severity | --diagnostics | -v | --verbosity) value=1 ;;
              --no-restore | whitespace | style | analyzers | --include=* | --exclude=* | --severity=* \
                | --diagnostics=* | --verbosity=*) ;;
              -* | /*) refuse "dotnet format ${a%%[:=]*}" ;;
            esac
          done
          [ $count = 1 ] || refuse "dotnet format without --verify-no-changes"
          ;;
        *) refuse "dotnet ${1:-}" ;;
      esac
      ;;
    for | grep | egrep | fgrep | ls | cat | head | tail | wc | cut | tr | jq | diff | cmp | stat | du | pwd | echo \
      | basename | dirname | realpath | readlink | date | cd | true | false | : | nl \
      | comm | column | fold | rev | od | hexdump | shasum | sha256sum | md5 | md5sum | which | type \
      | sleep | exit | return | shellcheck) ;;
    # These evaluate an array subscript, and so a substitution in it, when given a variable name.
    printf | test | '[' | read)
      for a in "$@"; do
        case $name$a in printf-v* | test-v | \[-v | read*\[*) refuse "$name $a" ;; esac
      done
      ;;
    bash | sh)
      # Only a syntax check: -n reads the script and runs nothing.
      [ "${1:-}" = -n ] || refuse "$name"
      ;;
    review-receipt.sh)
      case ${w[0]} in
        .github/scripts/review-receipt.sh | ./.github/scripts/review-receipt.sh | "$root/.github/scripts/review-receipt.sh") ;;
        *) refuse "${w[0]}" ;;
      esac
      case ${1:-} in check | base) ;; *) refuse "review-receipt.sh ${1:-}" ;; esac
      ;;
    *) refuse "$name" ;;
  esac
}

# Reads each simple command, and in turn each substitution words.awk found inside quotes or a
# heredoc body.
check_line() {
  local depth=$2 inner lines
  local -a w
  lines=$(printf '%s' "$1" | awk -v marks=1 -f "$hooks_dir/words.awk") || refuse "A command the hook cannot read"
  while IFS=$'\037' read -r -a w; do
    case ${w[0]:-} in
      $'\033') refuse "A substitution that never closes" ;;
      $'\035'*)
        [ "$depth" -lt 3 ] || refuse "A substitution nested this deep"
        inner=${w[0]#$'\035'}
        check_line "${inner//$'\034'/$'\n'}" $((depth + 1))
        ;;
      *) check_command "${w[@]}" ;;
    esac
  done <<<"$lines"
}

check_line "$(jq -r '.tool_input.command // empty' <<<"$input")" 0
exit 0
