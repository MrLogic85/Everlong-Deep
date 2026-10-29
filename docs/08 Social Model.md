# Pirate Game: Social Model

Oct 27, 2026 · Fredrik

## Purpose

The data the social core runs on: people, how they relate, what they believe and what they want. The rumor doc says how stories behave and the crew doc what wants do for a crew; this doc says what those things are. Values here are starting points, tuned by listening to the tavern (see the simulation architecture doc, "5. Stop: the tavern sounds alive").

**Belief is external, want is internal.** "I saw him at the bar" is a belief; "I want a drink" is a want. They feed each other: a belief can give rise to a want, and a want decides which beliefs matter.

## Scales

| Value | Range | Meaning at the ends |
| --- | --- | --- |
| Confidence, relevance, strength, privacy, trust, familiarity | 0 to 1 | None to complete |
| Opinion | −1 to 1 | Hate to love |
| Severity dial | 0 to 1 | Harmless to atrocious |
| Blame dial | −1 to 1 | Fully justified to fully the actor's fault |

The clock counts minutes, and rates change once per hour of simulation time. Time and randomness follow `docs/CODE_DESIGN_PRINCIPLES.md` §4, so a seed reproduces the same chatter.

## Person

| Field | What it is |
| --- | --- |
| Id, name | Who they are |
| Traits | Who they are inside: a value on every trait axis, as below |
| Place | The place or ship they are in (see the simulation architecture doc, "Things") |
| Dead | Whether they have died. The dead never talk, listen or change again; stories about them go on (see "Telling", "Fading" and "Relation") |

### Traits

Each trait is an axis from −1 to 1 between two poles, and everyone has a value on every axis; 0 is unremarkable. A trait **shows**, as its pole's label, only past ±0.5, so "honest" means honesty above 0.5. Traits are fixed: it's who someone is. What others believe of them can change.

A seeded person has 2 to 4 traits that show and the rest within ±0.3, so each reads as someone. *Gossip* or *tale-spinner* is a reading of a profile (talkative and curious; creative and dishonest), not a trait.

| + | − | Visible | Affinity |
| --- | --- | --- | --- |
| Honest | Dishonest | | 0.6 |
| Kind | Unkind | Yes | 0.5 |
| Brave | Cowardly | | 0.2 |
| Loyal | Disloyal | | 0.6 |
| Patient | Impatient | | 0.2 |
| Ambitious | Unambitious | | 0.1 |
| Empathetic | Apathetic | | 0.4 |
| Reliable | Unreliable | | 0.4 |
| Curious | Indifferent | | 0.2 |
| Optimistic | Pessimistic | | 0.2 |
| Responsible | Irresponsible | | 0.3 |
| Generous | Stingy | | 0.3 |
| Creative | Unimaginative | | 0.1 |
| Independent | Dependent | | 0 |
| Adaptable | Rigid | | 0.1 |
| Hardworking | Lazy | | 0.4 |
| Humorous | Humorless | Yes | 0.4 |
| Confident | Insecure | Yes | 0 |
| Organized | Disorganized | | 0.1 |
| Disciplined | Undisciplined | | 0.1 |
| Talkative | Quiet | Yes | −0.3 |
| Open | Secretive | | 0.2 |

A **visible** axis shows at a table; a hidden one is learned only from what someone does. **Affinity** is whether people like others alike on the axis: positive likes alike, negative means opposites attract, 0 doesn't matter.

**Choices.** Each option at a decision point has a row of weights, and a person's **pull** toward it is Σ weight × trait. The situation adds its own **context**, and the option's score is context plus pull. Options compete by chance e^score / Σ e^score, drawn from the world's randomness, so people play their profile rather than follow it; a yes-or-no choice scores against not doing it at 0. A log line names the two or three axes that pulled most.

| Option | Weights |
| --- | --- |
| Talk now | Talkative 1.0, Confident 0.5 |
| Approach a stranger | Confident 0.6, Curious 0.5, Talkative 0.5, Open 0.6 |
| Tell of others' business | Curious 0.8 |
| Tell a scandal over good news | Kind −0.6, Optimistic −0.6 |
| Color the dials (how far) | Creative 0.6, Honest −0.6 |
| Color toward harsher blame | Kind −0.6, Empathetic −0.4 |
| Claim "I saw it myself" | Honest −0.8, Confident 0.4 |
| Keep the witness when retelling | Honest 0.6, Reliable 0.4 |
| Keep a detail when retelling | Honest 0.6, Reliable 0.4 |
| Asked: tell it as believed | Honest 0.6, Open 0.3 |
| Asked: tell a colored version | Honest −0.4, Creative 0.4 |
| Asked: hold back | Open −0.8 |
| Asked: lie | Honest −1.0 |
| Believe the teller | Adaptable 0.5, Curious 0.3, Independent −0.5 |

A new behaviour is a new row, and a new axis a new column. When listening shows that two traits together must act unlike their sum, that becomes an explicit row too, not a special case in code.

## Relation

One per direction: how A sees B can differ from how B sees A.

| Field | What it is |
| --- | --- |
| Ties | The bond, zero or more |
| Opinion | How A feels about B |
| Trust | How much A believes what B says |
| Familiarity | How well A knows B: 0 for a stranger, rising with each conversation between them and fading slowly |
| Impressions | What A thinks B's traits are, as below |

Ties and feelings are separate, so a hated brother is still a brother. Familiarity is separate from trust: A can know B well and still not believe B ([#54](https://github.com/MrLogic85/Everlong-Deep/issues/54)). Ties can be one-sided: A's *captain* is B, B's *crew* is A.

| Tie | Kind |
| --- | --- |
| Parent, child, sibling | Fixed |
| Spouse, captain, crew | Made and broken by events |
| Friend, rival | Formed and broken by the simulation, as below |

A friend tie forms when high opinion and trust hold for a day, and a rival tie when low opinion does; trust doesn't matter to a rival. Each breaks when feelings hold past a milder mark for a day, so feelings near a mark don't make the tie flicker. The dead's ties never form or break; the living's ties to them still change.

A tie is the holder's own fact. What anyone else knows about it is a belief, and can be false.

### Impressions

An **impression** is A's view of one of B's trait axes, −1 to 1. A holds one only for the axes A has formed a view of; the rest are unknown, not 0. Impressions can be wrong, so a well-liked liar is possible.

- **First meeting:** strangers meet when one tells the other something (see "Telling"). Each side forms impressions of the other's visible axes, each within ±0.3 of the real value, the teller's side first. Their first feelings then come from compatibility alone: opinion 0.5 × compatibility and trust 0.3 + 0.2 × compatibility. What was said doesn't change them.
- **Seeded relations** start with impressions as their history: kin know every axis, hidden ones too, within ±0.1; acquaintances know the visible axes as a first meeting would.

**Compatibility**, how well A thinks they get on with B, is Σ affinity × A's trait × A's impression of B over the axes A has an impression of, divided by Σ |affinity| over those axes, so it runs −1 to 1. It reads only impressions, never B's real traits; with no impressions that matter it is 0.

## Belief

| Field | What it is |
| --- | --- |
| Holder | Who believes it |
| Claim | What they believe happened |
| Importance | How much it matters to them |
| Confidence | How sure they are |
| Relevance | How much they want to talk about it |
| Heard from | Who told it to them, each with when and the version they heard |
| Told to | Whom they told it, each with when, the belief as they held it then and the version they told |

A belief starts at confidence 0.8 to 1 when first-hand and 0.3 to 0.6 when hearsay. **Importance** is the holder's own judgment of who did what to whom, never the teller's dials. It is the told act's severity in the "Claim" table, before any coloring, plus the holder's stake: 0.3 when they are its actor, target or witness, and a tunable amount when they have a tie with or a strong opinion of one of them. It is judged when they first hear or witness the story, and again when they take a version with another actor. Relevance starts at importance.

**A belief with no relevance left is a memory**: still held and believed, but no longer told. Memories are kept; a cap on beliefs comes only if a long run needs one.

A holder keeps one belief per story (act and target), so holder and story name a belief wherever it is referred to.

**Entries are what a person remembers of talk** ([#53](https://github.com/MrLogic85/Everlong-Deep/issues/53)). Each holds the latest telling from or to one person. A telling always gives the teller a told-to entry, and gives the listener a heard-from entry when they hold the belief afterwards, new or already theirs. A teller has nothing new for a listener when their belief equals the one held in their told-to entry for them: the same claim, details included. Heard-from entries keep a listener from counting the same teller twice and let them say who told them. Entries name people by id. An MPC also keeps the narrated lines of a conversation on its entries, the last few per pair and story ([#56](https://github.com/MrLogic85/Everlong-Deep/issues/56)). Whether someone knows of a person or a story is a lookup in their relations, beliefs and entries.

There is no rumor type: a rumor is a belief someone has the relevance to tell. A listener never copies the teller's belief; they form their own.

### Claim

A claim is structured, so it can be colored, matched and logged without an LLM.

| Field | What it is |
| --- | --- |
| Actor | Who did it, or unknown |
| Act | What was done, from a list that grows (below) |
| Target | Whom or what it was done to |
| Place, when | Where and roughly when |
| Dials | Severity and blame |
| Witness | Who claims to have seen it, or unknown |
| Details | A few words that make it concrete: "a knife", "at night" |

Actor and target are each a person or a thing, and place is a thing, by id (see the simulation architecture doc, "Things"). Nothing in a claim refers to what really happened, because the truth is never stored.

| Act | Actor | Target | Place | Severity |
| --- | --- | --- | --- | --- |
| *sank* | Person or unknown | Ship | That ship | 0.8 × importance scale |
| *killed* | Person or unknown | Person | Tavern | 0.9 |
| *robbed* | Person or unknown | Item: what was taken | Tavern | 0.4 × importance scale |
| *attacked* | Person | Person | Tavern | 0.5 |
| *betrayed* | Person | Person | Tavern | 0.6 |
| *helped* | Person | Person | Tavern | 0.1 |
| *was seen at* | Person | Ship or tavern | The target | 0 |

The importance scale is 0.5 + importance / 2, so a thing's importance moves severity by up to half. The actor is never the target. Blame is −0.2 to 1 for a harmful act with a known actor, and 0 otherwise.

**First-hand is the witness, not a flag.** The holder is first-hand when they are the witness, and an actor is the witness of their own act. "I saw it myself" sets the teller as witness, true or not, and retold it becomes "Mara says she saw it". A teller may drop the witness, and the story goes on as hearsay.

**Details are one or two words each** ([#55](https://github.com/MrLogic85/Everlong-Deep/issues/55)). When an event happens, 0 to 2 come from a table for its act and are copied to every witness. A narrator adds more, listing each with what it is about and who said it; only listed details are kept, and some said in dialog are lost. A teller passes on or drops each detail, as "Keep a detail when retelling" says. Details never decide whether two claims are the same story. A detail about a person or thing rather than the story, such as "Pell, tavern regular", is a belief about them, and has no home yet ([#57](https://github.com/MrLogic85/Everlong-Deep/issues/57)).

**Telling colors the dials**, as "Telling" step 4 says. A creative, dishonest teller can also make up a new claim, for example by mixing the actors and acts of two stories.

### Telling

Talk happens in conversations, minute by minute. Everyone has a next moment to consider talking, the first within the run's first hour. Someone an event brings in waits for their first as after a conversation. If they keep quiet, or after a conversation, they consider it again 5 to 20 minutes later. A conversation lasts 3 + 2 × (teller's Talkative + listener's Talkative) minutes, at least 1, and keeps both busy. The listener hears it out at the end and may retell it at their next moment, within the same hour. Moments at the same minute come in the order they were set, never by id. A dead person's moment lapses, and a conversation that a death cuts short ends without effect: nothing is heard, remembered or logged, though a stranger met at its start stays met. The steps that are choices use the rows in "Choices".

1. **Talk now**, a yes-or-no choice.
2. **Listener**, someone alive and free in the same place. If a stranger is there, "Approach a stranger" (context −2) picks one at random; otherwise someone they know, scored opinion + trust + familiarity. With nobody to tell, they keep quiet.
3. **Topic**, any belief with relevance left that isn't the listener's own story (the listener is not its actor, target or witness), that the teller didn't hear from this listener, and that the teller hasn't told this listener in an unchanged version. Score 2 × relevance, plus "Tell of others' business" when the teller isn't in it, plus "Tell a scandal over good news" when the act does harm.
4. **Coloring**, how far, as a reach of 0.3 × the chance from "Color the dials". Severity moves by the reach × the teller's opinion of the target. For a harmful act with a known actor, blame moves by the reach × −opinion of the actor, and a further reach if "Color toward harsher blame" says yes. Each dial stays within its range.
5. **Witness**: a teller who saw it says so; otherwise "Claim I saw it myself" (context −2), and failing that "Keep the witness" (context 1) or the story goes on as hearsay.
6. **Believe the teller**, with context 2 × trust − 1: a new belief with the teller's confidence × trust, or nothing. A listener who already holds the story weighs it as "Same story or another" says, with no roll.

### Same story or another

A listener who already holds a story (the same act and target) weighs each telling of it against their belief. A teller already in their heard-from entries for it only updates the entry. For a new teller:

| The told version names | It is | Effect |
| --- | --- | --- |
| The same actor, or either names an unknown actor | The same story | Confidence grows by (1 − own) × k × the teller's confidence × trust, and relevance by as much. The listener keeps their own claim: coloring never splits a story. |
| Another known actor | A competing version | The listener keeps their own when their confidence ≥ the teller's × trust, and then trusts the teller 0.1 less, as a liar. Otherwise they take the teller's claim, with confidence the teller's × trust, and judge its importance again. |

k and the trust drop are tunable. **A first-hand holder** is the exception to both rows: they keep their claim and confidence, and a teller who contradicts them loses trust.

Near certainty, no single teller wins against a held version (`docs/02 Rumor System.md` → "Settled truths").

Place and time don't decide a match yet.

### Fading

Relevance falls every hour, slower the more important the story is to the holder and when a want is linked to it. The fade rate is computed each hour, never stored. A new teller of the story raises it, as above. A belief whose relevance runs out becomes a memory (see "Belief"). The beliefs of the dead don't fade.

## Events

An event is something that happens at once: an act, its actor and target, a place, an hour, and the people there by role. It brings in anyone it creates, applies the act (a killed target dies), gives the people there their beliefs and writes one log line. Then it is gone, and only the beliefs remain (`docs/02 Rumor System.md` → "How information is born").

| Role | What they come away believing |
| --- | --- |
| Actor | They did it, first-hand |
| Key witness, bystander | One of three, by two rolls: saw it and who did it, first-hand; saw it but not who, first-hand with the actor unknown; or only caught the commotion, hearsay with the actor and the witness unknown |

A key witness has the higher chance in both rolls, whether they saw it and whether they saw who; the chances are tunable. Everyone there holds the same severity, the "Claim" table's value, and the same blame, drawn within its range, where they know the actor.

A person an event brings in enters a stranger to all.

Until the player and the LLM cause events, a run can inject a hand-written one at the first minute of an hour, before that minute's talk (`docs/MANUAL_TESTING.md` → "Running a seeded world").

## Want

| Field | What it is |
| --- | --- |
| Holder | Who wants it |
| What | From a list that grows: *drink*, *rest*, *shore leave*, *money*, *respect*, *company*, *revenge* |
| From whom | Who could give it, if anyone: the bartender, the captain |
| Target | Who or what it is about, if anyone: revenge on Pell |
| Strength | How much they want it |
| Privacy | How unwilling they are to voice it; they tell it only to someone whose trust, opinion and familiarity are above it |
| Audience | Who might care to hear it: anyone, people they have a given tie with, or people with a want of the same kind |
| Relevance | How likely they are to bring it up, separate from strength: a strong revenge want can stay unspoken |
| Duration | *Permanent*; *fading*, with a rate at which strength falls; or *growing*, with a rate at which strength rises until it is met |

Needs are wants: hunger is a growing want.

## Open questions

- Should relevance fall the same way for wants as for beliefs?
