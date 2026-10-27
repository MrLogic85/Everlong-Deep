# Pirate Game: Overview

Oct 27, 2026 · Fredrik

## The game in brief

A single-player PVE open-world pirate sandbox, likely in 2D. You are a captain with a ship and a crew. You can be a pirate, a merchant, a bounty hunter, or all of them, though it's hard to be trusted as a merchant if you're also a pirate.

You spend most of your time on yourself, your crew and your ships. But the core of the game is the living characters in the world: what they believe, what they want, and how stories about you spread between them, until one day you're attacked in a bar far away over a price on your head that may only ever have been a rumor.

**Why single player.** A multiplayer sandbox would fit the vision of everyone being people in a living story, but it works against the core of the game. Time is shared in multiplayer, so voyages can't be skipped and nobody can pause, while the rumor system depends on stories taking days and weeks to travel. Worse, players talking to each other in real time across ports would know instantly what the world only learns much later, bypassing the rumor system entirely. If multiplayer ever returns, the one form that avoids this is friends sharing a single ship as its crew.

## Core principles

Two ideas carry the whole game, and they interlock, because trust is itself a belief:

1. **The world runs on beliefs, not facts.** The truth is never stored. NPCs act on what they believe, and information travels, mutates and arrives late.
2. **You're one person.** Everything beyond your reach runs through people you trust: ship stations, crew, other ships, a shop in a distant port.

The principles that follow from them:

| Principle | Where it shows up |
| --- | --- |
| The rules are yours, the execution is theirs | Ship rules, crew, holdings |
| Decisions are the player's, execution is the character's | Taking a station yourself |
| Every outcome produces stories, good or bad | Crew, rumors, factions |
| Danger is priced in | Trading, piracy |
| Interruptions are feedback | Crew management |
| Distance costs time | Ship layout, combat, information travel |
| Large size is unstable | Factions |
| Show the system through the world, never through a reputation screen | Rumors, crew mood |

## The core loop

*What happens at sea spreads when you reach port.*

1. **In port:** talk, trade, hire crew, upgrade the ship. Set rules for the ship's functions.
2. **Leave port:** the crew you hired is your bet on what the voyage will bring.
3. **At sea:** steer yourself or take any station. The crew runs the ship by your rules.
4. **Arrive at the next port:** what happened on board spreads, and stories about you may be waiting. Then back to step 1.

The player can spend a session on any part of the loop: talking in a tavern, configuring the ship and setting orders, or just sailing. No activity is forced. The crew simulation tells the story within a voyage, and the rumor system tells the story between voyages.

## Approach and scope

- **A personal project.** The game is what Fredrik wants to play himself.
- **2D** to keep it feasible.
- **Milestones, split as they develop.** Each part starts rudimentary, like clicking on a map to sail or auto-resolved fights, and grows as needed. Effort goes to the next thing that adds value.
- **The rumor system first**, as a headless simulation with event injection, logs and written tests. The same simulation, run for years before the player arrives, generates the world's starting history.
- **The simulation decides, an LLM narrates.** Rules and data decide what happens. An LLM can give characters personality at chosen story moments by interpreting their history within the rules.
- **Deep systems are kept in mind, not built early.** Owning shops, full factions and fleets are directions to grow into.

## Design docs

| Doc | Covers |
| --- | --- |
| Simulation architecture | The headless C# library, what the simulation and game own, MPCs, build order |
| Rumor system | How information is born, travels, settles and is shaped by the player |
| Crew and captaincy | The ship as a colony, crew wants, the captain, succession and legacy |
| Ship | Getting a ship, configuration, crewing, sailing and fighting |
| Ports | Points of interest, being found, wants, promises and quests |
| Trading | Danger priced in, prices as information, merchant trust |
| Factions | Focus areas, splits, brakes on domination, the player's holdings |
| Social model | What people, relations, beliefs and wants are, and how stories match and fade |

Still untouched: the world and travel, ship-to-ship combat at sea, bounty hunting, long-term goals, and fleets.
