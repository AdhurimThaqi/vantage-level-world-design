# VANTAGE: 5-minute presentation, speaker script

The professor asked for: **a map of the level, what happens, the emotion you want to create, and a blockout if possible.** The deck covers all four:
- **Map:** slides 3–5 (top-down plans rendered from the real scene, with route, gates and emotions)
- **What happens:** the numbered beats on slides 3–5
- **Emotion:** the emotion pills and the curve on slide 6
- **Blockout:** the in-engine screenshots

The same text is in the speaker notes of each slide (PowerPoint: *Slide Show → Use Presenter View*).

| # | Slide | Time | Running total |
|---|---|---|---|
| 1 | Title | 0:20 | 0:20 |
| 2 | One tower, four spaces | 0:35 | 0:55 |
| 3 | Level 1 · The lockdown | 0:45 | 1:40 |
| 4 | Level 2 · The armory | 0:40 | 2:20 |
| 5 | Level 3 and the helipad | 0:45 | 3:05 |
| 6 | The emotion curve | 0:30 | 3:35 |
| 7 | Spatial communication | 0:40 | 4:15 |
| 8 | Where it stands | 0:30 | 4:45 |

That leaves about 15 seconds of slack. If you run long, shorten slide 7 to three of the six cards (frame the goal, colour, one interaction).

---

**1 · Title (20 s)**
Hi, I'm Adhurim. My level is VANTAGE: a third-person shooter level in a military training base that has been overrun. You fight your way up a three-floor tower. Every floor has its own kind of lock, and the only way out is a helicopter from the helipad.

**2 · One tower, four spaces (35 s)**
The world is my own Blender scene: a one-kilometre valley with the base, and the tower on a plot in the middle. On the right is the level as a cross-section, read from the bottom up. Each floor has its own space, its own lock and its own emotion: a trap on the ground floor, a key on the armory floor, a robot and a patrol door on the destroyed floor, and a boss on the helipad.

**3 · Level 1, the lockdown (45 s)**
Level 1 is a trap. You pick up the pistol outside, walk in, and just inside the door the alarm trigger drops the shutter in front of the stairs: siren, red light, and every soldier on the floor comes for you. *(point at 2)* If the security camera in Room A sees you first, it sets the alarm off early, so shooting it is a choice before the trap. During the lockdown the left door stays locked, so there is one way on: Rooms A, B and C, the West Wing and the turret hall. *(trace the route)* When the last enemy and the turret are down, the alarm stops and the shutter rolls up. Shock first, then control, then relief.

**4 · Level 2, the armory (40 s)**
Level 2 is a puzzle under fire. You come up into a big hall with turrets and drones, so the pressure stays up. The goal is visible from the start: the stairwell gate with its red lamp. The rifle is in the Armory and the key is on the office desk, floating and glowing so it reads as important. The key slot is the only thing in the whole game you use with E, so it stands out. When the key turns the lamp goes green, and once the floor is clear the gate opens: reward, purpose, relief.

**5 · Level 3 and the helipad (45 s)**
Level 3 is the destroyed floor. You arrive and see daylight through the blown-out side of the building: awe, and also a warning. When the indoor soldiers are down, the robot room opens. The heavy robot only takes damage from the rifle, which tests that you found it on level 2. Through the breach two patrols guard the broken slab, and killing both unlocks the SE room and the exit to the outside stairs. On the helipad two soldiers wait, then stepping on the pad brings the boss. When it dies the helicopter lands: the climax, then the release.

**6 · The emotion curve (30 s)**
This is the curve I design for. Every level has its own rise and fall: the trap is a sudden spike on level 1, level 2 is steady pressure that ends in relief at the key slot, level 3 builds dread up to the robot, and the boss is the highest peak. The helicopter is the release afterwards, because a level that only stops at its peak feels unfinished.

**7 · Telling the player what to do with the space (40 s)**
The video you recommended, Peter Field's talk on spatial communication, says the level should explain itself before the HUD does. That is how I guide the player. The first corridor frames the shutter in front of the stairs, the exact thing the trap closes. Colours are rules: red is locked or danger, green is open. You can see the camera's light cone before it sees you. Only one object in the game uses E, so it stands out. Doors on the route are open and locked ones are shut. On level 3, daylight and a helipad sign pull you towards the finale.

**8 · Where it stands (30 s)**
Everything you saw is playable today, from the trap to the end screen. The procedural part is the seeded soldier placement, and an automated bot plays the whole level on every change. Next I'm modelling the robot, the boss and the helicopter in Blender, adding props for environmental storytelling and a mood per space, and running peer playtests, especially to balance the lockdown when you only have a pistol. Thank you.

---

## Before you present

- Open `VANTAGE_Presentation.pptx` on the presenting computer once. It uses the **Bahnschrift** font (included with Windows 10/11). On a Mac or a computer without it, present `VANTAGE_Presentation.pdf` instead; it looks the same.
- Questions you might get, with short answers:
  - *What is procedural?* The soldiers' positions per play: each level has more authored spawn markers than soldiers, and a seed picks them, spread over rooms first. Counts, gates and pacing stay authored.
  - *Why third person?* The template's cover system: the spaces are built around cover, and the open helipad takes it away.
  - *What's bought in?* The character and AI template from the course, Pistol Animset Pro and a coin pack. The world, the tower and the player character are my own Blender work.
- The `maps/` images work on their own too (for the written documentation). They are top-down renders of the real scene with the walls drawn from the collision geometry, made with *Vantage → Documentation → Render Floor Plans* in Unity.
