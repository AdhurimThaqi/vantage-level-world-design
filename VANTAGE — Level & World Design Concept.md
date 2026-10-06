# VANTAGE — Level & World Design Concept

Oct 4, 2026 · @Adhurim Thaqi

Module I.BA\_LWD · HS26 · solo project · Unity 6 (URP), third person · revised Oct 6, 2026

## Premise

VANTAGE is a single third-person level in which the player climbs an abandoned military watchtower from the ground floor to the roof.

The setting is a watchtower on the edge of an industrial site, abandoned after the position was overrun. Its automated defence system is still running and treats anyone inside as a target. The story exists to justify the space and is told entirely through the environment — no cutscenes, no text logs, no dialogue.

The climb is the structure. The radio mast on the roof is visible from outside before the player enters, so the level needs no objective marker: wayfinding is solved by architecture.

Target play time is 10 to 15 minutes.

## Player and mechanics scope

Mechanics are deliberately minimal so that effort goes into spatial design rather than systems the module does not assess.

| System | Scope |
| --- | --- |
| Perspective | Third person, over the shoulder |
| Movement | The course template's cover-shooter character: run, crouch, take cover, vault, roll |
| Weapons | Two: a weak pistol, then a rifle found on level 3 (the player starts unarmed) |
| Enemies | One drone prefab with two behaviours (mobile drone, fixed turret), plus the template's soldiers |
| Health | The template's health and hit system, shared by player and enemies |
| Feedback | Muzzle flash, hit particle, ammo and health readout, a drone eye that glows before it fires |

The level was first built in first person, as planned. It is now third person only, using the character from the course's *Third Person Cover Shooter* template. The cover system is the reason: every space below the roof is designed around cover, and the template's character can actually use it (take cover, peek, vault), so the roof taking cover away is felt much more strongly. The template also comes with animations, so the animation cost that first person was meant to avoid no longer exists. The two problems third person brings are handled in the level: walls between the camera and the character fade out in tight interiors, and the corridor rooms are sized so the camera fits.

The two weapons are a pacing tool rather than a feature. The pistol is deliberately weak, so the corridor level is tense. Finding the rifle on level 3 is a power spike placed immediately before the hardest space — relief and then a test, back to back.

## Level structure

The tower is four levels climbed in one direction, and each differs in volume and engagement distance. That rule is what keeps a stacked tower from feeling like one room repeated.

| Level | Space | Engagement distance | Way up |
| --- | --- | --- | --- |
| 1 | Lobby — wide, double height | Long, open sightlines with cover | Open stairwell, visible from the door |
| 2 | Corridors — tight offices and hallways | Close, two to five metres | Exterior fire escape |
| 3 | Collapsed level — holes in floor and ceiling | Mixed, enemies above and below | Collapsed slab forming a ramp |
| Roof | Open deck, radio mast, parapet | Open, all directions | — |

Read down the engagement column: long, close, mixed, open. No two levels play the same way, and the rhythm follows from the structure rather than being added to it.

**The approach** starts the player unarmed in the overrun yard in front of the tower. A lit guard hut is the brightest thing in view, and the pistol lies on its desk. Two drones guard the tower door and only react when the player comes close: a short tutorial fight before the tower itself.

**Level 1** follows with one slow drone in a wide lobby — enough to teach what the enemy is and what cover does.

**Level 2** is where the weak pistol is felt. Three drones wait in the rooms furthest from the stairs, so the fights are close ambushes with nowhere to fall back to.

**Level 3** is the level that carries the design. It is the only space where the fight is genuinely volumetric, and it is where the player can look down through a hole and see the lobby they started in. The rifle sits in a lit alcove slightly off the critical path. Two soldiers and two drones hold the floor, one drone rising from the collapse shaft below.

**The roof** reverses what the level has taught. Every space below offered something to hide behind; the roof takes it away. A turret on the mast denies the centre while three waves of drones (two, three, then four) arrive over different edges, so the player must keep moving while staying out of its line. When it ends, they stand at the parapet and look straight down at where they began.

One exterior stretch is deliberate. Stepping out of a dark corridor onto an open fire escape, exposed, with the yard below, is a pacing beat between two interior spaces — and the only point in the climb where the player is outside.

## Spaces, events, emotions

This is the course's own three-layer framework, and the level is designed from it rather than annotated with it afterwards.

| Space | Event | Emotion |
| --- | --- | --- |
| Approach | Arrive unarmed, find the pistol in the guard hut, first kill | Curiosity, then competence |
| Lobby | One slow drone, learn what cover does | Confidence |
| Stairwell | Transition, first glimpse of the mast above | Anticipation |
| Corridors | Three close ambushes with a weak weapon | Tension |
| Fire escape | Exposed exterior climb, no cover, yard below | Vulnerability |
| Collapsed level | Rifle pickup, then a firefight in three dimensions | Relief, then pressure |
| Slab ramp | Final climb, light increasing | Anticipation |
| Roof | Turret denies the centre, three waves of drones | Exposure, then release |

The pacing curve that follows from this is calm, tense, hardest, release.

## Environmental storytelling

Five props carry the whole story, with no text anywhere in the level.

1. A sandbag position in the lobby facing inward — they were defending against something already inside.
2. A door on level 2 barricaded from the player's side — they were retreating upward too.
3. Bunks on level 2 with fewer beds than the gear present — more people than the place was built for.
4. A body on level 3, with the rifle beside it — someone got this far and no further, so the reward carries a cost.
5. A radio on the roof, still powered, transmitting to nobody.

The test is that a player should be able to reconstruct *they retreated upward, they did not make it, nobody answered* without reading a word.

## Procedural generation

Level 2's interior partition layout is generated from a seed between hand-placed entry and exit anchors.

A room-and-corridor algorithm subdivides a fixed footprint; walls, doorways and prop placement vary per seed. The entry point, the exit point and the enemy count stay authored.

The constraint is the point rather than the algorithm: the layout varies, the pacing does not. Treating generation as a design tool with authored boundaries is what makes it useful here rather than a novelty. The seed is logged with every playtest so results stay reproducible.

If time runs short this degrades to seeded cover and prop placement in the lobby, which still demonstrates the method.

## Guidance and wayfinding

The radio mast is the level's sight, seen four times across the climb: from outside on approach, through a corridor window on level 2, through a ceiling hole on level 3, and standing beside the player on the roof.

Three supporting cues:

- Every vertical connection is the brightest thing in its room, so light marks the way up.
- Drone hum and wind grow louder toward the next level, so sound pulls the player forward.
- Nothing is sealed behind the player. Going back is always possible; going up is simply more interesting.

That last point is a testable claim rather than an assumption. If playtests show nobody walks backwards, the guidance works, and that becomes a documented finding.

## Lighting and atmosphere

Brightness rises with height, so the lighting curve and the emotional curve move together.

| Space | Lighting | Intent |
| --- | --- | --- |
| Lobby | Warm low sun through broken glass, long shadows | Inviting, readable |
| Corridors | Emergency lighting only, hard shadows | Threat, disorientation |
| Fire escape | Open sky, full exposure | Vulnerability, and the first real view |
| Collapsed level | Shafts of daylight through holes in the structure | Dramatic, reveals the verticality |
| Roof | Full golden hour, wide sky | Release |

One Volume profile per space. Baked GI using Adaptive Probe Volumes. The roof is the vertical slice — the one space taken to full art and lighting quality, chosen because it has the best light and is the last thing seen in any demonstration.

## Performance optimization

A tower of separated levels is a natural case for occlusion culling, since only one level is ever visible from inside.

- Occlusion culling baked with each level as its own occlusion area
- LOD groups on the heaviest props
- Baked lighting plus light probes for dynamic objects
- Profiler capture before and after, with frame times compared and screenshots kept for the documentation

This is roughly half a day of work against a skill the module lists explicitly.

## Production scope and asset use

The project is solo, which the assessment criteria allow, and scoped so that a bad week costs polish rather than content.

|  | Scope |
| --- | --- |
| Length | 10 to 15 minutes |
| Spaces | 4 levels, 3 vertical connections |
| Enemies | 1 drone prefab with 2 behaviours, plus the template's soldiers |
| Weapons | 2 |
| Scripts | About 20 of my own (gameplay, generator, logging, editor tools that build the tower) |
| Blockout complete | Course week 5 |
| Full art and lighting pass | Roof only |

**What is bought in.** The character, the soldier AI, the cover system and the weapons come from the course template (*Third Person Cover Shooter*, with *Pistol Animset Pro* for animations). The surrounding industrial yard, the props and the concrete and asphalt textures come from *RPG/FPS Game Assets — Industrial Set v2.0*. The tower itself is not a kit: it is a ProBuilder blockout generated by my own editor tool, textured with the industrial pack's concrete. Every package used will be listed in the project documentation with publisher and purpose.

**What is my own work.** The level layout, the floor plan of every space, the vertical structure and its connections, encounter placement and spacing, the lighting design, the prop placement that carries the story, all gameplay scripts, and the procedural generator. The asset packs supply the bricks; the building is designed here.

**On the surrounding yard.** Only the approach in front of the tower is playable (the guard hut and the two door drones); the rest of the military compound around it is set dressing. The yard serves three purposes: it makes the tower read as the tall thing, it provides the view from the roof that pays off the climb, and it explains why a watchtower stands here.

## Module requirements coverage

| Requirement | How VANTAGE covers it |
| --- | --- |
| Prototyping, creating and testing levels | ProBuilder blockout, iterated across playtests |
| World design supporting gameplay | Each level teaches one spatial combat idea |
| World design supporting narrative | Five-prop environmental story, no text |
| Visual and structural influence | Lighting arc tied directly to the vertical structure |
| Concept and iterations | This document, plus documented revisions after playtests |
| Procedural methods | Seeded level 2 layout between authored anchors |
| Performance optimization | Occlusion culling, LODs, profiler comparison |
| Layout (spatial) | Four levels, distinct volumes and engagement distances |
| Pacing (temporal) | Calm, tense, hardest, release |
| Guidance and spatial cues | Mast landmark, light, sound |
| Environmental storytelling | Five props, reconstructable without text |
| Testing and feedback loops | Peer playtests with logged findings and changes |
| Creating sights | The mast, and the view down from the roof |
| Critical path | Strictly upward, one direction, no branching |
| Vertical slice | Roof, full art and lighting pass |

One gap is worth naming. The module lists working together to create levels as a social skill, and this is a solo project. I intend to cover it by documenting peer playtesting properly — who tested, what they said, what changed as a result — which also serves the testing requirement.

## Revisions

| Date | Change | Why |
| --- | --- | --- |
| Oct 5, 2026 | First person → third person only. The first-person controller and the V key that switched views were removed. | The template's cover system is what makes the cover-based spaces and the cover-less roof work. |
| Oct 5, 2026 | The pistol moved from the lobby reception desk to a guard hut in the yard, with two drones at the tower door. | Gives a short outdoor tutorial fight before the tower and makes the first goal a light, not a marker. |
| Oct 5, 2026 | Soldiers from the template added on level 3 and the roof, next to the drones. | Soldiers take cover and flank, which drones cannot. Mixing them makes level 3 the hardest space as planned. |
| Oct 6, 2026 | Military compound (perimeter wall, checkpoint, tents, floodlights) built around the yard. | Explains why a watchtower stands here and gives the roof view something to look at. |

## Open questions

- [ ] Is a single tower with four levels enough scope for the project mark, or is more horizontal space expected?
- [ ] How much asset-pack use is acceptable, and how should it be declared in the documentation?
- [ ] Does the procedural requirement need to affect playable layout, or is seeded prop and cover placement sufficient?
- [ ] Should the vertical slice be one space at full quality, or a thinner art pass across the whole level?
- [ ] For the solo social-skills requirement, is documented peer playtesting the expected substitute?
