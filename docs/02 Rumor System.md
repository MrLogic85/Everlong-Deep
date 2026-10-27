# Pirate Game: Rumor System Design

Oct 26, 2026 · Fredrik

## Core premise

The world runs on what people believe, not on what happened. NPCs act on beliefs, and information travels, mutates and arrives late.

The moment the system is built for: you walk into a bar far from home and a bounty hunter attacks you over a price on your head that may only ever have been a rumor.

The pirate sandbox (ships, crew, trading, piracy, hunting) is the vehicle. It provides distance, travel and witnesses, which is what rumors need to exist. When scope has to be cut, the rumor system is what gets protected.

## How information is born

Every rumor starts with someone who saw something, or claims to have. The truth is never stored anywhere in the world.

- **Witnesses are the only record.** When something happens, the people present form beliefs about it. After that, the event itself no longer exists, only what each witness thinks they saw.
- **An eyewitness is as close to truth as the world gets.** A first-hand account told convincingly works like stored truth, but only as long as that person lives.
- **Dead crews tell no tales.** If you sink a ship and nobody survives, its crew spreads nothing. Your own crew survives, though, and they may tell the truth, spread rumors, or lie to protect you.
- **Lies can't be disproven, only outshouted.** A false story loses only when a more believable story beats it.

Killing a witness is therefore a meaningful act: it removes the last reliable account of what happened.

**Absence is evidence too.** Silencing witnesses doesn't erase everything. If you hire forty sailors in port and none of them ever come home, people notice. "The captain whose crews never return" becomes a rumor of its own, and the next time you try to hire, nobody signs on.

## How information travels

Rumors move because people move, and people move because their factions give them a reason to.

An NPC in port A who belongs to faction X sails to port B on faction business, and carries what they've heard. The geography of information follows the geography of purpose: navy news travels along patrol routes, merchant gossip along trade lanes, pirate stories between pirate havens.

This makes information flow something the player can learn and predict. Knowing who sails where tells you where a story will land first, and which ship to stop if you want it to never arrive.

The world tracks individuals: who has heard what, and how each of them spins the tale when they pass it on.

**Each person carries a set of things they want to talk about.** Some they want to talk about more, some less, and the more someone wants to talk about something, the more it spreads. Rumors and wants work the same way: a rumor is something someone wants to tell, a want is something someone wants to voice. What spreads isn't copied, though. Each listener reacts through their own personality. See the crew doc for wants.

The rumor system is also hoped to drive dynamic quests, such as delivering a message, finding a person, or transporting someone. See the port doc.

## Events and versions

One thing that happened can give rise to many competing stories, and the world needs to know they're about the same thing.

Example: you sink the *Margarethe* off port A. That one event might circulate as "sank her and killed everyone", "sank her in self-defense", or "was a navy informant all along".

- **The same rumor shouldn't count twice.** When the same story reaches a faction again, it doesn't pile on as if it were new.
- **A different version still matters.** A nastier or kinder version of the same event can shift opinion, because it competes with what's already believed.
- **Echoes aren't confirmation.** Five sailors repeating what they all heard from one drunk is one source, not five. Only independent sources should strengthen a story.

## Believability

Whether a story sticks depends on who tells it, who hears it, and how often it's heard from independent mouths.

Candidate factors, grouped by what they belong to:

| Belongs to | Factor |
| --- | --- |
| The teller | How good they are at telling a story; whether they claim to have seen it first-hand |
| Teller and listener | Whether they like each other; whether their factions like each other |
| The listener | Their faction's existing opinion of the subject, which skews how they hear and retell it |
| The story | How many independent sources tell it; how long it has circulated (more established, or staler?) |

Start with two or three of these and add more only once the behavior is understood. With many interacting factors, it becomes impossible to tell why a rumor took off.

## Factions and individuals

Reputation works on two layers: factions decide access, individuals decide how they treat you once you're inside.

**Factions** hold a classic, coarse standing toward the player. It's needed for things that must be stable, like a pirate haven that only lets pirates in. Factions hear rumors when the rumors arrive with travelling members.

**Individuals** hold their own beliefs, which may disagree with their faction's view.

The two feed each other. Someone who likes you, or whose faction likes you, tends to tell your story more kindly. Rumors in turn move faction standing.

**Tortuga example.** Your pirate standing is high, so the gate opens. But word has reached some people there that you also hunt pirates for bounties. Inside, someone attacks you or a bartender refuses to serve you. Playing more than one role creates exactly this kind of friction, and it's intended.

## Settled truths

When enough people in one place or one faction tell the same story, it hardens into what "everyone there knows".

- It gives places their own history and identity.
- It stops a story from mutating endlessly in that place, which also helps keep the system stable.
- It stands in for ordinary bar chatter, without needing every exchange between individuals to matter.

A settled truth can be overturned, but it takes deliberate effort from the player. That effort is the main active gameplay of the rumor system (see Player agency).

## Player agency

The player doesn't only suffer rumors, they shape them. This is what a player spends time on when they choose to work the rumor system.

| Lever | How it works |
| --- | --- |
| Bragging | You are the source. It only works as well as your own credibility. |
| Bribing | You pay someone to tell your story, borrowing their credibility instead of using yours. |
| Spreading lies | True or false, you can start stories about yourself or others. |
| Silencing witnesses | Leave no survivors, and nothing is told. |
| Intercepting carriers | Stop the ship carrying a story before it reaches port. |
| Seeking information | Go looking for what's being said about you, and where. |

The player should never be forced through long dialogue. Someone who prefers sailing or managing their ship still needs ways to use these levers.

## Surfacing

The system has to be shown off actively, but always through the world, never through a reputation screen.

- **Crew chatter:** crew mention something they heard, or something they did.
- **Letters:** news, warnings or threats arrive by post.
- **Active searching:** the player seeks out information somewhere.
- **Attackers:** they shout why they're attacking.
- **Refusals:** a merchant tells you why they won't trade with you.

The strongest channels are the ones that change what the player can do, like a refused trade or a closed door, because they make the rumor matter instead of just narrating it. The system should reach every playstyle: someone who spends their time sailing or managing the ship should still feel it, not only someone who talks in bars.

## World history

The game starts with history. Ships, captains and ports already have stories circulating when the player arrives.

Since truth is never stored, even the starting legends are only beliefs. Nobody, the player included, can be sure which famous stories actually happened.

## Character personality

An LLM gives individual characters personality at chosen story moments. It looks at everything known about a character and what just happened, and creates an interesting event based on a set of rules.

Rule-based systems handle categories well but struggle to interpret a character's long, specific history. That's where the LLM adds what hand-written rules can't. Not all dialogue comes from the LLM, only certain story events.

## Known risks

| Risk | What goes wrong | Mitigation so far |
| --- | --- | --- |
| Invisibility | A long chain of rumors plays out, and the player only sees "a random guy attacked me". | In-world surfacing that explains why things happen. |
| Feedback loops | Factions that like you tell kinder stories, so they like you more, and so on. Every faction drifts to an extreme and the world freezes. | Settled truths, dying witnesses, competing versions, fading over time. |
| Permanence | A false story settles in a port and closes it to the player forever. | Settled truths can be overturned through player effort. |
| Echo inflation | Repetition from one source gets counted as many confirmations. | Only independent sources strengthen a story. |
| Too many factors | Believability depends on so much that nobody can tell why a rumor took off. | Start with two or three factors. |

## Open questions

- Which two or three believability factors come first?
- How hard and how costly should overturning a settled truth be?
- How does the rumor system reach a player who mostly sails or manages the ship?
- Can the player use the rumor levers without long dialogue, for example by ordering crew to let something slip in port?
- Are rumors mostly about the player, or do NPCs spread just as many about each other?
- How do crew personalities decide whether a crew member protects you, tells the truth, or sells you out?
