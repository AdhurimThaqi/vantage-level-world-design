# VANTAGE work log

Every request and what was done about it, **newest first**. Claude sessions and the `vantage-watchdog` agent add an entry after every task (see *Work log* in `CLAUDE.md`). `CLAUDE.md` describes how the project is now; this file records how it got there.

Entry format:

```
## YYYY-MM-DD: short title (who: Claude session / vantage-watchdog)
**Asked:** the user's request, in their words where possible.
**Done:** what changed: files, tools run, decisions and why.
**Verified:** what was checked and the result (bot PASS/FAIL, screenshots, compile), or "not verified".
**Open:** what is left, what the user should check in Play mode.
```

---

## 2026-10-14: New tower built: 3 levels + helipad finale (Claude session)
**Asked:** "looks good start building please" / "Lets start building please" (spec docs/superpowers/specs/2026-10-14-vantage-tower-3-levels-design.md, plan docs/superpowers/plans/2026-10-14-vantage-tower-3-levels.md, native execution).
**Done:**
- `VANTAGE_Tower` placed where the old tower stood (entrance flush with the terrain); old `CommandTower` + `VantageCommandTower.cs` removed (tower art moved to `_UnusedAssets`).
- New `Editor/VantageTower.cs` (Set Up Tower / SetupBatch / ReportBatch): marker renderers off, static flags fixed, door rules (`ClearLevel`), doors out of the NavMesh with obstacles, stair surfaces from a height map of the steps (4 flights), L1→L2 red shutter, covers per prop mesh island (236), pistol outside the entrance, rifle at the Armory gun, key + key slot, alarm (beacons, 4 red lights, siren), security camera, placeholders, soldier prefab, level manager (4 levels, areas, arrivals), extraction, door check, NavMesh, 5 coins.
- Tower package runtime scripts edited (`// VANTAGE change`): VantageDoor (doors open for any living actor, event-driven KILL lists, ClearLevel, ForceOpen, obstacles), VantageInteract (no OnGUI; HUD prompt; TryUse), VantagePickup, VantageTrigger, VantageLevelManager (bridge).
- `VantageTowerLevels` rewritten (prefix-based levels, enemies named after markers, KillListDone, Alert, StartHunt, checkpoint restore, boss + BossDefeated, door sleeping); new `VantageRun`, `VantageAlarm`, `VantageSecurityCamera`, `VantageKeyItem`, `VantageKeySlot`, `VantageHeavyRobot`, `VantageCoin`, `VantageExtraction`, `Editor/VantagePlaceholders`; HUD (prompt, messages, coins, goal hints, "Level n / 3", end screen); siren sound; logger fixed (session time per session, coins, alarm); soldiers can be alerted and patrol.
- World rebuilt around the new plot (plot levelled at the ground floor), minimap and occlusion baked.
- Docs: CLAUDE.md, README.md, concept doc (new "current level" section + Revisions row), watchdog agent.
**Verified:** dotnet build OK; SetupBatch exit 0 (door check 11 ok, 0 FAIL; 4 flights; 5 coins on levels 1/2/2/3/finale); world RunBatch exit 0; LedgeReport 0 ledges, 0 steep; autoplay bot ALL PASSED twice (random seed 93531, seed 13375): camera trap, alarm once, entrance, soldiers fire, KILL door waits for its level, L1 clear stops alarm, key + gate, last enemies, L3 patrol door, rifle-only robot, 4 stair flights, finale (boss, helicopter, win), checkpoint restore + kept coin, shot camera quiet. HUD shots: hud_alarm, hud_combat, hud_key_prompt, hud_boss, hud_end_screen.
**Final review** (fresh reviewer): fixed the end screen restart (SampleScene was not in Build Settings; one shared `VantageRun.ReloadLevel()`), ground-floor doors frozen after level 2 (doors now wake around the player too), alerted soldiers chasing off their level, the robot becoming invulnerable with a model that has colliders (hit forwarding; eye and muzzle moved beside `Model`; robot/boss placeholders rebuilt) and soldiers' rounds hurting the robot. Bot after the fixes: ALL PASSED (26 checks). Deferred minors: owned-weapon pickups stay after a checkpoint reload, the camera keeps watching after L1, the key-slot lamp stays red after a checkpoint past L2, the safety net moves the robot without `Warp`, restored weapons equip in arbitrary order.
**Open:** Play-mode check by a human (alarm-hunt balance with a pistol, robot and boss fights, camera indoors and on the outside stairs, extraction camera, Enter to restart, camera cone readability, key copy orientation in the slot); F9 captures on L1 (alarm) and the boss fight; robot/boss/helicopter models from Blender (replace the `Model` child); professor's feedback to be written down; story props and mood volumes; new Documentation screenshots; Stylized Coin Pack licence; nothing committed yet.

## 2026-10-14: Design spec for the new tower (Claude session)
**Asked:** Go with the boss + helicopter ending; coins as exploration reward, 1-2 per level, Claude picks spots; full-alert alarm; camera triggers the alarm; pistol on L1, rifle in the Armory; robot/boss modelled by the user in Blender; approach C (hybrid) "but make sure everything is optimized"; "you are the dev do whats best".
**Done:** Wrote `docs/superpowers/specs/2026-10-14-vantage-tower-3-levels-design.md` (architecture, level flow, coins, performance, error handling, testing, docs). Not committed (CLAUDE.md: commit only when asked).
**Verified:** Spec self-review (placeholders, consistency, scope). No code changed.
**Open:** User reviews the spec; then implementation plan, then build.

## 2026-10-14: New VANTAGE_Tower design discussion (Claude session)
**Asked:** New tower added (3 levels + final). L1: entering the room drops the yellow door and triggers an alarm, then the right room/hallway route; coins as goodies/achievements (PolyKebap Stylized Coin Pack). L2: robot, enemies, drones, a key that must be claimed to open the L3 door with E, with animations. Final level open: floor breaks and you fall / final boss / helicopter end scene: "what do you think?"
**Done:** Read-only exploration, classified as architectural (brainstorming → spec → plan before code). Findings:
- `Assets/VANTAGE_Tower/` (commit e6f4168) brings its own runtime (`VantageLevelManager`, `VantageDoor`, `VantageTrigger`, `VantageInteract`, `VantagePickup`, `VantageBreakable`, `VantagePatrolPath`, `VantageTag`) and an `AssetPostprocessor` that wires doors, shutter, alarm trigger, key slot, pickups and endings A/B/C from Blender custom properties.
- The new tower is **not in `SampleScene` yet**; the scene still uses the old `CommandTower` prefab and `VANTAGE Tower Setup`.
- Integration risks found: `VantageLevelManager.IsAlive` counts a spawned enemy as alive while its GameObject is active (template soldiers stay active when dead, so KILL doors would never open); enemies are spawned with plain `Instantiate` (no NavMesh snap, not our Soldier prefab rules); prompts use `OnGUI` instead of our TMP HUD; `VantagePickup` "Gun" does not unlock the template inventory; stairs need our smooth surfaces (no step-ups); old 4-level `VantageTowerLevels` must give way to the new 3-level + final flow.
**Verified:** Not applicable (design discussion). No project files changed.
**Open:** User to choose the ending and approve the design; then spec, plan, implementation, autoplay test, docs (CLAUDE.md, README, concept-doc Revisions row).

## 2026-10-13: New tower coming from Blender (Claude session)
**Asked:** "i will update the tower as this is not the final one - will add another one and will asign the textures in blender"
**Done:** Noted the plan: the current `CommandTower` is not final; the user will export a new tower with textures assigned in Blender. Listed what the setup code expects from a tower (prefab instance named `CommandTower`, `CommandTowerKit.GameplayMarker`s with `markerType`, `MK_PlayerEntry_*` markers, `TWR_StairColliders`/"Stairs" and "Fire Escape" names, separate structure vs furniture, real step geometry). No project files changed. The sandbag-collider-in-source-prefab issue and the three missing-material overrides become moot if the tower and those props are replaced/rebuilt.
**Verified:** Not applicable (plan noted); names checked with grep in `Assets/Vantage/Editor` and `CommandTower_Unity/Scripts`.
**Open:** After the new tower is in: back up the scene, run Tower → Set Up Command Tower, check the `[Vantage]` log (furniture, stairs, gates, skipped spawns), run the autoplay test, update CLAUDE.md asset/scene tables and add a concept-doc Revisions row.

## 2026-10-13: Project status check (vantage-watchdog)
**Asked:** "how is the project going - i will do some updates later just letting u know"
**Done:** Read-only review of git state, uncommitted work, new playtests and docs. No project files changed.
- Commit cbe8212 (Oct 9, "feedback from the professor") added a BoxCollider to the `SandbagPosition` nested instance **inside the source `CommandTower.prefab`** (breaks the "source art read-only, overrides only" rule; unlogged). The professor's feedback itself is not recorded anywhere (WORKLOG, CLAUDE.md, concept doc).
- Uncommitted `SampleScene.unity` (saved Oct 12): 3 material overrides on generated world props (CamoShelter, Pad_ContainerYard, Crate_Wood) under `VANTAGE World - Military Base`. Lost on the next Build Military World. The two material GUIDs (2f7808e6…, f4e1030c…) exist nowhere in `Assets/`, `Packages/` or `Library/PackageCache` (missing materials: the props likely render pink or blank). No `_Backups/` copy since Oct 8.
- Untracked `Assets/Textures/` (85 MB: 4K forest floor ×2, 8K skybox, macro variation; files dated 2025, copied from another project). Covered by LFS (`*.jpg`, `*.png`). Nothing references them yet. Not in the README asset table.
- Playtests Oct 11: seed 13375, 68 s, died on level 1 at (6.7, 4.5, -73.4) ~4 s after entering, 0 kills, 3 entrance backtracks; seed 88109 is the restart (logger shows "Session length 0s" with events from 68 s: session time is not reset on level restart).
**Verified:** git diff/show, GUID searches, file timestamps. Unity is open (Unity.exe running, lockfile Oct 13), so no batch checks were run. Claude session re-checked the two material GUIDs against every `.meta` in `Assets/`, `Packages/`, `Library/PackageCache`: still not found (CamoShelter and Crate_Wood → 2f7808e6…, Pad_ContainerYard → f4e1030c…).
**Open:** Record the professor's feedback; move the sandbag collider out of the source prefab (instance override or Setup); confirm the override materials exist; story props, per-space mood volumes and procedural depth remain the main grade gaps; F9 perf capture still missing.

## 2026-10-08: What the watchdog is responsible for (vantage-watchdog)
**Asked:** "Hello agent, what are your responsibilities for this development project?"
**Done:** Answered from `.claude/agents/vantage-watchdog.md` and `CLAUDE.md`. Covered review scope, the rules that break silently, verification, level-design and performance checks, documentation duties and what the agent doesn't do. No project files changed.
**Verified:** Not applicable (question only). The agent file and `CLAUDE.md` agree.
**Open:** Nothing.

## 2026-10-08: Work log and agent instructions (Claude session)
**Asked:** "Can you make the agent write and update everything it does and what I ask?"
**Done:** Created this `WORKLOG.md` and added the logging rule to `.claude/agents/vantage-watchdog.md` and `CLAUDE.md` (section *Work log*). Backfilled today's entries below.
**Verified:** Files written; the agent picks up its new instructions in the next Claude Code session (agent files load at startup).
**Open:** Nothing.

## 2026-10-08: Watchdog agent brought up to date (Claude session)
**Asked:** "Check that the agent is updated with the latest updates and tell me what to do next time to call him."
**Done:** `vantage-watchdog.md` now covers the user's character and the swap tool, TextMeshPro, `VantagePhysics`, spawn/hunting rules, entrance-rail cutting, the new batch commands (`ApplyBatch`, `ShotsBatch`, `-vantageSeed`), per-frame rules, the Avaturn licence and the no-hardcoded-values preference.
**Verified:** File reviewed after the edit.
**Open:** Restart Claude Code so the agent reloads.

## 2026-10-08: Gun floating beside the hands (Claude session)
**Asked:** "The gun is floating near the hands, you don't really carry it. Can you check that? I'll close Unity; check for other bugs."
**Done:**
- Cause: the swap matched the two avatars' own T-poses, which differ, so every gun ended up offset from the hand.
- `VantagePlayerCharacter` now gives the new body the old body's humanoid muscle pose (`HumanPoseHandler`). It moves each gun keeping its world rotation and its offset from the bone, then returns the skeleton to its rest pose.
- Restored the Cowboy rig from `_Backups/Third Person Rig_before_character_swap_2026-10-08_11-11.prefab` and redid the swap.
- Added `ShotsBatch` (hand close-ups in `Pistol_Idle`, skinning forced per render).
- Bot: the player's `AutoTakeCover` is turned off and the bot leaves cover before teleporting. That caused two false failures: the player was crouched out of sight, then held in place by the cover.
- Spawns need NavMesh on the marker's own floor (`MK_EnemySpawn_F1_Lounge_1`, on a picnic table, is skipped); soldiers stand on the NavMesh point.
- `-vantageSeed N` replays an enemy layout; soldier names include their spawn marker.
**Verified:** Hand shots match the Cowboy reference (grip in the palm). Bot ALL PASSED twice, once replaying seed 53953, which had shown the floor-3 soldier bug.
**Open:**
- Check the rifle grip (two hands, aiming, crouching) and the holsters while running in Play mode.
- The floor-3 soldier cause isn't proven; the safety net and marker names will catch a recurrence.

## 2026-10-08: CharacterFace error (Claude session)
**Asked:** "Array index (9) is out of bounds (size=0) … CharacterFace.cs:163" while playing.
**Done:** The template face script sets the Cowboy's 10 facial blend shapes by index; the swap had pointed it at the new body, which has none. The swap tool now switches `CharacterFace` off when the body lacks the shapes. The user re-ran Vantage → Player → Use Character Model.
**Verified:** The user confirmed ("it's good").
**Open:** The avatar has no facial expressions.

## 2026-10-08: User's character, TextMeshPro, hardcoded values, optimisation (Claude session)
**Asked:** "I added my own character, can you replace it with the current character, make sure everything works properly. I also added a TextMesh Pro asset. Continue with the fixes, make sure everything is optimised, and hardcoded stuff should be gone where not necessary."
**Done:**
- **Character:**
  - New tool `VantagePlayerCharacter` (Vantage → Player → Use Character Model): Humanoid import, textures extracted (extensions added; Blender left them off), materials extracted and linked by reading the FBX (`FbxTextureLinks`), hair alpha-clipped.
  - All 23 attachments moved to the new bones; old body removed. Height 1.69 → 1.64 m.
- **TMP:** `VantageUI` / HUD / minimap moved to TextMeshPro (runtime Bebas TMP font, underlay shadow, word spacing, LiberationSans fallback for ∞).
- **Hardcoded values:**
  - `VantagePhysics` holds the masks and eye/aim points from colliders;
  - tower footprint measured from meshes;
  - top storey and storey names come from the levels;
  - marker types, enemy side and drone spawn height are named;
  - the NavMesh bake mask is built from the layers.
- **Optimisation:**
  - `ActivePlayer()` is cached per frame;
  - the minimap has no `Find*`;
  - drones cache their eye material and property IDs.
- **Fixes:**
  - hunters only hunt while the player is on their level (they used to leave and be teleported back every 2 s);
  - drones under the floor-5 ceiling no longer count as the roof;
  - spawns chosen by marker height;
  - the knee-height rail across the fire-escape entrance is cut (whole-piece cutting, rays from both sides);
  - the bot tries every soldier for the firing test.
- **Docs:** `CLAUDE.md`, `README.md`, concept doc Revisions (Oct 8). The `Military_Env_Unity` → `MilitaryEnvUnity` rename is documented.
**Verified:** Offline compile; Tower Setup (6/6 gate checks); bot ALL PASSED; HUD screenshots checked.
**Open:**
- `Documentation/world_tower_plot.png` shows as deleted in git (not by this session).
- Check the Avaturn licence.

## 2026-10-08: Scan after the laptop crash (Claude session)
**Asked:** "My laptop crashed while you were working yesterday; scan and check what is wrong."
**Done:** No corrupted files (scene complete, NavMesh binary as expected, code compiled). Found a logic bug in the last edit: hunting enemies left their level and the safety net teleported them back every 2 s. Found that the last edits had never been tested and the docs were not updated. Fixed in the entry above.
**Verified:** Offline compile, file integrity checks.
**Open:** None (fixed in the entry above).

## 2026-10-07 and earlier
Before this log existed. See *History* in `CLAUDE.md` and the concept doc's *Revisions* table: third person only, the command tower and four levels, the military base world, the autoplay bot, smooth stairs, furniture and UI passes.
