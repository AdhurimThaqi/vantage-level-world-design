# Sentries, warning ring and intro cutscene: design

Date: 2026-10-19. Status: approved in conversation (four sections), waiting for review of this file.

## Why

- After the presentation: the player can walk away from the tower into the base and the forest (made walkable on Oct 7), and nothing in the level says not to. The user wants enemies outside on the 3rd floor that patrol there, look around and stop the player from going "somewhere else instead of the tower".
- The professor suggested a cutscene. One intro shot sequence introduces the goal (the helipad) and the new threat (the sentries) without HUD text.
- Level-design value: a boundary with a visible reason instead of an invisible wall, the threat shown before it matters, and the goal framed at the start.

## Decisions (from the conversation)

| Question | Decision |
|---|---|
| Where does "running away" start? | A ring around the tower plot: free, then a warning band, then a kill zone. |
| Where do the sentries walk? | Both outside areas up top: the rim of the broken slab at 3rd-floor height (west/south) and the rim of the helipad platform (east). |
| Cutscenes | Intro only. |
| Sentry fighting | Our `Soldier.prefab` with a new `VantageSentry` brain in place of `VantageEnemyAggression`. |
| Sentry routes | Worked out by Setup from the baked NavMesh (no Blender change). |
| Camera takeover | A shared `VantageCinematic` helper used by the intro and the extraction. |
| Audio | Every new sound is an `AudioClip` slot with a synthesized fallback (the user will make the sounds in the SoundCrea module). |

## 1. The warning ring

Distances are measured from the tower plot (tower footprint + 8 m, the rectangle `VantageMilitaryBase` levels), as the distance outside that rectangle. All values are Inspector fields on `VantageBoundary`.

| Zone | Default | Effect |
|---|---|---|
| Free | 0 to 15 m past the plot | Nothing. |
| Warning | 15 to 35 m | HUD message "SENTRIES ABOVE: GET BACK TO THE TOWER" (flashes the first time); sentries in sight turn and fire warning shots: deliberate misses 1 to 3 m from the player with a tracer, a crack and a dust puff. |
| Kill | beyond 35 m, or more than 5 s without a break in the warning band | Sentry shots hit: about 25 damage each, one every ~1.2 s per sentry, through the normal `SendMessage("OnHit", Hit)` path. Stops when the player is back in the free zone. |

- Ring shots are scripted by `VantageSentry` (the template guns don't reach 40 to 80 m downhill and can't miss on purpose). Each shot is preceded by a short laser glint on the sentry so the player sees where it comes from.
- The ring is ignored inside the tower (`LevelAt` ≥ 0), during a cinematic (intro, extraction) and while the player is dead.
- A dead sentry enforces nothing. With both dead the HUD still warns but nothing fires.
- Minimap (`VantageMinimap`): the free zone's edge as a faint line, the warning band tinted red.
- Line of sight: `VantagePhysics.Sight` from `VantagePhysics.Eye(sentry)` to `VantagePhysics.AimPoint(player)`, checked at most every 0.2 s.

## 2. The sentries

- **Spawner:** `VantageSentries` (new component, under `VANTAGE Tower Setup/Sentries`) spawns `SENTRY_Slab` and `SENTRY_Helipad` from `Soldier.prefab` at scene start, whatever the current level. `VantageEnemyAggression` is removed from them and `VantageSentry` added.
- **Not level enemies:** they are not in `VantageTowerLevels`' enemy lists, so `Remaining`, hunting, `returnLostEnemies`, the HUD pips and the KILL lists ignore them. Their names match no KILL pattern.
- **Routes (Setup, after the NavMesh bake):** two NavMesh regions. *Slab:* triangles within 1 m of the 3rd floor's height (`Levels[2].FloorY`) whose centre lies outside the 3rd floor's walled rooms (the x/z bounds of the `LVL3_` wall meshes) and outside `FinaleArea`, keeping the largest connected piece. *Helipad:* triangles inside `FinaleArea` within 1 m of the `FINAL_Platform` top surface, largest connected piece. For each, the outer boundary edges of that region are traced and 3 or 4 waypoints placed 1.5 m inward, snapped to the NavMesh. Saved on `VantageSentries` (serialized point lists), logged (`[Vantage] Sentry route Slab: 4 points, 31 m`), drawn as gizmos. A region with no NavMesh logs `[Vantage] Sentry route Helipad: FAIL no NavMesh` and that sentry is not spawned.
- **Behaviour (`VantageSentry`, thinks every 0.25 s):**
  1. Player in the warning/kill zone and in sight: stop, face the player, fire ring shots (section 1).
  2. Player on the sentry's level (`LevelAt`: the 3rd floor for the slab sentry, the finale for the helipad sentry) and in sight within 32 m: template combat (`ToAttack`, `ToOpenFire`), cover off as for other soldiers.
  3. More than 6 m from the route: walk back to the nearest route point.
  4. Otherwise patrol: walk the route back and forth (broadcast `ToWalkTo`, as `VantageEnemyAggression.patrol` does); at each end stop ~3 s and scan ±60° outward.
  Never hunts, never uses stairs (the `AIMovement` floor-snap edit already drops targets on another floor).
- **Run state:** names of dead sentries go into `VantageRun` (new `DeadSentries` set, cleared with the run). The spawner skips them after a reload.
- **Audio slots:** warning call ("Turn back!"), radio chatter loop, ring-shot crack; synthesized fallbacks via `VantageProceduralAudio`.
- **Balance note (not built):** a flag to stand the helipad sentry down when the boss spawns, if playtests show the finale is too crowded.

## 3. The intro cutscene

- **Component:** `VantageIntro` (new, in the scene under `VANTAGE Tower Setup`).
- **When:** on scene start when `VantageRun.Checkpoint == 0` and `VantageRun.IntroPlayed` is false; sets `IntroPlayed`. `VantageRun.Reset()` clears it. The bot can request or suppress it.
- **Shots** (positions from the tower bounds, `EntrancePoint`/facing, the sentries, `FINAL_Helipad` and the player; nothing hardcoded):

| # | Time | Camera | Caption |
|---|---|---|---|
| 1 | 0 to 3.5 s | High and far out in front of the entrance, easing in on the tower | VANTAGE |
| 2 | 3.5 to 7 s | Rises along the tower face to the slab, framing `SENTRY_Slab` scanning | Sentries watch the ground |
| 3 | 7 to 9.5 s | Swings to the helipad and holds (helipad sentry in frame) | Your way out: the helipad |
| 4 | 9.5 to 13 s | Down to the player, blending into the exact `ThirdPersonCamera` pose; control back | (none) |

- If a sentry is missing, shot 2 or 3 frames the route's midpoint instead.
- **Skip:** any key or mouse button after 0.5 s jumps to shot 4's end pose and ends the intro.
- **During it:** `VantageCinematic.Begin()`; HUD and minimap hidden; letterbox bars and captions (built with `VantageUI`); ring off; sentries keep patrolling.
- **Audio slots:** intro music/ambience (fades into the game's ambience), a whoosh on hand-back. Fallback: the existing synthesized ambience with a short swell.
- **Logger:** `VantagePlaytestLogger` records "intro watched" or "intro skipped at n s".

## 4. Shared camera takeover: `VantageCinematic`

- Static helper. `Begin()`: `ThirdPersonInput` off, `CharacterHealth.IsTakingDamage` false, `ThirdPersonCamera` and `VantageCameraFader` off, remembers what it changed, raises `VantageEvents.CinematicStarted`. `End()`: restores exactly what `Begin()` changed, raises `CinematicEnded`. `Active` property.
- `VantageExtraction.freezePlayer()` is replaced by `VantageCinematic.Begin()` (the extraction never ends it; the end screen follows).
- `VantagePlayerHUD`, `VantageMinimap` and `VantageBoundary` listen to the two events.

## Files

New: `Scripts/VantageBoundary.cs`, `Scripts/VantageSentries.cs`, `Scripts/VantageSentry.cs`, `Scripts/VantageIntro.cs`, `Scripts/VantageCinematic.cs`.
Changed: `VantageRun` (IntroPlayed, DeadSentries), `VantageEvents` (cinematic events), `VantageExtraction`, `VantagePlayerHUD` (warning message, letterbox hide), `VantageMinimap` (ring), `VantagePlaytestLogger`, `Editor/VantageTower.cs` (sentry routes after the bake, spawner, boundary and intro objects), `Testing/VantageAutoplayBot.cs`.
Rules respected: one component per file; no per-frame `Find*`/`GetComponent`/LINQ; layers via `VantagePhysics`; damage via `OnHit`; alive via `CharacterMotor.IsAlive`.

## Testing

New bot checks (PASS/FAIL lines):
1. Intro plays on a new run; afterwards input, `ThirdPersonCamera` and the camera at the player are back; not replayed after the death reload; a skip ends it within a frame.
2. Both sentries spawn on the NavMesh of their region (slab, helipad platform) and walk at least 5 m within 15 s.
3. In 30 s neither is more than 6 m from its route nor off its floor.
4. Free zone: 8 s in the yard, no sentry shots.
5. Warning zone: HUD warning shown, warning shots (dust events) without damage for the first 5 s, then hits.
6. Kill zone: damage within 3 s beyond 35 m; back in the free zone it stops.
7. L3 exit and the finale open with both sentries alive.
8. A sentry killed before a death stays dead after the reload.

All existing checks must still pass on a random seed and seed 52703. Screenshots: `intro_shot2`, `hud_warning`.

Procedure (CLAUDE.md workflow): Unity closed, scene backup in `_Backups/`, offline compile, `SetupBatch` (both sentry routes logged, door check 0 FAIL, 5 coins), `VantageAutoplay.RunBatch`.

## Docs

CLAUDE.md (scripts, sentry rule, ring, bot checks, history), README (ring, sentries, intro), concept doc *Revisions* row (professor's cutscene feedback; leaving the tower), WORKLOG.

## Out of scope

`Model` children for drones and turret (separate task); the user's sound files (slots left empty); the helipad-sentry stand-down flag.

## Open for a human in Play mode

Ring distances and the 5 s grace feel fair; intro pacing and framing; ring shots readable (glint, tracer).
