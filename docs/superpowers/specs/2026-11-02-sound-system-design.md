# VANTAGE sound system: design

2026-11-02 · approved in conversation with Adhurim Thaqi (sections 1–4)

## Purpose

The complete sound layer of VANTAGE, which is also the user's SOUNDCREA project (I.BA_SOUNDCREA.H2601, final presentation 16.12). The user designs and produces the sounds in REAPER and builds the mixer, the reverb presets and the final mix; Claude builds the Unity systems and the hooks, explains them, and writes the user a step-by-step guide for the user's parts (split "B": the user can present the parts they built, and declares Claude's help as their course requires).

**Success:**
- Every sound moment of the level has an event: the floors sound different, footsteps follow the surface, fights bring in the combat layer, the stingers mark the pacing curve's peaks, and the end is silent.
- Sounds are swapped without code: render to `Assets/Audio/`, press one menu item.
- The game is never silent while the user works (placeholders), and an empty or partial bank causes no errors.
- Only Unity's built-in audio (AudioSource, AudioMixer), as the SOUNDCREA description promised.
- In-game mix to about −23 LUFS integrated, −1 dBTP.

**Not in scope:** new gun, reload, hurt or death sounds (the template's stay for now; replace before a commercial release), FMOD/Wwise, a full score.

## 1. Folders, naming, the sound list

```
Level_World_Design_/Audio_Production/      REAPER projects and raw recordings (outside Unity)
Vantage_First-Person/Assets/Audio/
    Ambience/  SFX/  Voice/  Music/  UI/   rendered WAVs, 48 kHz / 24-bit (3D sources mono, beds and music stereo)
    VantageSoundBank.asset                 every event (made by the refresh tool)
    VantageMixer.mixer                     made by the user (no scripting API creates mixers)
```

File names are `EVENT_NN.wav` (e.g. `FS_Concrete_03.wav`); a single file may drop the number. The refresh tool matches files to events by name.

| Event | Files | Use | Group | 2D/3D | P |
|---|---|---|---|---|---|
| `AMB_Exterior_Dusk` | 1 loop | evening wind, distant birds, far generator | Ambience | 2D | 1 |
| `AMB_L1_Interior` | 1 loop | ventilation, fluorescent buzz | Ambience | 2D | 1 |
| `AMB_L2_Armory` | 1 loop | big hall, metal ticks, faint radio | Ambience | 2D | 1 |
| `AMB_L3_Destroyed` | 1 loop | wind through the breach, tarp | Ambience | 2D | 1 |
| `AMB_Helipad` | 1 loop | strong open wind | Ambience | 2D | 1 |
| `FS_Concrete`, `FS_Metal`, `FS_Dirt` | 4 each | footsteps by surface, player and soldiers | SFX | 3D | 1 |
| `ALARM_Siren` | 1 loop | the lockdown siren | SFX | 3D (beacons) | 1 |
| `SHUTTER_Slam`, `SHUTTER_Roll` | 1 + 1 | the trap shutter drops; shutter and gates roll up | SFX | 3D | 1 |
| `KEY_Pickup`, `KEYSLOT_Turn` | 1 + 1 | key grabbed; key turned in the slot | SFX | 3D | 1 |
| `SENTRY_Glint`, `SENTRY_Crack` | 1 + 2 | warning tick with the red glint; the distant shot | SFX | 2D tick, 3D crack | 1 |
| `VO_Contact` | 3 | a soldier's bark when it wakes | Voice | 3D | 1 |
| `ROBOT_Deflect` | 2 | pistol round bouncing off the robot/boss | SFX | 3D | 1 |
| `ROBOT_Servo` | 1 loop | robot and boss moving (boss pitched down) | SFX | 3D | 1 |
| `HELI_Rotor` | 1 loop | menu and extraction helicopter | SFX | 3D | 1 |
| `UI_Move`, `UI_Select` | 1 + 1 | title screen | UI | 2D | 1 |
| `MUS_Explore`, `MUS_Combat` | 1 + 1 loop | drone; percussion layer (same tempo, key, length) | Music | 2D | 1 |
| `STG_Lockdown`, `STG_Cleared`, `STG_Extraction` | 1 each | stingers | Music | 2D | 1 |
| `SENTRY_WarningCall` | 2 | "Back to the tower!" in the warning band | Voice | 3D | 2 |
| `AMB_Debris` | 3 | random crumbles on L3 | Ambience | 3D | 2 |
| `DRONE_Hum`, `COIN_Collect`, `PICKUP_Weapon`, `ROBOT_Telegraph` | 1 each | replace synthesised placeholders | SFX | 3D | 2 |

P1 is about 30 files (the SOUNDCREA demo); P2 is extra.

## 2. Mixer and zones

**Mixer (built by the user, by the guide):**
```
Master
├── Music
├── Ambience
├── SFX        → send to Reverb
├── Voice      → send to Reverb
├── UI
└── Reverb     (SFX Reverb effect)
```
- Snapshots: `Explore` (default), `Combat` (Ambience −6 dB), `Cinematic` (title screen, intro, extraction: SFX lower, Music forward). Blends of 1–2 s.
- Exposed parameters (the names are the contract with the code), all in dB except the reverb: `MasterVolume`, `MusicVolume`, `SfxVolume`, `VoiceVolume`, `AmbienceVolume`, `ReverbRoom`, `ReverbDecay`, `ReverbWet`. The settings menu's Master slider drives `MasterVolume`, Music drives `MusicVolume`, and Effects drives `SfxVolume`, `VoiceVolume` and `AmbienceVolume` together. Group fader levels stay the user's mix; the sliders are offsets on top.
- The template's guns and grunts are routed to SFX and Voice at runtime (their AudioSources get the group).
- Without a mixer asset every source plays to the output directly; nothing fails.

**Zones (`VantageAudioZones`):** every 0.25 s the player's zone from `VantageTowerLevels.LevelAt`: outside (−1), L1, L2, L3, helipad (the finale's index). On a change the bed crossfades (2 s, two 2D looping sources) and the reverb parameters ease to the zone's preset over the same time.

| Zone | Bed | Reverb starting values (user tunes) |
|---|---|---|
| Outside | `AMB_Exterior_Dusk` | room low, decay 0.6 s, wet low |
| L1 | `AMB_L1_Interior` | small room, decay 0.8 s |
| L2 | `AMB_L2_Armory` | hall, decay 1.8 s |
| L3 | `AMB_L3_Destroyed` | medium, decay 1.1 s, wet lower |
| Helipad | `AMB_Helipad` | almost dry |

Presets are serialized fields on `VantageAudioZones` (one row per zone). 3D events use logarithmic rolloff with per-event min/max distance in the bank.

## 3. Music and events

**`VantageMusic`:** `MUS_Explore` and `MUS_Combat` start together with `PlayScheduled` on the same DSP time and loop forever; the combat layer's volume fades 0↔1 (2.5 s, a field).
- Combat when: any live level soldier's `FighterBrain.Threat` is the player, a live drone, turret or robot of the current level is engaging the player (the same test those components use before firing), the alarm is active, or the boss is alive. Explore after 6 s (a field) without any of these.
- Title screen and intro: drone only, `Cinematic` snapshot.
- Stingers: `STG_Lockdown` on `VantageEvents.AlarmRaised`, `STG_Cleared` on `VantageTowerLevels.LevelCleared`, `STG_Extraction` on `BossDefeated`; the layers dip (−8 dB) for the stinger's length.
- After the extraction stinger the drone fades out for good: the end screen has only wind and the rotor.

**Events:**
- `VantageFootsteps` on the player and on soldiers takes the template's footstep callback (`CharacterSounds.OnFootstep`, whose own clips are cleared at runtime): a ray down picks the surface (TerrainCollider → `FS_Dirt`; a renderer material whose name contains Metal, Steel, Grate or Rail → `FS_Metal`; else `FS_Concrete`).
- `VO_Contact` when `VantageEnemyAggression` wakes (first engagement), at most one bark per 3 s across the tower.
- `SENTRY_Glint` when a glint starts and `SENTRY_Crack` with each shot, for ring shots and the intro's scripted shots alike (the intro teaches the cue); `SENTRY_WarningCall` (P2) on entering the warning band.
- `ROBOT_Deflect` on each pistol bounce (`VantageHeavyRobot`), `ROBOT_Servo` while its agent moves.
- `ALARM_Siren` (replaces the synth on the beacons), `SHUTTER_Slam` (alarm closes the shutter), `SHUTTER_Roll` (`VantageDoor` shutter/gates opening, `VantageFloorGate.Open`), `KEY_Pickup` (`VantageKeyItem`), `KEYSLOT_Turn` (`VantageKeySlot`), `HELI_Rotor` (menu and extraction helicopters), `UI_Move`/`UI_Select` (`VantageMainMenu`).
- An empty event plays the existing synthesised sound where there is one (siren, drone hum, wind for the exterior bed, coin chime, whoosh), otherwise nothing. At scene start: `[Vantage] Sound bank: n / m events filled (missing: …)`.

## 4. Components

| Unit | Kind | Does | Depends on |
|---|---|---|---|
| `VantageSoundBank` | ScriptableObject | list of events: name, clips, volume, pitch range, spatial blend, min/max distance, loop, group name | — |
| `VantageAudio` | runtime, static facade + one scene object | `Play(event, position)`, `PlayLoop(event, follow) → handle`, `Stop(handle)`; pooled sources (16), random clip/pitch/volume, mixer group by name (`FindMatchingGroups`), placeholder fallback; `SetSnapshot`, `SetFloat` wrappers | bank, mixer (optional) |
| `VantageAudioZones` | runtime | zone polling, bed crossfade, reverb presets | `VantageAudio`, `VantageTowerLevels` |
| `VantageMusic` | runtime | layers, combat state, stingers, ending | `VantageAudio`, level brain events |
| `VantageFootsteps` | runtime, on characters | surface detection, footstep events | `VantageAudio` |
| `VantageAudioSetup` | editor | **Vantage → Audio → Refresh Sound Bank** (creates folders and the bank, fills clips by file name, logs filled/missing); places the scene object `VANTAGE Audio` with the bank, the mixer if found, zones and music | — |
| Hooks | edits | the existing scripts call `VantageAudio.Play` at their moments; menu settings get Master/Music/Effects | `VantageAudio` |

Rules kept: no per-frame `Find`/`GetComponent`/allocations (sources pooled, components cached), one component per file, `VantageCinematic` decides cinematic state (`CinematicStarted/Ended` → snapshot), batch-safe coroutines.

## 5. Testing

Autoplay bot (group `-vantageOnly audio`, and in the full run):
- the bank loads; an empty bank plays without errors (placeholders);
- zones: teleporting outside → L1 → L2 → L3 → helipad switches the active bed each time;
- music: a soldier engaging turns combat on; it turns off after the hold; stingers fire on the alarm, a level clear and the boss's death (counted through a debug counter);
- footsteps: surface picked at sample points (outside dirt, L1 floor concrete, the outside stairs metal);
- the end screen leaves the music stopped.
The bot cannot hear: loudness, balance and feel are checked by the user in Play mode and with a loudness meter on a capture.

## 6. Docs and deliverables

- `Audio_Production/README.md`: the user's guide (REAPER render settings, naming, the mixer build with exposed parameter names, snapshots, reverb tuning, loudness check, refresh).
- CLAUDE.md (code map, rules), README (sound section), concept doc (Revisions row), WORKLOG.
- A draft update of the SOUNDCREA description text to the current game (concrete tower at dusk, third person, the new events), for the user to edit.
- The helicopter model (`Raptor3D/MSH_N2`) swap is a separate task done after this design.
