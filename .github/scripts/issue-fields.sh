#!/usr/bin/env bash
# Read or set the Everlong Deep project fields (Status, Priority, Size) on an issue.
# See docs/ISSUES.md → "Setting project fields".
#
#   issue-fields.sh 286                         print the fields
#   issue-fields.sh 286 --status Ready          set one or more fields
#   issue-fields.sh 286 --priority Major --size 3   (also "2" or "2 Major")
#   issue-fields.sh 286 --size none             clear a field
#
# Adds the issue to the project if it is not there yet. Uses the caller's gh login;
# the token needs the `project` scope (gh auth refresh -s project).
set -euo pipefail

OWNER=MrLogic85
REPO=Everlong-Deep
PROJECT_NUMBER=3

usage() { sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }
[[ $# -ge 1 && $1 =~ ^[0-9]+$ ]] || usage
issue=$1; shift
# Bash 3.2 (macOS default) has no associative arrays: keep "Field=value" pairs in a plain array.
want=()
while [[ $# -gt 0 ]]; do
  case $1 in
    --status) want+=("Status=${2:?}"); shift 2 ;;
    --priority) want+=("Priority=${2:?}"); shift 2 ;;
    --size) want+=("Size=${2:?}"); shift 2 ;;
    *) usage ;;
  esac
done

project=$(gh api graphql -f query='
query($owner:String!,$number:Int!){ user(login:$owner){ projectV2(number:$number){ id
  fields(first:50){ nodes{ ... on ProjectV2FieldCommon{ id name }
    ... on ProjectV2SingleSelectField{ options{ id name } } } } } } }' \
  -f owner=$OWNER -F number=$PROJECT_NUMBER --jq '.data.user.projectV2')
project_id=$(jq -r .id <<<"$project")

lookup=$(gh api graphql -f query='
query($owner:String!,$repo:String!,$n:Int!){ repository(owner:$owner,name:$repo){ issue(number:$n){ id
  projectItems(first:20){ nodes{ id project{ id }
    fieldValues(first:20){ nodes{
      ... on ProjectV2ItemFieldSingleSelectValue{ name field{ ... on ProjectV2FieldCommon{ name } } }
      ... on ProjectV2ItemFieldNumberValue{ number field{ ... on ProjectV2FieldCommon{ name } } } } } } } } } }' \
  -f owner=$OWNER -f repo=$REPO -F n="$issue" --jq '.data.repository.issue')
item_id=$(jq -r --arg p "$project_id" '.projectItems.nodes[]|select(.project.id==$p)|.id' <<<"$lookup")

if [[ ${#want[@]} -eq 0 ]]; then
  if [[ -z $item_id ]]; then echo "#$issue is not in the project"; exit 0; fi
  jq -r --arg p "$project_id" '.projectItems.nodes[]|select(.project.id==$p)|.fieldValues.nodes[]
    |select(.field.name=="Status" or .field.name=="Priority" or .field.name=="Size")
    |"\(.field.name): \(.name // .number)"' <<<"$lookup"
  exit 0
fi

# Validate every value before writing anything, so a typo never leaves a half-updated issue.
plan=()
for pair in "${want[@]}"; do
  field=${pair%%=*}
  value=${pair#*=}
  field_id=$(jq -r --arg f "$field" '.fields.nodes[]|select(.name==$f)|.id' <<<"$project")
  [[ -n $field_id ]] || { echo "Project has no field '$field'" >&2; exit 1; }
  if [[ $value == none ]]; then
    plan+=("clear|$field|$field_id|")
  elif [[ $field == Size ]]; then
    [[ $value =~ ^(1|2|3|5|8)$ ]] || { echo "Size must be 1, 2, 3, 5 or 8" >&2; exit 2; }
    plan+=("number|$field|$field_id|$value")
  else
    # Match the full name, or for numbered options ("2 Major") just the number or just the name.
    matches=$(jq -r --arg f "$field" --arg v "$value" '[.fields.nodes[]|select(.name==$f)|.options[]
      |(.name|ascii_downcase) as $n|($v|ascii_downcase) as $w|($n|split(" ")) as $t
      |select($n==$w or ($t[0]|test("^[0-9]+$")) and ($t[0]==$w or ($t[1:]|join(" "))==$w))]
      |map("\(.id)|\(.name)")|join("\n")' <<<"$project")
    options=$(jq -r --arg f "$field" '[.fields.nodes[]|select(.name==$f)|.options[].name]|join(", ")' <<<"$project")
    [[ -n $matches ]] || { echo "Unknown $field '$value'. Options: $options" >&2; exit 2; }
    [[ $matches != *$'\n'* ]] || { echo "Ambiguous $field '$value'. Options: $options" >&2; exit 2; }
    option_id=${matches%%|*}
    plan+=("option|$field|$field_id|$option_id")
  fi
done

if [[ -z $item_id ]]; then
  item_id=$(gh api graphql -f query='mutation($p:ID!,$c:ID!){ addProjectV2ItemById(input:{projectId:$p,contentId:$c}){ item{ id } } }' \
    -f p="$project_id" -f c="$(jq -r .id <<<"$lookup")" --jq '.data.addProjectV2ItemById.item.id')
fi

for step in "${plan[@]}"; do
  IFS='|' read -r kind field field_id value <<<"$step"
  case $kind in
    clear)
      gh api graphql -f query='mutation($p:ID!,$i:ID!,$f:ID!){ clearProjectV2ItemFieldValue(input:{projectId:$p,itemId:$i,fieldId:$f}){ clientMutationId } }' \
        -f p="$project_id" -f i="$item_id" -f f="$field_id" >/dev/null
      value=none ;;
    number)
      gh api graphql -f query='mutation($p:ID!,$i:ID!,$f:ID!,$v:Float!){ updateProjectV2ItemFieldValue(input:{projectId:$p,itemId:$i,fieldId:$f,value:{number:$v}}){ clientMutationId } }' \
        -f p="$project_id" -f i="$item_id" -f f="$field_id" -F v="$value" >/dev/null ;;
    option)
      gh api graphql -f query='mutation($p:ID!,$i:ID!,$f:ID!,$o:String!){ updateProjectV2ItemFieldValue(input:{projectId:$p,itemId:$i,fieldId:$f,value:{singleSelectOptionId:$o}}){ clientMutationId } }' \
        -f p="$project_id" -f i="$item_id" -f f="$field_id" -f o="$value" >/dev/null
      value=$(jq -r --arg f "$field" --arg o "$value" '.fields.nodes[]|select(.name==$f)|.options[]|select(.id==$o)|.name' <<<"$project") ;;
  esac
  echo "#$issue $field → $value"
done
