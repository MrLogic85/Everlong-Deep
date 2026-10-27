# Pirate Game: Crew and Captaincy Design

Oct 26, 2026 · Fredrik

## Core idea

Each ship is a colony. Every crew member is a tracked NPC who knows, and has opinions about, every other member, in the spirit of Dwarf Fortress, RimWorld and Mind Over Magic.

The simulation is about the ship, not survival. Crew don't have individual food and sleep cycles to manage. What's simulated is mentality and capability: what each person is good at, what they like, and how they get along.

The ship is simulated spatially, on board and in port, but with a different focus than RimWorld.

Physical placement matters because distance costs time: where ammunition, gun racks and quarters sit decides how fast the crew can act. See the ship design doc.

Crew sizes are a rough guess: a handful on a small ship, around 50 on a large one.

## Crew composition

Every crew is a bet on what the voyage will bring: stability or capability. The bet is placed in port, when you hire.

| Crew | In calm waters | In a storm or a fight |
| --- | --- | --- |
| Well-behaved citizens | Content as long as sailing is smooth and provisions and pay are right | Fragile |
| Restless fighters | Harder to keep happy and in line | Strong, e.g. firing chain shot to get away from attackers |
| Mixed | The two groups may not get along | Covers more situations |

A long merchant voyage with no combat calls for a different crew than a pirate ship.

Mixing types creates new needs. Fights have to be broken up, and some crew are on board only to keep the others in line. Their value exists only because of the mix you chose.

Ship-level resources such as provisions and voyage pay shape mood, rather than individual needs.

## Interruptions as feedback

If the player constantly has to stop and manage fights and other events, they've built the crew wrong. Interruptions tell you something about who you hired.

The system allows for optimization and automation. Mastery shows up as a quieter ship.

A quiet ship is a valid way to play. Sticking to patrolled routes and hiring escorts is a legitimate style in a sandbox, as long as safety has a price.

The player needs to be able to see why something went wrong, such as which two personalities clashed and over what. Otherwise the feedback feels random.

## Stories beyond conflict

The journey should be a story of its own, and every outcome produces stories, good or bad. An optimized ship has no internal struggle, but it still has individuals with their own beliefs.

- A sailor falls in love, and is devastated when the other leaves the crew or dies.
- After a smooth voyage the crew throws a party in the bar when they reach port.
- At that party, another sailor gets along with the crew and asks to join.
- The same success starts a rumor about your profitable voyages, which brings jealousy and a hunt the next time you leave port.

Success has consequences too. Quiet, personal stories need a moment of presentation, or they're easy to miss.

## Crew wants

The best natural quests come from the crew. Each crew member has their own personality and things they want, big and small. Wants don't have to exist from the start: they're generated from what happens in the story.

What a want is, its fields and how it fades, is in the social model doc.

**Wants have a target.** A want aimed at the captain, such as a bigger ship or going somewhere, affects the relationship with you. A want aimed elsewhere, such as revenge, doesn't punish your relationship while it's unresolved, unless you're the one standing in the way.

**Unresolved wants cost morale; resolved ones give a boost.** Fulfilling wants builds the loyalty that decides whether a crew member lies for you, follows orders under fire, or joins a mutiny.

**Wants spread through talk.** Each person carries a set of things they want to talk about, some more and some less. The more someone wants to talk about something, the more it spreads. If one sailor wants to go ashore and says so, others may start wanting the same thing. Ignore it, and you'll have trouble.

**Listeners react, they don't copy.** If one crew member wants to sink more ships, another might want that person off the crew.

The want system is a full system of its own, in the spirit of how colony sims handle their colonists. Whether talking about a want makes it fade or grow is left for balancing.

## The captain

The player is the captain, a person in the world, not a hand of god. The rumor system depends on this: the stories are about you.

**The rules are yours, the execution is the crew's.** You set standing rules for the ship's functions, such as what to do when attacked. Whether a rule works depends on who carries it out, how skilled they are, and whether they follow your orders.

**The captain can take any station.** You can steer, command the guns, or fight yourself, and the crew handles everything you don't. For each function you can do it yourself, command it, or delegate it. Decisions such as timing and positioning are the player's; execution such as reload speed and accuracy comes from character stats. See the ship design doc.

The core loop:

1. In port: talk to people, buy and sell, upgrade the ship, set rules for its functions.
2. Leave port.
3. At sea: steer the ship yourself, while the crew runs it according to your rules.
4. Arrive at the next port, where what happened on board starts to spread.

## Succession

When the captain dies or the crew mutinies, some event selects a new captain. The player chooses whether to end the game or continue as the new character.

- **Selection:** the crew may choose, or an inheritance rule may decide. Pirate crews historically elected captains under the ship's articles, while merchant captains were appointed, so the succession rule could be one of the rules you set for your ship.
- **Mutiny:** if you continue after a mutiny, you play the person who overthrew you. The new captain starts life as a mutineer, with crew still loyal to the old captain on board.
- **Continuation is not guaranteed.** You can end up penniless, lose your ship, or have yourself and your whole crew die in a bar brawl.

## Legacy

If you inherit the ship, you inherit its legacy, but not all of the good and bad should come with it.

What carries over depends on what each rumor is attached to:

| Rumor attached to | What happens on succession |
| --- | --- |
| The ship | Stays with the ship and its new captain |
| The crew | Stays with whoever remains on board |
| The old captain | Stays with the old captain, not the successor |

The change of captain is itself news that has to travel. Ports that haven't heard still treat you as the old captain, for better or worse. The partial inheritance comes from information spreading unevenly, not from a hand-tuned rule.

If legacy follows the ship, renaming or repainting it becomes a way to shed a reputation, at the risk of being recognized anyway.

## Ties to the rumor system

The crew simulation tells the story within a voyage. The rumor system tells the story between voyages, as what happened on board spreads in port.

- **Crew are witnesses** to everything you do, and they survive it. They may protect you, tell the truth, or lie.
- **The ship has its own rumor network.** Crew know each other, so gossip, grudges and loyalty spread below deck by the same rules as between ports. A mutiny can grow from a story about the captain taking hold among the crew.
- **Hiring carries history.** Every sailor hired in port brings their own past, friends and reputation.
- **Crew go ashore** wherever you dock, and carry stories with them.

## Open questions

- What do standing rules look like, and how detailed can they get?
- How does a new player learn to build a good crew, when they face the most trouble while understanding the least?
- How do the player's other ships work in a fleet, when they can't watch them directly?
- Besides the ship's legacy, what else carries over to a new captain: money, other ships, relationships?
