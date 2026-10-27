# Everlong Deep

> You're one captain. The world only knows what it's been told.

**Everlong Deep** is a single-player open-world pirate sandbox. You are a
captain with a ship and a crew: a pirate, a merchant, a bounty hunter, or
all of them. The heart of the game is the people in the world, what they
believe and want, and how stories about you travel between them, until
one day you're attacked in a bar far away over a price on your head that
may only ever have been a rumor.

The design is in [`docs/00 Overview.md`](docs/00%20Overview.md).

## Status

**Early development.** The world simulation is being built first, as a
headless C# library with no graphics
([`docs/01 Simulation Architecture.md`](docs/01%20Simulation%20Architecture.md)).

## Building

Install the tools in "Tools" below, then:

```bash
dotnet build EverlongDeep.slnx
dotnet test EverlongDeep.slnx
```

More in `docs/TEST_STRATEGY.md` → "Running the suite".

### Tools

Every tool a script, doc or agent relies on; machine-specific state goes
in `LOCAL_CONFIG.md` (`AGENTS.md` → "Machine-local setup").

| Tool | Required or recommended for | macOS install |
| --- | --- | --- |
| .NET 10 SDK | Required: building and testing | `brew install --cask dotnet-sdk` |
| `git` 2.31+ | Required: the hooks and `.github/scripts/review-receipt.sh` | Xcode Command Line Tools, or `brew install git` |
| `gh` 2.94+ | Required: scripts in `.github/scripts/` and the skills | `brew install gh` |
| `jq` | Required: the same scripts, the hooks, which are off without it, and the hook specs | `brew install jq` |
| `rg` (ripgrep) | Recommended: fast search that skips build output | `brew install ripgrep` |
| `csharp-ls` | Recommended: the agents' `LSP` tool; without it they use `git grep` | Pinned in `dotnet-tools.json`; a new agent session runs `dotnet tool restore`. Claude Code loads it only in a trusted folder |

The `dotnet-sdk` cask installs the newest SDK; the build needs 10.x. The
scripts' `gh` login needs the `project` scope (`docs/ISSUES.md` →
"Setting project fields").

## Documentation

| Topic | Document | Audience |
| --- | --- | --- |
| The game in brief, core principles and loop | [`docs/00 Overview.md`](docs/00%20Overview.md) | Both |
| Simulation architecture and build order | [`docs/01 Simulation Architecture.md`](docs/01%20Simulation%20Architecture.md) | Both |
| Rumor system | [`docs/02 Rumor System.md`](docs/02%20Rumor%20System.md) | Both |
| Crew and captaincy | [`docs/03 Crew and Captaincy.md`](docs/03%20Crew%20and%20Captaincy.md) | Both |
| Ship | [`docs/04 Ship.md`](docs/04%20Ship.md) | Both |
| Ports | [`docs/05 Ports.md`](docs/05%20Ports.md) | Both |
| Trading | [`docs/06 Trading.md`](docs/06%20Trading.md) | Both |
| Factions | [`docs/07 Factions.md`](docs/07%20Factions.md) | Both |
| Social model: people, relations, beliefs and wants | [`docs/08 Social Model.md`](docs/08%20Social%20Model.md) | Both |
| Agent and contributor entry point | [`AGENTS.md`](AGENTS.md) | Agents |
| Layers, dependencies, solution layout | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Both |
| Code and doc rules | [`docs/CODE_DESIGN_PRINCIPLES.md`](docs/CODE_DESIGN_PRINCIPLES.md) | Agents |
| Automated tests | [`docs/TEST_STRATEGY.md`](docs/TEST_STRATEGY.md) | Agents |
| Manual tests | [`docs/MANUAL_TESTING.md`](docs/MANUAL_TESTING.md) | Agents |
| Issues, labels and project fields | [`docs/ISSUES.md`](docs/ISSUES.md) | Agents |
| Tools to install | `README.md` → "Tools" | Humans |
| How a change lands, review agents, hooks | [`docs/REVIEW.md`](docs/REVIEW.md) | Agents |
| What each review agent checks | [`.claude/agents/`](.claude/agents/) | Agents |
| Procedures (skills) | [`.claude/skills/`](.claude/skills/) | Agents |

## Contributing

A solo personal project. Work is tracked in GitHub Issues
(`docs/ISSUES.md`) on the
[Everlong Deep project](https://github.com/users/MrLogic85/projects/3), and
`AGENTS.md` is the working process.

## License

GPLv3; see `LICENSE`.
