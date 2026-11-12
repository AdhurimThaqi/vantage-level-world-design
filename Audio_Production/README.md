# VANTAGE audio: production guide

This folder holds your REAPER projects and raw recordings. It is outside the Unity project on purpose: only the rendered WAVs go into `Vantage_First-Person/Assets/Audio/`.

**Whose part is what (for SOUNDCREA).** The sound design, recording, editing, music, the mixer, the reverb presets and the final mix are **yours**. Claude built the code that plays the sounds: the sound bank, the zones, the music states, footsteps, the event hooks and the settings menu. Declare that help in your hand-in.

**Cuts made by Claude (2026-11-12, at your request, to be replaced by your REAPER renders if you want):** from your takes in `Source_SFX/` Claude cut, looped (equal-power crossfade), mixed to mono and levelled: `FS_Concrete_01-04` (run steps 0.38/0.98/1.34/1.97 s), `FS_Dirt_01-04` (grass steps 1.41/1.95/2.60/3.24 s), `BODY_Fall` (2.25–3.6 s of the fall), `SHUTTER_Slam` (heavy object falling), `ALARM_Siren` (0.02–4.02 s loop), `GUIDE_Radio` (11.1–19.1 s loop), `DRONE_Hum` (4–14 s loop, +19 dB), `GUN_Pistol`, `GUN_Empty`, `PLAYER_Jump`, `ROBOT_Fire` (laser gun), `ROBOT_Hit` (robot damage), and trimmed silence from `UI_Select`, `UI_Move` and `BOSS_Roar`. Declare these in the hand-in, or render your own versions with the same names (the refresh picks them up).

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
| `MUS_BossFight` | 1 loop | the boss fight's track: from the end of the boss reveal until the boss dies (the other music is silent under it; faded 2 s at the death) | Music | 2D | 1 |
| `MUS_End` | 1 (not looped) | the ending: from the boss's death through the extraction and the end screen | Music | 2D | 1 |
| `MUS_Intro` | 1 (not looped) | the intro flyover's track: starts on Play as the menu theme fades (1.5 s), plays to its own end after the intro (23 s) and rings out into the ambience; `MUS_Explore` fades in after it. End it on a long fade or a sustained tone that sits under the exterior bed. A fight cuts it short with a 3 s fade | Music | 2D | 1 |
| `GUIDE_PA` | 1 loop | L1 lockdown: a PA speaker crackling a looped lockdown announcement at the end of the route (turret hallway) | SFX | 3D | 1 |
| `GUIDE_Stairs` | 1 loop | the way up: a draft and a low hum from the open stairwell (used three times) | SFX | 3D | 1 |
| `GUIDE_Radio` | 1 loop | L2: a field radio murmuring next to the rifle in the Armory | SFX | 3D | 1 |
| `GUIDE_Phone` | 1 loop | L2: a desk phone ringing in the office where the key lies (ring, pause, ring) | SFX | 3D | 1 |
| `KEYSLOT_Beep` | 1 loop | L2: the key slot by the stairwell gate beeping, waiting for the key | SFX | 3D | 1 |
| `GUIDE_Wind` | 1 loop | L3: wind howling through the breach in the robot room's wall | Ambience | 3D | 1 |
| `GUIDE_HeliRadio` | 1 loop | finale: the helicopter's radio calling from the pad ("...on approach, clear the pad...") | SFX | 3D | 1 |

**The guide sounds** (`GUIDE_*`, `KEYSLOT_Beep`) are the level's wayfinding: only one plays at a time, from the player's next goal, and only on that floor (`VantageGoalSound`). Make each a seamless loop of 4–10 s with a clear rhythm or voice that is easy to locate, a little brighter than the ambience bed; it is heard from about 30 m and should be clearly audible across a room at the default volume 0.8. Until a guide file exists a soft two-tone pulse plays in its place.

Any event without files keeps its placeholder (the synthesised siren, drone hum, wind, coin chime) or stays silent. Guns, hits and grunts are the template's own sounds and stay as they are.

## 4. After every render

1. Save the WAVs into `Assets/Audio/<folder>/` with the right names.
2. In Unity: **Vantage → Audio → Refresh Sound Bank.** It fills every event from the files and sets 3D/2D, loop and group. Its log line tells you which events are still empty.
3. Fine-tune an event in `Assets/Audio/VantageSoundBank.asset` (volume, pitch range, min/max distance). The refresh keeps your values; it only replaces the clip list.
4. Press Play. The Console prints `[Vantage] Sound bank: n / 52 events filled (missing: …)`.

## 5. Build the mixer (your part)

Unity can't create a mixer from a script, so you build it once. Then the code finds it by these names.

1. **Create** `Assets/Audio/VantageMixer.mixer` (Project window → right click → Create → Audio Mixer). The name and place matter.
2. **Groups** under Master: `Music`, `Ambience`, `SFX`, `Voice`, `UI`, `Reverb`. Use these exact names.
3. **Reverb:** on `Reverb`, add an **SFX Reverb** effect. On `SFX` and `Voice`, add a **Send** to `Reverb`.
4. **Volume settings:** on `Master`, `Music`, `Ambience`, `SFX` and `Voice`, add an extra **Attenuation** effect. Right-click its *Volume* → *Expose to script*. That way the menu sliders sit on top of your mix: your group faders and the snapshots stay yours.
5. **Exposed parameters:** in the Audio Mixer window → *Exposed Parameters* (top right), rename them exactly like this. The names are the contract with the code:

   | Name | What it is |
   |---|---|
   | `MasterVolume` | the Attenuation volume on Master |
   | `MusicVolume` | the Attenuation volume on Music |
   | `AmbienceVolume` | the Attenuation volume on Ambience |
   | `SfxVolume` | the Attenuation volume on SFX |
   | `VoiceVolume` | the Attenuation volume on Voice |
   | `ReverbRoom` | SFX Reverb → Room (mB) |
   | `ReverbDecay` | SFX Reverb → Decay Time (s) |
   | `ReverbWet` | the Send level from SFX to Reverb (dB). Expose Voice's send too if you want it to follow (it can share the name only if you link them; otherwise leave Voice's send fixed) |

6. **Snapshots:** `Explore` (the default; right-click → *Set as start snapshot*), `Combat` (Ambience group fader about −6 dB), `Cinematic` (SFX lower, Music forward). Snapshots move the **group faders**, not the exposed parameters (a parameter set from script no longer follows snapshots).
7. **Hook it up:** select the `VANTAGE Audio` object in `SampleScene` and drag `VantageMixer` into *Mixer* on `VantageAudio`, or just run **Vantage → Audio → Refresh Sound Bank**: it assigns the mixer when it finds the file. Save the scene.

Without the mixer everything still plays, straight to the output. Only the Master slider works then.

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
