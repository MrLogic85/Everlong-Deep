# Pirate Game: Social Model

Oct 27, 2026 · Fredrik

## Purpose

The data the social core runs on: people, how they relate, what they believe and what they want. The rumor doc says how stories behave and the crew doc what wants do for a crew; this doc says what those things are. Values here are starting points, tuned by listening to the tavern (see the simulation architecture doc, "5. Stop: the tavern sounds alive").

**Belief is external, want is internal.** "I saw him at the bar" is a belief; "I want a drink" is a want. They feed each other: a belief can give rise to a want, and a want decides which beliefs matter.

## Scales

| Value | Range | Meaning at the ends |
| --- | --- | --- |
| Confidence, relevance, strength, privacy, trust | 0 to 1 | None to complete |
| Opinion | −1 to 1 | Hate to love |
| Severity dial | 0 to 1 | Harmless to atrocious |
| Blame dial | −1 to 1 | Fully justified to fully the actor's fault |

Rates are per hour of simulation time. Time and randomness follow `docs/CODE_DESIGN_PRINCIPLES.md` §4, so a seed reproduces the same chatter.

## Person

| Field | What it is |
| --- | --- |
| Id, name | Who they are |
| Traits | Who they are inside, such as *tale-spinner*, *gossip*, *secretive*, *honest*. Traits color what a person tells, how much they talk, and how much others trust them. |
| Place | The place or ship they are in (see the simulation architecture doc, "Things") |

## Relation

One per direction: how A sees B can differ from how B sees A.

| Field | What it is |
| --- | --- |
| Ties | The bond, zero or more |
| Opinion | How A feels about B |
| Trust | How much A believes what B says |

Ties and feelings are separate, so a hated brother is still a brother. Ties can be one-sided: A's *captain* is B, B's *crew* is A.

| Tie | Kind |
| --- | --- |
| Parent, child, sibling | Fixed |
| Spouse, captain, crew | Made and broken by events |
| Friend, rival | Formed and broken by the simulation, as below |

A friend tie forms when high opinion and trust hold for a day, and a rival tie when low opinion does; trust doesn't matter to a rival. Each breaks when feelings hold past a milder mark for a day, so feelings near a mark don't make the tie flicker.

A tie is the holder's own fact. What anyone else knows about it is a belief, and can be false.

## Belief

| Field | What it is |
| --- | --- |
| Holder | Who believes it |
| Claim | What they believe happened |
| Confidence | How sure they are |
| Relevance | How much they want to talk about it |

There is no rumor type: a rumor is a belief someone has the relevance to tell. A listener never copies the teller's belief; they form their own.

### Claim

A claim is structured, so it can be colored, matched and logged without an LLM.

| Field | What it is |
| --- | --- |
| Actor | Who did it, or unknown |
| Act | What was done, from a list that grows: *sank*, *killed*, *robbed*, *attacked*, *betrayed*, *helped*, *was seen at* |
| Target | Whom or what it was done to |
| Place, when | Where and roughly when |
| Dials | Severity and blame |
| Witness | Who claims to have seen it, or unknown |

Actor, target and place are each a person or a thing, by id (see the simulation architecture doc, "Things"). Nothing in a claim refers to what really happened, because the truth is never stored.

**First-hand is the witness, not a flag.** The holder is first-hand when they are the witness. "I saw it myself" sets the teller as witness, true or not, and retold it becomes "Mara says she saw it". A teller may drop the witness, and the story goes on as hearsay.

**Telling colors the dials.** A teller shifts severity and blame toward their opinion of the actor and target, by how much their traits allow. A tale-spinner can also make up a new claim, for example by mixing the actors and acts of two stories.

### Same story or another

When a listener hears a claim, they compare it with the beliefs they hold:

| The claim has | It is | Effect |
| --- | --- | --- |
| The same act and target, and the same actor with dials close to theirs | The same story | Confidence moves toward the teller's, weighted by trust; it never adds up. Relevance rises. |
| The same act and target, but another actor or dials far apart | A competing version | The listener keeps the version they believe more, by confidence and trust in the teller |
| Anything else | A new story | A new belief, with confidence from the teller's, weighted by trust |

Place and time don't decide a match yet.

### Fading

Relevance falls every hour, and a belief with no relevance left is forgotten. It falls slower when:

- the story is **important** to the holder: it involves them, someone they have a tie with or a strong opinion of, or the target of one of their wants;
- the holder is **confident** in it;
- the holder is the **witness**;
- a **want** is linked to it.

The fade rate is computed from these each hour, never stored. Hearing the story again, or something related happening, raises relevance. A forgotten eyewitness account is gone for good, which is how witnesses fade from the world.

## Want

| Field | What it is |
| --- | --- |
| Holder | Who wants it |
| What | From a list that grows: *drink*, *rest*, *shore leave*, *money*, *respect*, *company*, *revenge* |
| From whom | Who could give it, if anyone: the bartender, the captain |
| Target | Who or what it is about, if anyone: revenge on Pell |
| Strength | How much they want it |
| Privacy | How unwilling they are to voice it; they tell it only to someone whose trust and opinion are above it |
| Audience | Who might care to hear it: anyone, people they have a given tie with, or people with a want of the same kind |
| Relevance | How likely they are to bring it up, separate from strength: a strong revenge want can stay unspoken |
| Duration | *Permanent*; *fading*, with a rate at which strength falls; or *growing*, with a rate at which strength rises until it is met |

Needs are wants: hunger is a growing want.

## Open questions

- Which traits come first, and how much does each color a story?
- How far apart can dials be before a story becomes a competing version?
- Should relevance fall the same way for wants as for beliefs?
