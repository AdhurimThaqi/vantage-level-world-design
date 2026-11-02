# VANTAGE: Sound Design Project Description (updated draft)

Adhurim Thaqi · I.BA_SOUNDCREA.H2601

> **Draft for you to edit.** This brings the October description in line with the game as it is now. Changes from the PDF are listed at the end. Please rewrite it in your own words before handing it in. The implementation code was written with AI help (Claude); say so in the hand-in.

## Project overview

I will design and implement the complete interactive audio for VANTAGE, a **third-person** shooter level set in a military training base at **dusk**. The player fights up a three-floor concrete tower with a helipad on top. The level is my solo project for the module Level and World Design (I.BA_LWD); this SOUNDCREA project adds its sound layer.

- **Format:** individual project (solo), implemented in Unity 6 (**6000.0.58f2**, URP)
- **Scope:** ambience per space, footsteps, the level's key moments (trap, shutter, key, sentries, robot, helicopter), enemy barks, UI and adaptive music (two layers and three stingers)
- **Assessment fit:** sound design, game-ready audio assets and interactive implementation (40 % final project) and the final presentation on 16.12 (50 % oral)

## Concept and player experience

The tower is a trap, and the sound should say so. Outside it is calm and wide. Inside, every floor sounds different, so the player hears progress as well as sees it. The higher you climb, the more open and exposed it gets.

- **Outside / approach (dusk):** evening wind, distant birds, a far generator. Calm, but the sentries on the roof are watching: a glint tick and a distant crack warn you off.
- **Level 1, ground floor:** tight rooms, ventilation and fluorescent buzz. Then the trap: the shutter slams, the siren starts and the lockdown stinger hits.
- **Level 2, armory:** a big hall with a longer reverb, metal ticks and a faint radio. The key turning in its slot opens the way on.
- **Level 3, destroyed floor:** wind through the breach, a loose tarp, debris crumbling around you. The heavy robot's servos, and a pistol round bouncing off it ("you need a better gun").
- **Helipad (finale):** strong open wind and almost no reverb. The boss, then the helicopter's rotor and the extraction stinger. After that the music fades: the tower is silent.

Sound also guides the player: the sentry's tick before the shot teaches the warning ring in the intro, a soldier's "Contact!" tells you you've been seen, and the percussion layer tells you a fight is still on.

## Audio direction

| Area | Direction |
|---|---|
| Ambience | One stereo bed per space (exterior dusk, L1 interior, L2 armory, L3 destroyed, helipad), crossfading as you move; random debris one-shots on level 3. |
| SFX | Grounded and physical. Footsteps by surface (dirt, concrete, metal) for the player and soldiers; the trap's shutter slam and roll; the key and key slot; the siren at the beacons; robot servo and deflects; helicopter rotor. |
| Enemies | A short bark when a soldier first engages (at most one every 3 s across the tower, so it never turns into a chorus); the sentries' glint tick, crack and warning call. |
| Music | A low drone while exploring and a percussion layer, sample-locked to it, that fades in during combat; stingers for the lockdown, a cleared level and the extraction. |
| UI | Short, dry clicks on the title screen; no sci-fi tones. |
| References | The Last of Us (sparse interiors), Firewatch (exposed outdoor ambience), Ghost of Tsushima (silence before combat). |

**Sources:** my own recordings where possible (footsteps, shutter and metal, my voice for the barks), plus CC0 libraries (Freesound, Sonniss GDC) and the course sound pool. Guns, bullet hits and character grunts keep the course template's sounds; they are routed through my mixer.

## Asset list

About 30 WAVs for the demo (P1) and about 12 extras (P2). All are 48 kHz / 24-bit, 3D sources mono, beds and music stereo, named `EVENT_NN.wav`.

| Event | Files | Category | Runtime behaviour | P |
|---|---|---|---|---|
| `AMB_Exterior_Dusk` | 1 | Ambience bed | 2D loop, outside | 1 |
| `AMB_L1_Interior` | 1 | Ambience bed | 2D loop, crossfades on entering L1 | 1 |
| `AMB_L2_Armory` | 1 | Ambience bed | 2D loop, L2 | 1 |
| `AMB_L3_Destroyed` | 1 | Ambience bed | 2D loop, L3 | 1 |
| `AMB_Helipad` | 1 | Ambience bed | 2D loop, the top | 1 |
| `FS_Concrete` / `FS_Metal` / `FS_Dirt` | 4 each | Footsteps | per step, surface by raycast, random clip and pitch | 1 |
| `ALARM_Siren` | 1 | Alarm | 3D loop at the beacons during the lockdown | 1 |
| `SHUTTER_Slam` / `SHUTTER_Roll` | 1 + 1 | Mechanical | trap closes; shutter and gates open | 1 |
| `KEY_Pickup` / `KEYSLOT_Turn` | 1 + 1 | Interaction | key grabbed; key turned | 1 |
| `SENTRY_Glint` / `SENTRY_Crack` | 1 + 2 | Warning | 2D tick with the glint; 3D distant shot | 1 |
| `VO_Contact` | 3 | Voice | 3D bark when a soldier wakes | 1 |
| `ROBOT_Deflect` / `ROBOT_Servo` | 2 + 1 | Enemy | pistol bounce; loop while walking (boss pitched down) | 1 |
| `HELI_Rotor` | 1 | Vehicle | 3D loop, menu and extraction helicopter | 1 |
| `UI_Move` / `UI_Select` | 1 + 1 | UI | title screen | 1 |
| `MUS_Explore` / `MUS_Combat` | 1 + 1 | Music | two sample-locked layers; combat fades in and out | 1 |
| `STG_Lockdown` / `STG_Cleared` / `STG_Extraction` | 1 each | Music | stingers; the layers dip under them | 1 |
| `SENTRY_WarningCall` | 2 | Voice | "Back to the tower!" in the warning band | 2 |
| `AMB_Debris` | 3 | Ambience | random crumbles on L3 | 2 |
| `DRONE_Hum`, `COIN_Collect`, `PICKUP_Weapon`, `ROBOT_Telegraph` | 1 each | SFX | replace synthesised placeholders | 2 |

## Implementation in Unity

Audio runs on Unity's built-in audio (AudioSource, AudioMixer), no middleware.

- **Sound bank:** every event is one entry (clips, volume, pitch range, 2D/3D, distances, loop, mixer group). A refresh tool fills it from the file names, so a new render is one click away from the game.
- **Spatial audio:** 3D sources with logarithmic rolloff and per-event distances; beds, music and UI in 2D.
- **Zones:** the level code already knows which floor the player is on. Each change (after 0.75 s, so stairs don't flap) crossfades the beds over 2 s and glides the mixer's reverb (room, decay, send) to that space's preset.
- **Footsteps:** the character's own step event; a raycast picks the surface (terrain → dirt, metal materials → metal, else concrete), random clip and pitch.
- **Music states:** explore and combat. Combat means a soldier has the player as its target, a drone, turret or robot is engaging, the alarm is on, or the boss is alive. The layer fades in over 2.5 s and leaves 6 s after the last fight. Stingers on the lockdown, a level clear and the boss's death; after the extraction the music fades out.
- **Mixer:** groups Music, Ambience, SFX, Voice, UI and a Reverb return; snapshots Explore, Combat (ambience ducked) and Cinematic (title screen, intro, extraction). Master, Music and Effects sliders in the settings menu.
- **Loudness target:** about −23 LUFS integrated, true peak max −1 dBTP (ITU-T / EBU R128, from the export-specs slides), measured on a gameplay capture.

## Timeline and deliverables

The phases follow the class schedule (Sound, Actions & Events on 04.11 through Group Project Implementation on 09.12), so each topic is applied to VANTAGE the week it is taught.

Deliverables for 16.12:

- Playable Unity build of VANTAGE with full audio
- Audio document (overview, direction, asset list) and the final asset folder
- Short gameplay capture (2–3 min) showing zones, footsteps, the trap, combat and music states
- Presentation with reflection on design and implementation decisions

---

### What changed from the October PDF

- First person → **third person**; abandoned watchtower → **military training tower at dusk** with three floors and a helipad
- Unity 6000.0.56f1 → **6000.0.58f2**
- Zones come from the level code instead of trigger volumes and Audio Reverb Zones: the reverb is on a mixer return, its preset set per space
- Wood creaks and water drips are gone (it's a concrete military tower); the trap, the key, the sentries, the robot and the helicopter are new
- Footstep surfaces: grass/wood/concrete/metal → dirt/concrete/metal (what the tower is made of)
- The rifle and reload, hit and death sounds stay the course template's; my work is the level-specific sounds
- Music: two layers plus **three stingers**
- About 20 → about 30 assets for the demo, plus extras
