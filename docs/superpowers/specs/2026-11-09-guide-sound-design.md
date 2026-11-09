# Guide sound and menu music: design

**Date:** 2026-11-09 · **Status:** approved in chat, spec awaiting review
**Why:** teacher's feedback: each floor should *sound* like where the player needs to go. The floors already have their own ambience beds (`VantageAudioZones`), but those play everywhere and give no direction. This adds a **wayfinding layer**: one 3D sound at the current goal. It also adds title-screen music (`MUS_Menu`), replacing the idea of using a copyrighted theme.

## Goals and non-goals

- **Goal:** at any moment at most **one** guide sound plays, from the object the player has to reach next, and only on that object's floor. It is diegetic (a PA speaker, a radio, a phone, a beeping slot, wind, helicopter radio), not an abstract beep.
- **Goal:** the title screen has its own music, which fades out on Play before the level music starts.
- **Goal:** works today without WAVs (synthesised fallback), so the routing can be playtested now; the user's REAPER WAVs drop in by file name.
- **Non-goal:** layered story ambience per room (option B in chat): later, if there is time.
- **Non-goal:** guide sounds during fights (L3 indoor soldiers, open-area patrols, boss): the enemies' own noise leads there.
- **Non-goal:** changing the HUD's objective hint logic.

## The goal ladder

Evaluated top to bottom; the first step whose condition holds is the current goal. "Level" is `VantageTowerLevels.Current`; L1 = 0, L2 = 1, L3 = 2, finale = 3.

| # | Step | Condition | Anchor (Blender name) | Event |
|---|---|---|---|---|
| 1 | L1 lockdown | `VantageAlarm.Active` | `MK_Turret_Hallway` (end of A → B → C → West Wing route) | `GUIDE_PA` |
| 2 | L1 cleared: go up | Current == 1, player on L1 | the L1→L2 `VantageFloorGate` | `GUIDE_Stairs` |
| 3 | Find the rifle | Current == 1, rifle not unlocked in `VantageArsenal` | `L2_PICKUP_Gun` | `GUIDE_Radio` |
| 4 | Find the key | Current == 1, `!VantageInventory.Has("Key")`, key not inserted | `L2_PICKUP_Key` | `GUIDE_Phone` |
| 5 | Use the key | Current == 1, has the key, key slot not used | `L2_KEYSLOT_InsertKey` | `KEYSLOT_Beep` |
| 6 | L2 gate open: go up | `L2_GATE_Stairwell_LOCKED` open and Current == 2 with the player still on L2, or Current == 1 with the gate open | `L2_GATE_Stairwell_LOCKED` | `GUIDE_Stairs` |
| 7 | Robot room open | Current == 2, `L3_DOOR_RobotRoom_LOCKED` open, `TRIGGER_L3_OpenArea_Enter` not yet entered | `LVL3_BreachEdges` | `GUIDE_Wind` |
| 8 | L3 cleared: exit | `L3_DOOR_Exit_To_Final` open and the player still below the finale | `L3_DOOR_Exit_To_Final` | `GUIDE_Stairs` |
| 9 | Reach the pad | Current == 3, `!BossSpawned` | `FINAL_Helipad` (top of its renderers' bounds) | `GUIDE_HeliRadio` |
| — | otherwise | — | — | silence |

Additional rules:
- **Nothing before L1:** outside and before the alarm, the intro's pistol banner and the open entrance do the work.
- **Same floor only:** the sound plays only while `LevelAt(player) == LevelAt(anchor)` (stair anchors count for the floor below them). Otherwise it fades out; the step stays current.
- **Silent in cinematics** (`VantageCinematic.Active`) and while the player is dead.
- **Not on the title screen** (`VantageMainMenu.Showing`).
- The exact state queries (rifle unlocked, key inserted, door open, trigger entered) use the existing public state (`VantageArsenal`, `VantageInventory`, `VantageDoor.Find(name).IsLocked`, `VantageFloorGate.IsOpen`, `VantageKeySlot`). Where a state isn't public yet (key inserted, open area entered), add a read-only property; no behaviour change.

## Components

### `VantageGoalSound` (new, `Assets/Vantage/Scripts/`, on `VANTAGE Audio`)
- Serialized `Steps` list: `Name`, `Event`, `Anchor` (Transform, set by Setup). Conditions are code (one `switch` over the step index), so the order is reviewable in one place.
- Polls every 0.25 s (no per-frame lookups; references cached at start and after the level brain changes level).
- On a change of step: fades the current loop out (1 s) and starts the new one at the anchor with `VantageAudio.PlayLoop(event, anchor, fallback)`. One source at a time.
- Floor gate: fades the volume to 0 (0.5 s) off-floor, back up on-floor; the loop keeps running so it doesn't restart on every stair step.
- Public for the bot: `Current` (step index, −1 = none), `CurrentName`, `Audible` (on-floor and not silenced), `Anchor`, `Changes` (count).
- Fallback while the event has no clip: a soft synthesised two-tone pulse from `VantageProceduralAudio` (new method `GuidePulse()`), 3D at the anchor.

### `VantageSounds` / sound bank
- New constants: `GUIDE_PA`, `GUIDE_Stairs`, `GUIDE_Radio`, `GUIDE_Phone`, `KEYSLOT_Beep`, `GUIDE_Wind`, `GUIDE_HeliRadio`, `MUS_Menu` (32 → 40 events).
- `VantageAudioSetup` defaults: guide events are 3D loops in the `SFX` group (`GUIDE_Wind` in `Ambience`), min distance 2 m, max 30 m, volume 0.8; `MUS_Menu` a 2D loop in `Music`. The user's tuned values are kept on refresh, as now.

### Menu music
- `VantageMainMenu` starts `MUS_Menu` (2D loop) when it shows, fades it out over 1.5 s on Play.
- `VantageMusic` waits while `VantageMainMenu.Showing` and schedules `MUS_Explore` / `MUS_Combat` after it hides (still sample-locked to one DSP time). After a death reload the menu doesn't show, so the music starts as today.

### Setup (`VantageTower`)
- Adds `VantageGoalSound` to `VANTAGE Audio` if missing and fills the anchors by Blender name. Logs `Guide sound: n / 9 anchors (missing: …)`; a missing anchor leaves that step silent (FAIL line in the log, as with the sentry routes).

## Testing (autoplay bot)

New group `-vantageOnly guide` (also in the full run):
1. Title screen: `MUS_Menu` requested while showing; faded and stopped within 2 s of Play; `MUS_Explore` starts only after.
2. Walk the ladder with the existing bot steps (alarm, L1 clear, rifle, key, slot, gate, robot room, exit, pad): at each, `Current` is the expected step, the source sits within 1 m of the anchor, exactly one guide source is playing.
3. Changes on completion: each step ends within 0.5 s after its condition clears.
4. Floor gating: on L2's step 3, teleport the player to L1 → `Audible` false; back → true.
5. Silent in the intro and on the death screen.
6. Fallback: with the bank's guide events empty, the pulse plays (no null source).

The full autoplay run must still pass.

## Documentation

- `Audio_Production/README.md`: the 8 new WAV names with a brief per sound (character, loop length, loudness target) for REAPER.
- `CLAUDE.md` (code map, sound rules, event count 40), `README.md` (sound section), concept doc *Revisions* row: "Sound as a wayfinding layer (teacher feedback 2026-11-09): one diegetic goal sound at a time".
- `WORKLOG.md` entry.

## Risks

- **Hearing through floors:** Unity audio has no occlusion; the same-floor rule handles it. Check by ear in Play mode at the stairs.
- **Stair anchors** are at the floor boundary: `LevelAt` with the stair lead decides the floor; the bot's floor-gating test covers it.
- **Too loud or too quiet** next to combat music: tuned in the bank after the WAVs exist (listening checklist item).
