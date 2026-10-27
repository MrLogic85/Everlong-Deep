# Pirate Game: Simulation Architecture

Oct 27, 2026 · Fredrik

## Purpose

The world simulation is built as a C# library with no knowledge of the engine, 2D or 3D. The goal is to get a living world running, with interactions, history and the LLM storyteller, without any graphics at all. If that works, the game is built on top of it.

The library also serves as:

- **The test harness**, running with event injection, logs and written tests.
- **The history generator**, run for years before the player arrives to create the world's starting history.

## What the simulation owns and what the game owns

The world has to live where the player isn't: ships sail, rumors travel and factions plot in ports the player hasn't visited, and during history generation there's no game running at all. So the simulation owns the world, including where people are.

| The simulation owns | The game owns |
| --- | --- |
| Places as abstract locations: ports, taverns, ships | Physical space: layouts, pathfinding, rendering |
| Who is in each place, and how long travel takes between them | Real-time scenes, such as deck fights |
| Where NPCs go and why, from their wants, plans and faction business | Carrying out the player's actions |
| The results of interactions | |

**The player chooses, the simulation decides.** Where the player is, the game owns the actions and the simulation owns their results. Talking to someone, the dialogue options come from the simulation, but the player chooses what to talk about.

A place is a container with properties: who controls it, who is present, what's settled there. That's what makes a tavern in one port different from another's. Places exist from the first step of the build, so a world with one tavern is just a world with a single place, and adding more later means adding data, not changing the architecture.

## Played or simulated

**If the player can witness it, the game plays it. If not, the simulation resolves it, and the player hears about it.**

Everything within the player's perception is a game interaction: the point of interest they're at, the ships they can see from the deck. Everything outside it is simulated. A brawl in another tavern in the same port is resolved by the simulation, and the crew come back with a story about it. A ship over the horizon is a position on a route until it comes into sight.

The boundary is meaningful, not just technical: witnessing is where truth comes from in the rumor system, and what the player didn't see, they only know through what people tell them.

| Handover | What passes |
| --- | --- |
| Entering the player's perception | The simulation hands the game a snapshot: who is there, where, and what they're doing |
| Leaving it | The game reports what happened to the simulation as events, which become beliefs in the witnesses |

Results out of sight need to feel real. The known trap, seen in games like X4, is resolving things differently out of sight than in view, which players learn to exploit.

## Levels of simulation

Fidelity follows narrative importance. Important characters are simulated more heavily than ordinary NPCs.

| Tier | Who | How they're simulated |
| --- | --- | --- |
| MPC (Model Played Character) | A handful of characters important to the overall story of the world | Simulated in depth, with long-term plans, and roleplayed to some degree by the LLM |
| NPC | Everyone else, including most of your crew | Driven by the algorithm |

**NPCs can be promoted to MPCs** through interaction, by the player or by another MPC. Example: if you interact a lot with a crew member and then make them a tavern owner, they become an MPC.

Only the handful of MPCs need the expensive LLM treatment, which keeps history generation affordable. When an NPC is promoted, the LLM continues the character from their traits, relationships and history in the data, so they still feel like the same person.

## Build order

*Two stops: social core working, factions balanced, then travel.*

**Social core, all in one tavern**

1. NPCs: adding them and keeping track of who they are
2. Relations: who knows whom, and what they think of them
3. Rumors: who knows what and how it spreads, distance agnostic for now
4. Wants: shaped by who people are and who they know, and shaping what they say

**5. Stop: get this working before adding factions**

**Factions**

6. Factions: who belongs to which faction, and what they think of the factions
7. Policies: leaders decide how to deal with factions and individuals, swayed by rumors and friends

**8. Stop: get this balanced**

**A world with intent**

9. Plans: people pursue futures, with LLM final touches on the characters that matter
10. Travel: time and place, who goes where and who can talk to whom, messages

Each stop point is a gate: nothing new gets added until what's there works or is balanced. Places exist in the simulation from step 1, even though travel comes last. For people to truly feel alive they need plans for the future, not just reactions, and to keep those from feeling scripted, the LLM adds the final touches to the characters that matter. Relationships can be driven by an algorithm, while their effects on allegiance and wants benefit from the LLM. Travel needs messages to force communication, and messages need a deeper system for long-term agendas, so steps 9 and 10 are closely tied.

### 5. Stop: the tavern sounds alive

The social core works when you can listen to the tavern chatter on a seeded run and it sounds alive. You listen to the simulation's own log, plain template lines of who told whom what, how strongly they believe it, and why, so a dead simulation can't hide behind good prose. The player isn't part of this stop: injected events stand in for them.

"Alive" means:

1. **Caused:** every line traces back to a belief, a want or a relation.
2. **Varied:** different people talk about different things, no topic loops forever, and the tavern never falls silent.
3. **Spreading:** an injected event reaches people who weren't there, colored by who tells it.
4. **Responsive:** an injected event changes the chatter and shifts relations.
5. **Stable:** a long seeded run neither blows up nor dies out.
6. **Reproducible:** the same seed gives the same chatter.

Caused, spreading and reproducible are automated tests. The other three start as manual listening and become tests once we know what good sounds like (`docs/TEST_STRATEGY.md` → "Layers").

## Open questions

- What does "balanced" mean at step 8? Which measurable checks become the tests?
- Balancing done in one tavern may change when travel and distance arrive at step 10. How much rebalancing is acceptable?
- Can MPCs be demoted, so the handful stays a handful? (An optimization detail for later.)
- How are results out of sight kept consistent with results the player witnesses?
