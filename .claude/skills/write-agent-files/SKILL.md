---
name: write-agent-files
description: Write or change an Everlong Deep agent, skill, .claude/rules/ file, hook or AGENTS.md in the agreed structure. Use whenever you add, move or reword an instruction for agents; docs in docs/ or README.md use write-docs.
---

# Write agent files

`docs/REVIEW.md` → "Agent, rule and skill files" owns the structure.

1. Choose the kind of file from that section's table.
2. If a script can check the rule, write a hook or an arch test instead
   of prose.
3. Write an agent with a `description` saying when to use it, a `model`
   (`sonnet` by default, `opus` where the focus needs deeper reasoning,
   `haiku` for a light, mechanical task) and these sections, in this order:
   - `## Input`: what it is given, and the doc sections it reads.
   - `## Rules`: only what AGENTS.md, docs and hooks don't say.
   - `## Work`: its steps.
   - `## Output`: for a code reviewer, `AgentFiles.FindingsFormat` word
     for word (`docs/REVIEW.md` → "Findings format").
4. Name a doc by its heading, as `` `docs/X.md` → "Heading" ``, wherever
   you reference one (`docs/REVIEW.md` → "Agent, rule and skill files").
5. Ask `docs/CODE_DESIGN_PRINCIPLES.md` §9's four questions of every
   text you touched.
6. Run the `review-change` skill.
