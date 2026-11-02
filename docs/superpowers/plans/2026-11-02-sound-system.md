# VANTAGE Sound System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A data-driven sound layer (sound bank, zones with reverb, adaptive music with stingers, surface footsteps, event hooks) that the user fills with their own REAPER renders and mixes in a mixer they build.

**Architecture:** A `VantageSoundBank` ScriptableObject lists every event; `VantageAudio` (one scene object) plays events by name with pooled sources, routes them to mixer groups by name and falls back to the existing synthesised placeholders. `VantageAudioZones`, `VantageMusic` and `VantageFootsteps` are small consumers; the existing scripts call `VantageAudio` at their moments. An editor tool builds folders, the bank and the scene object, and fills clips by file name.

**Tech Stack:** Unity 6000.0.58f2, URP, built-in audio (AudioSource, AudioMixer), C#; tests are the autoplay bot (`Testing/VantageAutoplayBot.cs`) run in batch mode.

**Spec:** `docs/superpowers/specs/2026-11-02-sound-system-design.md`

## Global Constraints

- Only Unity built-in audio; no FMOD/Wwise.
- Event names exactly as the spec's table (`AMB_Exterior_Dusk`, `FS_Concrete`, …); defined once as constants in `VantageSounds`.
- Mixer group names `Music`, `Ambience`, `SFX`, `Voice`, `UI`, `Reverb`; snapshots `Explore`, `Combat`, `Cinematic`; exposed parameters `MasterVolume`, `MusicVolume`, `SfxVolume`, `VoiceVolume`, `AmbienceVolume`, `ReverbRoom`, `ReverbDecay`, `ReverbWet`.
- No mixer asset → everything plays straight to the output; an empty event → its placeholder or nothing; never an exception or error log.
- No per-frame `Find*`, `GetComponent`, LINQ or allocations; one component per file; use `VantagePhysics` masks; wait frames with `yield return null` (batch mode).
- Camera/cinematic state only through `VantageCinematic` (`CinematicStarted`/`CinematicEnded`).
- CLAUDE.md workflow: Unity closed for batch runs; back up `SampleScene.unity` to `_Backups/` before scene changes; compile offline first (scratch csproj in the session scratchpad, see CLAUDE.md *Workflow* step 3); **no git commits unless the user asks** (each task ends with a WORKLOG note instead).
- Offline compile: `dotnet build <scratchpad>/build/Scratch.csproj -nologo -v q` → `Build succeeded.`
- Bot run: `"C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe" -batchmode -projectPath "<…>\Vantage_First-Person" -executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch -vantageOnly audio -logFile <scratch>\audio.log`, read `[Bot]` lines; exit 0 = passed.

## Review Focus

- A partly filled bank (some events with clips, most without, a loop event with one clip): every system keeps running and the startup log lists exactly the empty events. → Task 1 test `audioBankPlaceholders` runs with two fake clips injected.
- The player standing on a floor boundary (top of a stair flight) must not make the bed flap between two zones every poll. → Task 2 test `audioZonesFollowFloors` holds the player at a stair top for 3 s and expects at most one zone change.
- A fight that stops and starts within the hold time must not drop the combat layer. → Task 3 test checks `Combat` stays true 3 s after the last engagement and is false after 6 s + fade.
- A death reload during combat or during the alarm: music, zones and loops restart cleanly in the new scene with no duplicate loops. → Task 3 test reloads the scene once and counts live music sources (exactly 2).
- Footsteps on the invisible stair surfaces must take the surface of the real steps underneath, not default to concrete everywhere. → Task 4 test samples the outside helipad stairs.

---

### Task 1: Sound bank, player and refresh tool

**Files:**
- Create: `Assets/Vantage/Scripts/VantageSounds.cs` (constants), `Assets/Vantage/Scripts/VantageSoundBank.cs` (ScriptableObject + `SoundEvent`), `Assets/Vantage/Scripts/VantageAudio.cs`, `Assets/Vantage/Editor/VantageAudioSetup.cs`
- Create (by the tool): `Assets/Audio/{Ambience,SFX,Voice,Music,UI}/`, `Assets/Audio/VantageSoundBank.asset`, scene object `VANTAGE Audio`; `Level_World_Design_/Audio_Production/` (empty folder with the guide in Task 7)
- Test: `Assets/Vantage/Scripts/Testing/VantageAutoplayBot.cs` (group `audio`)

**Interfaces:**
- Produces:
  - `public static class VantageSounds` — one `public const string` per spec event (`AmbExterior = "AMB_Exterior_Dusk"`, `AmbL1`, `AmbL2`, `AmbL3`, `AmbHelipad`, `FsConcrete`, `FsMetal`, `FsDirt`, `AlarmSiren`, `ShutterSlam`, `ShutterRoll`, `KeyPickup`, `KeySlotTurn`, `SentryGlint`, `SentryCrack`, `VoContact`, `RobotDeflect`, `RobotServo`, `HeliRotor`, `UiMove`, `UiSelect`, `MusExplore`, `MusCombat`, `StgLockdown`, `StgCleared`, `StgExtraction`, `SentryWarningCall`, `AmbDebris`, `DroneHum`, `CoinCollect`, `PickupWeapon`, `RobotTelegraph`) and `public static readonly string[] All`.
  - `[Serializable] public class SoundEvent { string Name; List<AudioClip> Clips; float Volume = 1; Vector2 Pitch = (0.95, 1.05); float Spatial = 1; float MinDistance = 2; float MaxDistance = 40; bool Loop; string Group = "SFX"; bool Filled => Clips.Count > 0; AudioClip Pick(); }`
  - `public class VantageSoundBank : ScriptableObject { List<SoundEvent> Events; SoundEvent Find(string name); }` (dictionary built lazily once)
  - `public class VantageAudio : MonoBehaviour` with fields `VantageSoundBank Bank`, `AudioMixer Mixer`, and statics:
    `AudioSource Play(string name, Vector3 position, AudioClip fallback = null)`, `AudioSource Play2D(string name, AudioClip fallback = null)`, `AudioSource PlayLoop(string name, Transform follow, AudioClip fallback = null)`, `void Stop(AudioSource loop, float fade = 0f)`, `void SetSnapshot(string name, float seconds)`, `void SetParameter(string name, float value)`, `AudioMixerGroup Group(string name)`, `bool Has(string name)`, `int Plays(string name)` (count since scene load), `VantageAudio Instance`.
  - Editor: `VantageAudioSetup.Refresh()` (menu **Vantage → Audio → Refresh Sound Bank**), `VantageAudioSetup.RefreshBatch()`.

- [ ] **Step 1: Write the failing test** — in the bot add group `case "audio":` running `audioBankPlaceholders()`:
  - `VantageAudio.Instance != null` and `Instance.Bank != null` and the bank has an event for every name in `VantageSounds.All`;
  - inject two fake clips (`AudioClip.Create`, 0.2 s) into `FS_Concrete`, call `VantageAudio.Play(VantageSounds.FsConcrete, pos)` 5 times and `VantageAudio.Play2D(VantageSounds.UiSelect)` (empty event, no fallback) once; assert `Plays(FsConcrete) == 5`, the returned source is non-null for the filled event and null for the empty one, and no `[Vantage]`/Unity error was logged (hook `Application.logMessageReceived`, count `LogType.Error`/`Exception`);
  - `PlayLoop(VantageSounds.AmbExterior, null, VantageProceduralAudio.Create(Wind, 1))` returns a playing looping source; `Stop` it.
  - Log `PASS audio bank: …` or `FAIL audio bank: …`.
- [ ] **Step 2: Run offline compile** — expected: FAIL, `VantageAudio`/`VantageSounds` not defined.
- [ ] **Step 3: Implement** the four files. `VantageAudio`: 16 pooled one-shot `AudioSource`s under its object (round robin, skip ones still playing when possible); loops get their own child source each (following `follow` in `LateUpdate` through a cached list); settings from the event (volume, random pitch in range, `spatialBlend = Spatial`, `rolloffMode = Logarithmic`, min/max distance, `outputAudioMixerGroup = Group(evt.Group)`); groups and snapshots looked up once by name and cached (`Mixer.FindMatchingGroups`, `Mixer.FindSnapshot`), all calls no-ops when `Mixer == null`. Startup log: `[Vantage] Sound bank: n / m events filled (missing: …)`. `VantageAudioSetup.Refresh`: create folders, create/load the bank, add missing events with the spec's defaults per event (group, 2D/3D, loop, distances from the spec table), keep existing settings, set `Clips` from `Assets/Audio/**` WAVs whose file name is the event name or `EVENT_NN`, find `Assets/Audio/VantageMixer.mixer` if present, create/update scene object `VANTAGE Audio` with `VantageAudio` (bank, mixer), log `Sound bank refreshed: n / m filled`. Back up the scene first.
- [ ] **Step 4: Run** compile → `Build succeeded.`; `RefreshBatch` (scene saved) → log line present; bot `-vantageOnly audio` → `PASS audio bank`.
- [ ] **Step 5: WORKLOG note** (no commit).

### Task 2: Floor zones and reverb

**Files:**
- Create: `Assets/Vantage/Scripts/VantageAudioZones.cs`
- Modify: `Assets/Vantage/Editor/VantageAudioSetup.cs` (adds the component to `VANTAGE Audio` with the starting presets)
- Test: bot `audioZonesFollowFloors()` in group `audio`

**Interfaces:**
- Consumes: `VantageAudio.PlayLoop/Stop/SetParameter`, `VantageTowerLevels.LevelAt`, `VantageEvents.ActivePlayer()`.
- Produces: `public class VantageAudioZones : MonoBehaviour { [Serializable] class Zone { string Name; string Bed; float Room; float Decay; float Wet; } List<Zone> Zones; float Fade = 2f; float Poll = 0.25f; float Settle = 0.75f; int Current { get; } string ActiveBed { get; } int Changes { get; } }` — zone index 0 = outside, 1–4 = `LevelAt(player) + 1` (L1, L2, L3, Helipad).
- Starting presets (`Room` mB, `Decay` s, `Wet` dB send): Outside −3000 / 0.6 / −20; L1 −1200 / 0.8 / −10; L2 −800 / 1.8 / −6; L3 −1500 / 1.1 / −12; Helipad −6000 / 0.3 / −30.

- [ ] **Step 1: Write the failing test** `audioZonesFollowFloors`: teleport the player outside (15 m in front of the entrance), then to the floor of L1, L2, L3 (`floorAhead()` points) and onto `FINAL_Helipad`, waiting 3 s each; assert `ActiveBed` is `AMB_Exterior_Dusk`, `AMB_L1_Interior`, `AMB_L2_Armory`, `AMB_L3_Destroyed`, `AMB_Helipad` in turn. Then teleport to the top step of the L1→L2 flight and stand 3 s: `Changes` grows by at most 1. With a fake clip in `AMB_Debris`, 20 s on L3 → `Plays(AmbDebris) >= 1`.
- [ ] **Step 2: Run** compile → FAIL (`VantageAudioZones` missing).
- [ ] **Step 3: Implement**: poll every `Poll` s; a new zone is accepted only after it has held for `Settle` s (no flapping on a boundary); on change `PlayLoop` the new bed at volume 0 and fade in over `Fade` while the old one fades out and stops; ease `ReverbRoom`, `ReverbDecay`, `ReverbWet` to the zone's preset over `Fade` via `SetParameter`. Exterior bed fallback: `VantageProceduralAudio.Create(Kind.Wind, …)`; other beds: none. While in L3, if `AMB_Debris` is filled, play it every 6–14 s (random) at a random point 6–15 m from the player on the L3 floor height (P2).
- [ ] **Step 4: Run** compile, `RefreshBatch`, bot → `PASS audio zones`.
- [ ] **Step 5: WORKLOG note.**

### Task 3: Music layers, combat state, stingers, snapshots

**Files:**
- Create: `Assets/Vantage/Scripts/VantageMusic.cs`
- Modify: `Assets/Vantage/Scripts/VantageTowerLevels.cs:11-14` (`IVantageEnemy`) to add `bool Engaging { get; }`; `VantageDrone.cs`, `VantageHeavyRobot.cs` implement it with the condition they already use before firing at the player; `VantageAudioSetup.cs` adds the component.
- Test: bot `audioMusic()` in group `audio`

**Interfaces:**
- Consumes: `VantageAudio`, `VantageEvents.AlarmRaised`, `VantageTowerLevels.Instance.LevelCleared` / `BossDefeated` / `BossSpawned`, `VantageAlarm.Instance.Active`, `VantageCinematic.CinematicStarted/Ended`, `VantageEvents.GameWon`.
- Produces: `public class VantageMusic : MonoBehaviour { float CombatFade = 2.5f; float CombatHold = 6f; float StingerDip = -8f; bool Combat { get; } bool Ended { get; } }`; snapshot changes `Explore`/`Combat`/`Cinematic` through `VantageAudio.SetSnapshot` (1.5 s).

- [ ] **Step 1: Write the failing test** `audioMusic`:
  - at start two music sources playing (`MUS_Explore`, `MUS_Combat`), started on the same `dspTime` (store it; compare `timeSamples` within 1 % of the clip with fake equal-length clips injected), combat volume 0;
  - order one soldier of the current level to attack the player (`FighterBrain.ToAttack`) → within 1 s `Combat == true`; stop it (kill the soldier) → still `true` after 3 s, `false` after `CombatHold + CombatFade + 0.5` s;
  - raise the alarm → `Plays(StgLockdown) == 1`; clear the level (`clearLevel`) → `Plays(StgCleared) >= 1`;
  - reload the scene once (as the checkpoint check does) → exactly 2 live music sources in the new scene.
  - (The boss/extraction part is checked in the full run's `finale`: after `BossDefeated`, `Plays(StgExtraction) == 1` and, 5 s after `GameWon`, `Ended == true` and the explore source stopped.)
- [ ] **Step 2: Run** compile → FAIL.
- [ ] **Step 3: Implement**: `PlayScheduled` both layers at `AudioSettings.dspTime + 0.2`; combat test every 0.25 s over cached lists (`VantageTowerLevels` enemies' `FighterBrain` and `IVantageEnemy`), alarm, boss alive; hold and fade as specified; stingers via `Play2D`, layers dipped by `StingerDip` dB for the stinger's length; `CinematicStarted` → `Cinematic` snapshot, combat layer forced 0; `CinematicEnded` → `Explore`/`Combat`; after the extraction stinger the explore layer fades out (4 s) and `Ended` = true. Wire `STG_Extraction` and the `Ended` assertions into the bot's existing `finale` check.
- [ ] **Step 4: Run** compile, `RefreshBatch`, bot `audio` → `PASS audio music`.
- [ ] **Step 5: WORKLOG note.**

### Task 4: Footsteps by surface

**Files:**
- Create: `Assets/Vantage/Scripts/VantageFootsteps.cs`
- Modify: `Assets/Vantage/Editor/VantageAudioSetup.cs` (adds `VantageFootsteps` to `Assets/Vantage/Prefabs/Third Person Rig.prefab`), `Assets/Vantage/Editor/VantageTower.cs` (adds it when Setup builds `Soldier.prefab`)
- Test: bot `audioFootsteps()` in group `audio`

**Interfaces:**
- Consumes: `VantageAudio.Play`, `CoverShooter.ICharacterPhysicsListener` (the motor collects listeners in `Awake`, so the component must be on the prefabs, not added at runtime).
- Produces: `public class VantageFootsteps : MonoBehaviour, ICharacterPhysicsListener { static string SurfaceAt(Vector3 foot); }` returning `VantageSounds.FsDirt|FsMetal|FsConcrete`.

- [ ] **Step 1: Write the failing test** `audioFootsteps`: `SurfaceAt` at a terrain point 15 m in front of the entrance → `FS_Dirt`; at the L1 floor in front of the shutter → `FS_Concrete`; at the middle of the outside helipad stairs (`FINAL_Landing_Stairs`) → `FS_Metal`. Walk the player 3 s outside: `Plays(FsDirt) > 0` and the player's `CharacterSounds.Footstep` is empty.
- [ ] **Step 2: Run** compile → FAIL.
- [ ] **Step 3: Implement**: `SurfaceAt` raycasts down 1.5 m with `VantagePhysics.Solid` (non-alloc buffer), takes the first hit that is a `TerrainCollider` (dirt) or has an enabled `Renderer` (skips the invisible stair surfaces), dirt for terrain, metal when the renderer's shared material name contains `Metal`, `Steel`, `Grate` or `Rail` (case-insensitive), concrete otherwise. `OnFootstep(position)` plays the surface event at the foot; `Awake` clears this character's `CharacterSounds.Footstep`. Before relying on the keywords, run `VantageTower.ReportBatch` and check the material name of `FINAL_Landing_Stairs`; if it has none of the keywords, add its material's name stem to the keyword list and note it in CLAUDE.md.
- [ ] **Step 4: Run** compile, `RefreshBatch`, `VantageTower.SetupBatch` (soldier prefab), bot `audio` → `PASS audio footsteps`.
- [ ] **Step 5: WORKLOG note.**

### Task 5: Event hooks

**Files (Modify):** `VantageAlarm.cs` (siren loop on the beacons instead of the `Siren` object's synth when the event is filled; `SHUTTER_Slam` on close), `VANTAGE_Tower/Scripts/VantageDoor.cs` (`SHUTTER_Roll` in `OpenShutter` and when a shutter/gate starts rolling up; mark `// VANTAGE change`), `VantageFloorGate.cs` (`SHUTTER_Roll` in `Open`), `VantageKeyItem.cs` (`KEY_Pickup`), `VantageKeySlot.cs` (`KEYSLOT_Turn` in `used`), `VantageSentry.cs` (`SENTRY_Glint` when a glint starts, `SENTRY_Crack` in `shotEffects`, `SENTRY_WarningCall` replacing `WarningCall` when filled), `VantageHeavyRobot.cs` (`ROBOT_Deflect` on each pistol bounce, `ROBOT_Servo` loop while the agent moves, `ROBOT_Telegraph` in the charge-up), `VantageEnemyAggression.cs` (`VO_Contact` on first engagement, one bark per 3 s tower-wide), `VantageExtraction.cs` and `VantageMainMenu.cs` (`HELI_Rotor` loop following the helicopter; `UI_Move`/`UI_Select` on select/act), `VantageDrone.cs` (`DRONE_Hum` replacing the synth hum when filled), `VantageCoin.cs` (`COIN_Collect`, fallback the chime), `WeaponPickup.cs` (`PICKUP_Weapon`, fallback `PickupSound`).
- Existing `AudioClip` fields stay as fallbacks (passed as `fallback`).
- Test: bot `audioEvents()` in group `audio`

- [ ] **Step 1: Write the failing test** `audioEvents`: with two fake clips injected into every event, trigger each moment through existing bot helpers (spring the trap, pick up the key, use the slot, open the floor gate, a sentry ring shot in the warning band, a pistol hit on the robot, one soldier engaging, the title screen moving and selecting, coin collect); assert `Plays(event) >= 1` for `ALARM_Siren`, `SHUTTER_Slam`, `SHUTTER_Roll`, `KEY_Pickup`, `KEYSLOT_Turn`, `SENTRY_Glint`, `SENTRY_Crack`, `ROBOT_Deflect`, `VO_Contact`, `UI_Move`, `UI_Select`, `COIN_Collect`, and that two soldiers engaging within 1 s produce exactly one `VO_Contact`.
- [ ] **Step 2: Run** compile → the test compiles but FAILS (counts 0).
- [ ] **Step 3: Implement** the hooks listed above, each one line where possible (`VantageAudio.Play(VantageSounds.X, position, existingFallback)`); loops stored and stopped on death/disable.
- [ ] **Step 4: Run** compile, bot `audio` → `PASS audio events`; then the full suite (all groups) → no new failures.
- [ ] **Step 5: WORKLOG note.**

### Task 6: Volume settings and template routing

**Files (Modify):** `VantageMainMenu.cs` (settings rows Master / Music / Effects replace Volume; PlayerPrefs `vantage.master`, `vantage.music`, `vantage.effects` (0–1, step 0.1); applied through `VantageAudio.SetParameter` as dB `20·log10(max(v, 0.0001))`; Effects sets `SfxVolume`, `VoiceVolume`, `AmbienceVolume`; without a mixer, Master falls back to `AudioListener.volume`), `VantageAudio.cs` (on scene start route every template `GunSounds`/`HitSounds` source group to `SFX` and `CharacterSounds` to `Voice`, through a cached scan once per scene and for spawned enemies via `VantageTowerLevels` spawn).
- Test: bot extends `titleScreen()`.

- [ ] **Step 1: Write the failing test**: in `titleScreen()`, open Settings, lower Master once (call the row's action): `PlayerPrefs.GetFloat("vantage.master") == 0.9f`; restore it; the settings page shows 6 rows (Master, Music, Effects, Mouse sensitivity, Graphics, Fullscreen) plus Back.
- [ ] **Step 2: Run** compile → FAIL.
- [ ] **Step 3: Implement** as above; the old `vantage.volume` key is read once as the initial Master value.
- [ ] **Step 4: Run** compile, bot `-vantageOnly intro` → `PASS title screen…`.
- [ ] **Step 5: WORKLOG note.**

### Task 7: The user's guide, docs, final verification

**Files:**
- Create: `Level_World_Design_/Audio_Production/README.md` — the user's step-by-step guide: REAPER render settings (WAV 48 kHz / 24-bit, mono for 3D, stereo for beds/music, loops cut on zero crossings, music layers same tempo/length/key), naming (`EVENT_NN`), the sound list (copy of spec §1 table with P1/P2), building `VantageMixer` (groups, Reverb send with an SFX Reverb effect, the three snapshots, exposing the eight parameters with these exact names), tuning the zone presets on `VANTAGE Audio`, combat fade/hold fields, loudness check (−23 LUFS integrated, −1 dBTP, measured on a capture), and pressing **Vantage → Audio → Refresh Sound Bank** after each render. Marks which parts are the user's to present.
- Create: `PaperDocumentation-/Sound-design-desc/VANTAGE_Sound_Design_Update_DRAFT.md` — the SOUNDCREA description text updated to the current game (third person, concrete three-floor tower with helipad at dusk, Unity 6000.0.58f2, the new events and zones, music two layers + three stingers), for the user to edit.
- Modify: `CLAUDE.md` (code map rows, editor tool row, rules: event names = file names, mixer names are the contract, footsteps component must be on prefabs, re-run table row "new WAVs → Refresh Sound Bank"), `README.md` (sound section, settings), `VANTAGE — Level & World Design Concept.md` (Revisions row), `WORKLOG.md`.

- [ ] **Step 1: Write** the two documents and the doc updates.
- [ ] **Step 2: Run** the full bot suite (no `-vantageOnly`) → `ALL PASSED` (or report failures with their lines).
- [ ] **Step 3: Report** to the user: what's verified, what to check by ear in Play mode, and their next steps (render the first P1 sounds, build the mixer by the guide).
