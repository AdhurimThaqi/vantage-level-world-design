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
