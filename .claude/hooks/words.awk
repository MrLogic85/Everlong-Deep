# Splits a shell command line into simple commands, one per output line, with
# its words separated by \037 and quotes removed. Quoted text, comments,
# redirections and heredoc bodies yield no commands, so `echo "git stash"`
# reads as echo plus one word. Command and process substitutions start a
# command of their own. With -v marks=1, for reviewer-bash.sh, an output
# redirection's target is kept as a word "\036<operator>\036<target>", and
# each substitution, quoted or not, stays part of its word and is printed as a
# line "\035<its text>", with newlines as \034, to be read in turn; a line
# "\033" marks one that never closes. A best-effort reader for the guard
# hooks, not a shell.
{ src = src (NR > 1 ? "\n" : "") $0 }

END {
  n = length(src); i = 1; word = ""; inword = 0; line = ""; nwords = 0
  redirect = 0; heredoc = 0; npending = 0
  while (i <= n) {
    c = substr(src, i, 1)
    if (c == "$" && substr(src, i + 1, 1) == "'") {
      # $'...' takes backslash escapes, so \' does not end it.
      i += 2
      while (i <= n && (d = substr(src, i, 1)) != "'") {
        if (d == "\\") { word = word substr(src, i, 2); i += 2; continue }
        word = word d; i++
      }
      inword = 1; quoted = 1; i++; continue
    }
    if (c == "'") {
      j = index(substr(src, i + 1), "'")
      if (j == 0) j = n - i + 1
      word = word substr(src, i + 1, j - 1); inword = 1; quoted = 1; i += j + 1; continue
    }
    if (c == "\"") {
      i++; qs = i
      while (i <= n && (d = substr(src, i, 1)) != "\"") {
        if (d == "\\" && i < n && index("\"\\$`\n", substr(src, i + 1, 1))) { i++; d = substr(src, i, 1) }
        # A substitution's own quotes do not end the string.
        else if ((d == "$" && substr(src, i + 1, 1) == "(") || d == "`") {
          e = d == "`" ? closetick(src, i + 1) : closeparen(src, i + 2, 1)
          # One that never closes is read as plain text, and scansubs marks it.
          if (e) { word = word substr(src, i, e - i + 1); i = e + 1; continue }
        }
        word = word d; i++
      }
      if (marks) scansubs(substr(src, qs, i - qs))
      inword = 1; quoted = 1; i++; continue
    }
    if (c == "\\") {
      if (i < n && substr(src, i + 1, 1) != "\n") { word = word substr(src, i + 1, 1); inword = 1; quoted = 1 }
      i += 2; continue
    }
    if (c == " " || c == "\t") { endword(); i++; continue }
    if (c == "#" && !inword) {
      while (i <= n && substr(src, i, 1) != "\n") i++
      continue
    }
    if (c == "<" || c == ">" || (c == "&" && substr(src, i + 1, 1) == ">")) {
      if (inword && word ~ /^[0-9]+$/) { word = ""; inword = 0 } else endword()
      op = c; i++
      while (i <= n && length(op) < 3) {
        d = substr(src, i, 1)
        if (!index("<>&|", d) && !(d == "-" && (op ~ /&$/ || op == "<<"))) break
        op = op d; i++
      }
      if (substr(src, i, 1) == "(" && (c == "<" || c == ">") && length(op) == 1) { i++; endcommand(); continue }
      # Closing a descriptor, as in >&-, takes no target.
      if (op ~ /&-$/) continue
      redirect = 1; heredoc = (op == "<<" || op == "<<-"); rop = op
      strip = (op == "<<-")
      continue
    }
    # Arithmetic, $((...)) or a ((...)) command, stays one word, so a << in it starts no
    # heredoc, and for marks a substitution inside it is read; $((cmd) ...) and ((cmd) ...)
    # start with a subshell instead.
    if (substr(src, i, 3) == "$((" || (!inword && substr(src, i, 2) == "((")) {
      k = c == "$" ? i + 3 : i + 2
      e = closeparen(src, k, 2)
      if (e && cp1 == e - 1) {
        if (marks) scansubs(substr(src, k, e - k - 1))
        word = word substr(src, i, e - i + 1); inword = 1; i = e + 1; continue
      }
    }
    # For marks, a substitution stays part of its word, so a redirect into it is seen, and its
    # text is printed to be read in turn.
    if (marks && ((c == "$" && substr(src, i + 1, 1) == "(") || c == "`")) {
      e = c == "`" ? closetick(src, i + 1) : closeparen(src, i + 2, 1)
      if (!e) { print "\033"; i = n + 1; continue }
      emitsub(c == "`" ? untick(substr(src, i + 1, e - i - 1)) : substr(src, i + 2, e - i - 2))
      word = word substr(src, i, e - i + 1); inword = 1; i = e + 1; continue
    }
    if (c == "$" && substr(src, i + 1, 1) == "(") { endcommand(); i += 2; continue }
    if (c == "\n") {
      endcommand(); i++
      for (p = 1; p <= npending; p++) i = skipbody(i, pending[p], pendingstrip[p], pendingquoted[p])
      npending = 0; continue
    }
    if (index(";&|()`", c)) { endcommand(); i++; continue }
    word = word c; inword = 1; i++
  }
  endcommand()
}

function endword() {
  if (!inword) return
  if (redirect) {
    if (heredoc) { npending++; pending[npending] = word; pendingstrip[npending] = strip; pendingquoted[npending] = quoted }
    else if (marks && rop ~ />/) { line = (nwords ? line "\037" : "") "\036" rop "\036" word; nwords++ }
    redirect = 0; heredoc = 0
  } else {
    gsub(/\n/, " ", word)
    line = (nwords ? line "\037" : "") word; nwords++
  }
  word = ""; inword = 0; quoted = 0
}

function endcommand() {
  endword()
  if (nwords) print line
  line = ""; nwords = 0; redirect = 0; heredoc = 0
}

# A heredoc body expands substitutions unless its delimiter was quoted.
function skipbody(at, delim, tabs, literal,    stop, text, body) {
  body = ""
  while (at <= n) {
    stop = index(substr(src, at), "\n")
    text = stop ? substr(src, at, stop - 1) : substr(src, at)
    at = stop ? at + stop : n + 1
    if (tabs) sub(/^\t+/, "", text)
    if (text == delim) break
    body = body text "\n"
  }
  if (marks && !literal) scansubs(body)
  return at
}

# Prints each $(...) and `...` in text, the inside of double quotes or a heredoc body, as a
# line of its own; quotes there are literal, so only backslashes escape.
function scansubs(text,    k, m, ch, e) {
  m = length(text); k = 1
  while (k <= m) {
    ch = substr(text, k, 1)
    if (ch == "\\") { k += 2; continue }
    if (substr(text, k, 3) == "$((") {
      e = closeparen(text, k + 3, 2)
      if (e && cp1 == e - 1) { scansubs(substr(text, k + 3, e - k - 4)); k = e + 1; continue }
    }
    if (ch == "$" && substr(text, k + 1, 1) == "(") {
      e = closeparen(text, k + 2, 1)
      if (!e) { print "\033"; return }
      emitsub(substr(text, k + 2, e - k - 2)); k = e + 1; continue
    }
    if (ch == "`") {
      e = closetick(text, k + 1)
      if (!e) { print "\033"; return }
      emitsub(untick(substr(text, k + 1, e - k - 1))); k = e + 1; continue
    }
    k++
  }
}

# The index of the ) that closes a substitution whose text starts at k, at the given depth of
# parentheses, reading quotes, escapes, comments and heredoc bodies inside it as the shell
# does; 0 when it never closes. Sets cp1 to where the depth first fell to 1, so $((...)) is
# arithmetic when cp1 is just before the end.
function closeparen(t, k, depth,    m, ch, e, prev, nd, delim, tabs, q, text, stop, h, keep) {
  m = length(t); prev = " "; nd = 0; cp1 = 0
  while (k <= m) {
    ch = substr(t, k, 1)
    if (ch == "\\") k++
    else if (substr(t, k, 3) == "$((") {
      # Arithmetic, where << shifts; otherwise the ( is counted as usual.
      keep = cp1; e = closeparen(t, k + 3, 2)
      if (e && cp1 == e - 1) k = e
      cp1 = keep
    }
    else if (ch == "$" && substr(t, k + 1, 1) == "'") {
      for (k += 2; k <= m && substr(t, k, 1) != "'"; k++) if (substr(t, k, 1) == "\\") k++
      if (k > m) return 0
    }
    else if (ch == "'") { e = index(substr(t, k + 1), "'"); if (!e) return 0; k += e }
    else if (ch == "\"") { k = closequote(t, k + 1); if (!k) return 0 }
    else if (ch == "`") { k = closetick(t, k + 1); if (!k) return 0 }
    else if (ch == "#" && index(" \t\n;&|(", prev)) {
      while (k < m && substr(t, k + 1, 1) != "\n") k++
    }
    else if (substr(t, k, 3) == "<<<") k += 2
    else if (substr(t, k, 2) == "<<") {
      # A heredoc: its body starts after the line ends and may hold anything.
      k += 2; nd++; tabs[nd] = 0; delim[nd] = ""
      if (substr(t, k, 1) == "-") { tabs[nd] = 1; k++ }
      while (substr(t, k, 1) == " " || substr(t, k, 1) == "\t") k++
      while (k <= m && !index(" \t\n;&|<>()", ch = substr(t, k, 1))) {
        if (ch == "'" || ch == "\"") {
          q = index(substr(t, k + 1), ch)
          if (!q) return 0
          delim[nd] = delim[nd] substr(t, k + 1, q - 1); k += q + 1
        } else {
          if (ch == "\\") k++
          delim[nd] = delim[nd] substr(t, k, 1); k++
        }
      }
      prev = " "; continue
    }
    else if (ch == "\n" && nd) {
      k++
      for (h = 1; h <= nd; h++) {
        while (k <= m) {
          stop = index(substr(t, k), "\n")
          text = stop ? substr(t, k, stop - 1) : substr(t, k)
          k = stop ? k + stop : m + 1
          if (tabs[h]) sub(/^\t+/, "", text)
          if (text == delim[h]) break
        }
      }
      nd = 0; prev = "\n"; continue
    }
    else if (ch == "(") depth++
    else if (ch == ")") {
      if (--depth == 0) return k
      if (depth == 1 && !cp1) cp1 = k
    }
    prev = ch
    k++
  }
  return 0
}

# The index of the " that ends a double-quoted string whose text starts at k, or 0.
function closequote(t, k,    m, ch, keep) {
  m = length(t)
  while (k <= m) {
    ch = substr(t, k, 1)
    if (ch == "\"") return k
    if (ch == "\\") k++
    else if (ch == "`") { k = closetick(t, k + 1); if (!k) return 0 }
    else if (ch == "$" && substr(t, k + 1, 1) == "(") {
      keep = cp1; k = closeparen(t, k + 2, 1); cp1 = keep
      if (!k) return 0
    }
    k++
  }
  return 0
}

# The index of the ` that ends a backtick substitution whose text starts at k, or 0.
function closetick(t, k,    m, ch) {
  m = length(t)
  while (k <= m) {
    ch = substr(t, k, 1)
    if (ch == "`") return k
    if (ch == "\\") k++
    k++
  }
  return 0
}

# Inside backticks, a backslash before `, \ or $ only escapes it.
function untick(s,    out, k, ch) {
  out = ""
  for (k = 1; k <= length(s); k++) {
    ch = substr(s, k, 1)
    if (ch == "\\" && index("`\\$", substr(s, k + 1, 1))) { k++; ch = substr(s, k, 1) }
    out = out ch
  }
  return out
}

function emitsub(text) {
  gsub(/\n/, "\034", text)
  print "\035" text
}
