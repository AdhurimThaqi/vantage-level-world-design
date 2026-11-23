# VANTAGE audio: production guide

This folder holds your REAPER projects and raw recordings. It is outside the Unity project on purpose: only the rendered WAVs go into `Vantage_First-Person/Assets/Audio/`.

**Whose part is what (for SOUNDCREA).** The sound design, recording, editing, music, the reverb presets and the final mix are **yours**. Claude built the code that plays the sounds: the sound bank, the zones, the music states, footsteps, the event hooks, the settings menu, the Sound Board and (at your request, 2026-11-16) the mixer's structure; the levels in it are yours to set. Declare that help in your hand-in.

**Cuts made by Claude (2026-11-12, at your request, to be replaced by your REAPER renders if you want):** from your takes in `Source_SFX/` Claude cut, looped (equal-power crossfade), mixed to mono and levelled: `FS_Concrete_01-04` (run steps 0.38/0.98/1.34/1.97 s), `FS_Dirt_01-04` (grass steps 1.41/1.95/2.60/3.24 s), `BODY_Fall` (2.25–3.6 s of the fall), `SHUTTER_Slam` (heavy object falling), `ALARM_Siren` (0.02–4.02 s loop), `GUIDE_Radio` (11.1–19.1 s loop), `DRONE_Hum` (4–14 s loop, +19 dB), `GUN_Pistol`, `GUN_Empty`, `PLAYER_Jump`, `ROBOT_Fire` (laser gun), `ROBOT_Hit` (robot damage), and trimmed silence from `UI_Select`, `UI_Move` and `BOSS_Roar`. **2026-11-16 (after your playtest):** the footsteps were too scratchy and loud, so they were low-passed (concrete 4 kHz, dirt 3 kHz, 4th order) and levelled to −12 / −14 dBFS (bank volume 0.8, pitch 0.9–1.08). `DRONE_Hum` was a high whine, so it was low-passed at 1.2 kHz (filtered as a loop, seamless) to a motor hum at −12 dBFS (bank volume 0.7, heard to 14 m). Declare these in the hand-in, or render your own versions with the same names (the refresh picks them up).

**Your renders, trimmed by Claude (2026-11-18):** the five files you dropped into `Assets/Audio/SFX` were full-timeline REAPER renders (the sound sat after up to 83 s of silence), so the originals moved to `Renders/` and Claude cut them, levels and EQ untouched: `DRUMGUN_Fireshot` → `GUN_Rifle_01-04` (four single shots from inside the 13-shots/s burst, 0.16 s each, played per shot by the drum rifle), `GATE_Opening` → `SHUTTER_Roll` (1.0 s), `HELICOPTER_SFX` → `HELI_Rotor` (85.0–101.5 s, looped with a 1 s crossfade), `ROBOT_Noise` → `ROBOT_Telegraph_01-04` (its four takes, the robots' wind-up), `SFX_Ambient` → `Ambience/AMB_Exterior_Dusk` (looped with a 3 s crossfade). Tip: in REAPER render with *Bounds: Time selection* or *Selected media items* to skip the trimming.

**Cut by Claude to their scenes (2026-11-23, your request: "cut them perfectly for their provided scene or area only"):** your new renders were 105 s files with the sound at the start (originals in `Renders/`). Cut, levels and EQ untouched unless noted: `BOSS_Roar` (2.35 s), `COIN_Collect`, `KEY_Pickup`, `KEYSLOT_Turn`, `PICKUP_Weapon`, `ROBOT_Deflect`, `ROBOT_Hit`, `ROBOT_Servo` (0.77 s, repeats while it walks), `SENTRY_Crack`, `SENTRY_WarningCall`, `VO_Contact`; `FS_Metal` → `FS_Metal_01-04` (four steps, levelled to −12 dBFS like the concrete ones); `HELI_Gun` (one burst per helicopter pass: the first 8 rounds and the last one with its tail, 0.92 s); `BOSS_Reveal` (the 8 s reveal + its 2.5 s fade); `STG_Cleared` (leading silence off). The stingers were whole tracks (`STG_Lockdown` was the same file as `MUS_Combat`, `STG_BossFight` 11 min), so each is now one 5–5.6 s hit with a 2 s fade: `STG_Lockdown` from 16.0 s, `STG_BossFight` from 20.9 s (where it kicks in), `STG_Extraction` from its start. `CAM_SwitchSwoosheEffects` → `CAM_Swoosh_01-04` (new event: every cutscene cut, the peak 0.6 s into each file lands on the cut). `MUS_Explore` and `MUS_Combat` moved to `Music/`, cut to the same 176 s (88 bars of the 120 BPM combat track) with a 2 s loop crossfade: their fade-out tails dipped the music every loop, and they must be the same length. Note: the two layers have different tempos (120 / about 146 BPM), so they can't lock beat to beat.

**Made by Claude (2026-11-23, your request: "this is in your hand you can generate"):** the 11 missing sounds, from your own recordings where one fits plus generated air, hum and tones. Replace any of them with your own render of the same name.

| File | Made from |
|---|---|
| `AMB_L1_Interior` (45 s loop) | ventilation air and a 100 Hz fluorescent buzz (generated), your exterior bed far behind (low-passed) |
| `AMB_L2_Armory` (50 s loop) | darker hall air, metal ticks from your empty-gun click and a metal step (pitched down), your radio take far off, a 2.4 s hall reverb |
| `AMB_L3_Destroyed` (50 s loop) | wind through the breach (generated), two structure groans from your gate render (pitched down), your exterior bed louder |
| `AMB_Helipad` (50 s loop) | strong gusting wind (generated) over your exterior bed |
| `AMB_Debris_01-03` | your heavy-object and falling takes (pitched up, smaller) with a pebble trickle made of your dirt footstep transients, dust and a room |
| `GUIDE_PA` (8.5 s loop) | a two-tone chime and "Lockdown in effect. All units, west hallway." (Windows TTS voice "Microsoft George": check the licence before selling the game) through a tannoy: band-limited, driven, a slap off the walls |
| `GUIDE_Stairs` (8 s loop) | a draft swelling twice, a 50 Hz motor hum, a vent flap (your metal step, pitched up) on each swell |
| `GUIDE_Phone` (6 s loop) | an office phone's double ring (generated warble) through a small speaker in a small room |
| `KEYSLOT_Beep` (4 s loop) | a rising double chirp every 2 s over the reader's faint hum (a different shape from the old single beep) |
| `GUIDE_Wind` (12 s loop) | wind with a resonant howl (generated) |
| `GUIDE_HeliRadio` (9.9 s loop) | two passages of your radio take (not the ones in `GUIDE_Radio`) with your rotor under them, squelch on each key-up, through a radio band |

## Where everything is (in Unity)

- **Vantage → Audio → Sound Board**: every sound of the game in one window.
  - **Events** tab: grouped (Music, Interface, Ambience, Guide, Player and soldiers, Machines and boss, Level), with where each plays (e.g. `MUS_Menu`: title screen, until Play), its WAV files (*Show* finds the file in the Project window), ▶ to listen, and volume, pitch, 2D/3D, distances, loop and mixer group. Empty events are red and silent.
  - **Mix** tab: the mixer (*Open Audio Mixer*), the five zones' reverb, the music states (fades, stinger dip, intro tail) and the guide steps.
  - **Tuning** tab: the remaining numbers (machine shot pitches, fades, the sentries' radio, the body-fall delay …).
- **Files**: `Assets/Audio/Music`, `SFX`, `UI`, `Ambience`, `Voice`. The file name is the event name (`MUS_Menu.wav` is the title screen music). After adding or renaming files: **Vantage → Audio → Refresh Sound Bank** (or the button in the Sound Board).
- **Mixer**: `Assets/Audio/VantageMixer.mixer`. Double-click it, or use *Open Audio Mixer* in the Sound Board.
- **Settings data**: `Assets/Audio/VantageSoundBank.asset` (events and tuning) and the `VANTAGE Audio` object in `SampleScene` (zones, music, guide). The Sound Board edits both.
- **Rule since 2026-11-16**: nothing is synthesised any more. An empty event is silent, except that empty guide steps borrow your `GUIDE_Radio` and empty drone/turret shots borrow `ROBOT_Fire` (pitched); the drone hum and siren have a generated last resort only if their files are deleted.

---

## 1. Render settings (REAPER)

- **Format:** WAV, 48 kHz, 24-bit.
- **Channels:** mono for everything placed in the world (3D: footsteps, siren, shutter, sentry crack, barks, robot, helicopter); stereo for ambience beds, music, stingers and UI.
- **Loops** (beds, siren, servo, rotor, music layers): cut on zero crossings and check the loop point in REAPER (loop the item and listen for a click).
- **Music layers:** `MUS_Explore` and `MUS_Combat` must have the **same tempo, key and exact length** (same sample count). They start on the same sample and loop together forever.
- **Peaks:** leave −1 dBTP headroom on every file.

## 2. Naming

`EVENT_NN.wav`, e.g. `FS_Concrete_01.wav` … `FS_Concrete_04.wav`. A single file can drop the number (`ALARM_Siren.wav`). The game picks one variation at random each time, with a small pitch change.

Put each file in its folder: `Ambience/`, `SFX/`, `Voice/`, `Music/`, `UI/` under `Assets/Audio/`. The folder only keeps things tidy; the **name** connects the file to its event.

## 3. The sound list

**P1** is about 30 files: the SOUNDCREA demo. **P2** is extra.

| Event | Files | What | Group | 2D/3D | P |
|---|---|---|---|---|---|
| `AMB_Exterior_Dusk` | 1 loop | evening wind, distant birds, far generator | Ambience | 2D | 1 |
| `AMB_L1_Interior` | 1 loop | ventilation, fluorescent buzz | Ambience | 2D | 1 |
| `AMB_L2_Armory` | 1 loop | big hall, metal ticks, faint radio | Ambience | 2D | 1 |
| `AMB_L3_Destroyed` | 1 loop | wind through the breach, tarp | Ambience | 2D | 1 |
| `AMB_Helipad` | 1 loop | strong open wind | Ambience | 2D | 1 |
| `FS_Concrete`, `FS_Metal`, `FS_Dirt` | 4 each | footsteps by surface (player and soldiers) | SFX | 3D | 1 |
| `ALARM_Siren` | 1 loop | the lockdown siren (plays at the beacons) | SFX | 3D | 1 |
| `SHUTTER_Slam`, `SHUTTER_Roll` | 1 + 1 | the trap shutter drops; shutter and gates roll up | SFX | 3D | 1 |
| `KEY_Pickup`, `KEYSLOT_Turn` | 1 + 1 | key grabbed; key turned in the slot | SFX | 3D | 1 |
| `SENTRY_Glint`, `SENTRY_Crack` | 1 + 2 | warning tick with the red glint; the distant shot | SFX | 2D tick, 3D crack | 1 |
| `VO_Contact` | 3 | a soldier's bark when it wakes ("Contact!") | Voice | 3D | 1 |
| `ROBOT_Deflect` | 2 | pistol round bouncing off the robot / boss | SFX | 3D | 1 |
| `ROBOT_Servo` | 1 loop | robot and boss walking (the boss plays it lower) | SFX | 3D | 1 |
| `HELI_Rotor` | 1 loop | menu and extraction helicopter | SFX | 3D | 1 |
| `GUN_Rifle` | 1+ (one shot each) | the player's drum rifle, played per shot at 13 shots/s, so keep each clip short | SFX | 3D | 1 |
| `UI_Move`, `UI_Select` | 1 + 1 | title screen | UI | 2D | 1 |
| `MUS_Explore`, `MUS_Combat` | 1 + 1 loop | drone; percussion layer (same tempo, key, length) | Music | 2D | 1 |
| `STG_Lockdown`, `STG_Cleared`, `STG_Extraction` | 1 each | stingers: trap springs, level cleared, boss dead | Music | 2D | 1 |
| `SENTRY_WarningCall` | 2 | "Back to the tower!" in the warning band | Voice | 3D | 2 |
| `AMB_Debris` | 3 | random crumbles around you on level 3 | Ambience | 3D | 2 |
| `DRONE_Hum`, `COIN_Collect`, `PICKUP_Weapon`, `ROBOT_Telegraph` | 1 each | replace the synthesised placeholders | SFX | 3D | 2 |
| `MUS_Menu` | 1 loop | the title screen's theme (fades out on Play; the level music starts after it) | Music | 2D | 1 |
| `BOSS_Reveal` | 1 (not looped) | the finale's boss reveal cutscene (8 s, faded out as the fight starts) | SFX | 2D | 1 |
| `BOSS_Roar` | 1+ | the mech's roar half way through the reveal | SFX | 3D | 1 |
| `STG_BossFight` | 1 | the stinger as the boss fight starts (after the reveal) | Music | 2D | 1 |
| `BODY_Fall` | 1+ | a body hitting the ground (soldiers, the deserter, the player; 0.5 s after the death) | SFX | 3D | 1 |
| `GUN_Pistol`, `GUN_Empty` | 1+ each | the player's pistol shot; the empty click on any gun (replace the template's clips) | SFX | 3D | 1 |
| `PLAYER_Jump` | 1+ | the player's jump (replaces the template's clip) | SFX | 3D | 2 |
| `ROBOT_Fire`, `ROBOT_Hit` | 1+ each | the robot's and boss's shot (the boss pitched down); a rifle round hitting their armour | SFX | 3D | 1 |
| `DRONE_Fire`, `TURRET_Fire` | 1+ each | a drone's shot (small, sharp, a light zap) and a turret's (a heavier cannon thump). Until they exist, `ROBOT_Fire` plays pitched up (drone, ×1.35–1.5) or down (turret, ×0.72–0.8). The turret fires every 0.45 s: keep it short (< 0.4 s) | SFX | 3D | 1 |
| `MUS_BossFight` | 1 loop | the boss fight's track: from the end of the boss reveal until the boss dies (the other music is silent under it; faded 2 s at the death) | Music | 2D | 1 |
| `MUS_End` | 1 (not looped) | the ending: from the boss's death through the extraction and the end screen | Music | 2D | 1 |
| `MUS_Intro` | 1 (not looped) | the intro flyover's track: starts on Play as the menu theme fades (1.5 s), the intro lasts about 40 s (since 2026-11-16); the track is faded 15 s after the hand-back over 8 s (`VantageMusic.IntroTail` / `IntroTailFade`), so about 55–63 s of it is heard if the player waits; `MUS_Explore` fades in after it. End it on a long fade or a sustained tone that sits under the exterior bed. A fight cuts it short with a 3 s fade | Music | 2D | 1 |
| `GUIDE_PA` | 1 loop | L1 lockdown: a PA speaker crackling a looped lockdown announcement at the end of the route (turret hallway) | SFX | 3D | 1 |
| `GUIDE_Stairs` | 1 loop | the way up: a draft and a low hum from the open stairwell (used three times) | SFX | 3D | 1 |
| `GUIDE_Radio` | 1 loop | L2: a field radio murmuring next to the rifle in the Armory | SFX | 3D | 1 |
| `GUIDE_Phone` | 1 loop | L2: a desk phone ringing in the office where the key lies (ring, pause, ring) | SFX | 3D | 1 |
| `KEYSLOT_Beep` | 1 loop | L2: the key slot by the stairwell gate beeping, waiting for the key | SFX | 3D | 1 |
| `GUIDE_Wind` | 1 loop | L3: wind howling through the breach in the robot room's wall | Ambience | 3D | 1 |
| `CAM_Swoosh` | 1+ | a swoosh on each cutscene camera cut (its peak 0.6 s in: `Tuning.SwooshPeak`) | SFX | 2D | 1 |
| `GUIDE_HeliRadio` | 1 loop | finale: the helicopter's radio calling from the pad ("...on approach, clear the pad...") | SFX | 3D | 1 |

**The guide sounds** (`GUIDE_*`, `KEYSLOT_Beep`) are the level's wayfinding: only one plays at a time, from the player's next goal, and only on that floor (`VantageGoalSound`). Make each a seamless loop of 4–10 s with a clear rhythm or voice that is easy to locate, a little brighter than the ambience bed; it is heard from about 30 m and should be clearly audible across a room at the default volume 0.8. Until a guide file exists, your radio loop (`GUIDE_Radio`) plays from that step's anchor in its place (since 2026-11-16; the synthesised two-tone pulse before sounded like a heart monitor).

Any event without files keeps its placeholder (the synthesised siren, drone hum, wind, coin chime) or stays silent. Guns, hits and grunts are the template's own sounds and stay as they are.

## 4. After every render

1. Save the WAVs into `Assets/Audio/<folder>/` with the right names.
2. In Unity: **Vantage → Audio → Refresh Sound Bank.** It fills every event from the files and sets 3D/2D, loop and group. Its log line tells you which events are still empty.
3. Fine-tune an event in `Assets/Audio/VantageSoundBank.asset` (volume, pitch range, min/max distance). The refresh keeps your values; it only replaces the clip list.
4. Press Play. The Console prints `[Vantage] Sound bank: n / 57 events filled (missing: …)`; the Sound Board shows the same in red.

## 5. The mixer

**Vantage → Audio → Refresh Sound Bank** builds `Assets/Audio/VantageMixer.mixer` when it is missing (`VantageMixerBuilder`, 2026-11-16, at your request) and connects it to `VANTAGE Audio`. It never overwrites an existing mixer: tune it in **Window → Audio → Audio Mixer**.

```
Master                          exposed MasterVolume (menu: Master)
├ Music Volume    → Music       MusicVolume on the bus (menu: Music)
├ Ambience Volume → Ambience    AmbienceVolume (menu: Effects)
├ SFX Volume      → SFX         SfxVolume (menu: Effects); Send to Reverb = ReverbWet (set per zone)
├ Voice Volume    → Voice       VoiceVolume (menu: Effects); Send to Reverb fixed at −10 dB
├ UI
└ Reverb                        Receive → SFX Reverb (ReverbRoom, ReverbDecay, set per zone)
```

- **Snapshots** move the faders of `Music`, `Ambience`, `SFX`, `Voice` (not the buses): `Explore` (start; Music −2 dB), `Combat` (Ambience −6 dB), `Cinematic` (SFX −6, Ambience −3, Voice −3, Music 0). Change them in the Audio Mixer window: select a snapshot, move the faders.
- **Why the buses:** a parameter set from a script stops following snapshots, so the menu's sliders sit on the `… Volume` buses and the snapshots on the groups below. (The earlier advice to add a second Attenuation effect doesn't work: Unity allows one per group.)
- **Names are the contract** with the code: the six group names, the three snapshot names and the eight exposed parameters. Renaming one silently turns that part off; the audio test (`-vantageOnly audio`) checks them.

## 6. Tune the spaces and the music

Select `VANTAGE Audio` in the scene:

- **`VantageAudioZones`:** one row per zone (Outside, Level 1, Level 2, Level 3, Helipad), each with *Room* (mB), *Decay* (s) and *Wet* (dB). These are starting values; tune them by ear while walking between floors. *Fade* (2 s) is the crossfade and reverb glide; *Settle* (0.75 s) stops flapping on stairs; *Debris Every* sets how often level 3 crumbles.
- **`VantageMusic`:** *Combat Fade* (2.5 s), *Combat Hold* (6 s of quiet before the percussion goes), *Stinger Dip* (−8 dB under a stinger), *Ending Fade* (4 s after the boss).

## 7. Loudness check

Record a capture of a full run (OBS, or REAPER recording the system output). Measure it in REAPER (*SWS Loudness* or the *Loudness Meter* JS plugin):

- **Integrated about −23 LUFS** (EBU R128 for the demo; games often sit louder, around −16 to −18, so say which target you chose)
- **True peak ≤ −1 dBTP**

Fix the balance on the group faders, not in the files.

## 8. Listening checklist (Play mode)

- Outside → entrance → each floor → helipad: the bed changes smoothly, the reverb grows in the Armory and dries out on the pad.
- Trap: slam + siren + lockdown stinger together; siren stops when level 1 is cleared.
- Footsteps: dirt outside, concrete inside, metal on metal surfaces (outside stairs are concrete).
- A soldier wakes: one "Contact!", not a chorus.
- Sentry: glint tick before the shot, crack from far away.
- Pistol on the robot: deflect sound each hit.
- Music: percussion comes in with a fight and leaves about 6 s after; stingers duck the layers; after the boss the music fades out.
- Title screen: rotor circling, UI clicks, the menu theme; it fades when you press Play and the level music starts after it; the three volume sliders work (Music and Effects need the mixer).
- Guide sounds: on each floor, close your eyes and turn: can you point at the goal? Is it audible over the combat music, but not tiring? Stand on the stairs between two floors: the guide of the floor you leave fades, nothing is heard through a floor.
