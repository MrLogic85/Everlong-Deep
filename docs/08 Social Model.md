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
| Traits | Who they are inside: a value on every trait axis, as below |
| Place | The place or ship they are in (see the simulation architecture doc, "Things") |

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
| Talk this hour | Talkative 1.0, Confident 0.5 |
| Approach a stranger | Confident 0.6, Curious 0.5, Talkative 0.5, Open 0.6 |
| Tell of others' business | Curious 0.8 |
| Tell a scandal over good news | Kind −0.6, Optimistic −0.6 |
| Color the dials (how far) | Creative 0.6, Honest −0.6 |
| Color toward harsher blame | Kind −0.6, Empathetic −0.4 |
| Claim "I saw it myself" | Honest −0.8, Confident 0.4 |
| Keep the witness when retelling | Honest 0.6, Reliable 0.4 |
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
| Impressions | What A thinks B's traits are, as below |

Ties and feelings are separate, so a hated brother is still a brother. Ties can be one-sided: A's *captain* is B, B's *crew* is A.

| Tie | Kind |
| --- | --- |
| Parent, child, sibling | Fixed |
| Spouse, captain, crew | Made and broken by events |
| Friend, rival | Formed and broken by the simulation, as below |

A friend tie forms when high opinion and trust hold for a day, and a rival tie when low opinion does; trust doesn't matter to a rival. Each breaks when feelings hold past a milder mark for a day, so feelings near a mark don't make the tie flicker.

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
| Confidence | How sure they are |
| Relevance | How much they want to talk about it |

A belief starts at confidence 0.8 to 1 when first-hand and 0.3 to 0.6 when hearsay. Relevance starts at the claim's severity, plus 0.3 when the holder is its actor, target or witness.

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

**First-hand is the witness, not a flag.** The holder is first-hand when they are the witness. "I saw it myself" sets the teller as witness, true or not, and retold it becomes "Mara says she saw it". A teller may drop the witness, and the story goes on as hearsay.

**Telling colors the dials**, as "Telling" step 4 says. A creative, dishonest teller can also make up a new claim, for example by mixing the actors and acts of two stories.

### Telling

Each hour everyone, in id order, may tell one belief they held when the hour began, so a story moves at most one person an hour. The steps that are choices use the rows in "Choices".

1. **Talk this hour**, a yes-or-no choice.
2. **Listener**, someone in the same place. If a stranger is there, "Approach a stranger" (context −2) picks one at random; otherwise someone they know, scored opinion + trust. With nobody to tell, they keep quiet.
3. **Topic**, any belief that isn't the listener's own story: the listener is not its actor, target or witness. Score 2 × relevance, plus "Tell of others' business" when the teller isn't in it, plus "Tell a scandal over good news" when the act does harm.
4. **Coloring**, how far, as a reach of 0.3 × the chance from "Color the dials". Severity moves by the reach × the teller's opinion of the target. For a harmful act with a known actor, blame moves by the reach × −opinion of the actor, and a further reach if "Color toward harsher blame" says yes. Each dial stays within its range.
5. **Witness**: a teller who saw it says so; otherwise "Claim I saw it myself" (context −2), and failing that "Keep the witness" (context 1) or the story goes on as hearsay.
6. **Believe the teller**, with context 2 × trust − 1: a new belief with the teller's confidence × trust, or nothing. Until "Same story or another" is built ([#19](https://github.com/MrLogic85/Everlong-Deep/issues/19)), a listener who already holds a belief with the same act and target keeps theirs and draws nothing.

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

- How far apart can dials be before a story becomes a competing version?
- Should relevance fall the same way for wants as for beliefs?
