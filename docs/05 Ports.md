# Pirate Game: Port Design

Oct 26, 2026 · Fredrik

## What a port is

A port is a map of points of interest, not a town you walk around in. You move between places such as the tavern, the market and the shipyard.

This follows the approach of several D&D-style RPGs, and of games like Mount & Blade and Battle Brothers, where menu-based towns work because the world around them is alive.

The benefits:

- No need to design every town, or build a system for generating them.
- No need to populate a whole town. People only need to be at certain key places, which means less to simulate.

The rumor system still tracks every individual. They just need to be somewhere among a handful of places, not walking around.

## What makes ports different

Ports differ in their places, capabilities, goods, ships, factions and people.

Most of these can change over time. Goods shift with trade, factions gain or lose control, people come and go, and stories settle. A port isn't a fixed set of properties, it's a place with its own ongoing story.

## What you do in port

Ports are where most of the systems in the other docs meet.

| Activity | Related doc |
| --- | --- |
| Talk to people, seek out information, brag and bribe | Rumor system |
| Get past faction-controlled access, like a pirate haven's gate | Rumor system |
| Hear what "everyone here knows" | Rumor system |
| Hire crew, give shore leave | Crew and captaincy |
| Trade goods | Trading design |
| Upgrade the ship, commission a new one | Ship design |
| Fence or repaint a stolen ship | Ship design |
| Steal a ship from the harbor | Ship design |
| Find wants, make promises, take on quests | This doc |

## Being found

Being ambushed or tracked down, by a hostile crew or by a messenger, is the moment ports exist for. It's the payoff of the rumor system: someone in the world has a reason to find you, and the port is where they do it.

These encounters happen when you're at, or moving between, points of interest. Being found isn't always hostile. A letter, an offer, someone wanting passage or wanting to join your crew all work the same way.

Where you are is still a decision, just in a different form:

- **Which places you visit.** The busy tavern is where news arrives, and where people come looking.
- **Who you bring.** Crew at your side are protection.
- **How long you stay.** Leaving tonight because someone's been asking about you is a real choice.

Shore leave keeps the crew happy, but a crew spread across the port's taverns leaves the captain with nobody around them.

## When it turns violent

A fight in port drops into a small spatial scene that reuses the same systems as fighting on deck. There is one fighting system for three situations: your own deck, boarding another ship, and violence in port.

The principles from the ship doc carry over:

- Distance costs time. A tavern is a small layout with tables, a bar and exits, just as a deck is.
- The captain can fight, command, or let the crew handle it.
- The crew you brought ashore are the ones standing next to you.

The real combat system must be designed for fighting in a space in general, with ship-specific parts like guns and sails on top, so it works in both places. Early milestones can still use rudimentary, auto-resolved fights.

## Wants, promises and quests

Ports give reasons to go somewhere that aren't just about trading. These come in three layers.

| Layer | How it works |
| --- | --- |
| Wants | Someone needs something, and if you happen to fulfil it, it counts. No commitment needed. |
| Promises | Optional. Committing to a want turns it into a risk and a reward, with your reputation at stake. |
| Formal quests | Posted contracts, such as supplying the town with goods, killing a named pirate, or sinking a rival faction's merchant vessel. |

The hope is that the rumor system can be reused to create dynamic quests. The simplest kinds gain depth through it:

- **Deliver a message.** You become a carrier, and could read, alter, sell or never deliver it.
- **Find a person.** All you have to go on is what people believe about where they are, which may be wrong.
- **Transport someone.** Your passenger has a history, and anyone hunting them will hear which ship they left on.

Quests should come from what NPCs actually want and believe, so the outcome matters to someone in the world.

The log is based on the people you've talked to and what they wanted from you. The balance between what's tracked as a quest and what's just knowledge about people is still open.

## Open questions

- What's the balance in the log between what's tracked as a quest and what's just knowledge about people?
- Should the player's log be able to be wrong, showing what they were told rather than what's true? For prices this is settled: price knowledge comes through the rumor system and can be wrong (see the trading doc).
- Which points of interest does a port have, and are they the same in every port?
