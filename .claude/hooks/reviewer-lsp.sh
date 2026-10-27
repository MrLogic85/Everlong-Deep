#!/usr/bin/env bash
# PreToolUse on LSP in the review agents. See docs/REVIEW.md → "Hooks".
. "${0%/*}/common.sh"

operation=$(jq -r '.tool_input.operation // empty' <<<"$input")
case $operation in
  goToDefinition | findReferences | hover | documentSymbol | workspaceSymbol | goToImplementation \
    | prepareCallHierarchy | incomingCalls | outgoingCalls) ;;
  *) deny "LSP ${operation:-without an operation} is not allowed for a review agent, which only reads (docs/REVIEW.md → \"Hooks\"): use a lookup such as findReferences, or report what should change." ;;
esac
exit 0
