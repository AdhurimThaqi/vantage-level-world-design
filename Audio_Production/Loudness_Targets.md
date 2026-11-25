# VANTAGE loudness targets (for REAPER)

Measured on 2026-11-25 with ffmpeg (EBU R128: integrated loudness in LUFS, true peak in dBTP) on every WAV in `Assets/Audio`.

## How to use it

1. **Nothing to do right now.** The sound bank volumes are already set so the game plays at the *In game* column (Vantage > Audio > Sound Board).
2. **When you replace a file:** render it in REAPER at its *Render target*: in the Render dialog, normalize to LUFS-I at that value, with a true-peak limit of **-1 dBTP**, and the render bounds set to the time selection (no silence after the sound). Keep the same file name.
3. **Then set that sound's Volume to 1** in the Sound Board and press Refresh Sound Bank. The game sounds the same as now, from a clean file.
4. Rows marked *keep* are your own mix choices or too short to measure: render those at the level that sounds right and leave their Volume as it is.
5. **Music tracks:** keep them mastered as they are (about -14 LUFS, -1 dBTP) and leave their Volume: the game turns them down to the *In game* level.

Short one-shots (under 0.4 s: guns, footsteps, jump) have no integrated loudness; their peak is listed and they are set by ear.
*clips* = the true peak is above -1 dBTP: put a limiter on it when you render.

## Music

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `MUS_Explore` | 1 | -13.7 | -0.1 *clips* | 0.03 | 0.03 | -44.2 | keep | your choice: very quiet under the ambience |
| `MUS_Combat` | 1 | -13.0 | 0.9 *clips* | 0.05 | 0.05 | -39.0 | keep | your choice: very quiet under the ambience |
| `MUS_Menu` | 1 | -13.6 | 0.1 *clips* | 0.3 | 0.269 | -25.0 | master as is (-14) |  |
| `MUS_Intro` | 1 | -12.6 | 0.6 *clips* | 0.25 | 0.24 | -25.0 | master as is (-14) |  |
| `MUS_BossFight` | 1 | -12.6 | 0.1 *clips* | 0.2 | 0.24 | -25.0 | master as is (-14) |  |
| `MUS_End` | 1 | -11.7 | 0.2 *clips* | 0.2 | 0.216 | -25.0 | master as is (-14) |  |

## Stinger

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `STG_Lockdown` | 1 | -13.3 | -0.4 *clips* | 0.145 | 0.232 | -26.0 | -26 |  |
| `STG_Cleared` | 1 | -10.1 | -0.0 *clips* | 0.5 | 0.16 | -26.0 | -26 |  |
| `STG_Extraction` | 1 | -14.1 | 0.1 *clips* | 0.143 | 0.254 | -26.0 | -26 |  |
| `STG_BossFight` | 1 | -17.9 | -3.9 | 0.3 | 0.394 | -26.0 | -26 |  |

## Ambience

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `AMB_Exterior_Dusk` | 1 | -43.9 | -29.5 | 0.55 | 0.55 | -49.1 | keep | your choice (intro birds); the file is already very quiet |
| `AMB_L1_Interior` | 1 | -35.8 | -23.2 | 1 | 0.776 | -38.0 | -38 |  |
| `AMB_L2_Armory` | 1 | -31.6 | -13.8 | 1 | 0.479 | -38.0 | -38 |  |
| `AMB_L3_Destroyed` | 1 | -28.8 | -15.2 | 1 | 0.347 | -38.0 | -38 |  |
| `AMB_Helipad` | 1 | -25.1 | -11.0 | 1 | 0.226 | -38.0 | -38 |  |
| `AMB_Debris` | 3 | -28.5 | -4.0 | 1 | 0.671 | -32.0 | -32 |  |

## Guide

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `GUIDE_PA` | 1 | -18.3 | -3.0 | 0.8 | 0.519 | -24.0 | -24 |  |
| `GUIDE_Stairs` | 1 | -24.6 | -10.0 | 0.8 | 1 | -24.6 | -24 | file is 1 dB too quiet: raise it in REAPER |
| `GUIDE_Radio` | 1 | -23.2 | -3.0 | 0.8 | 0.912 | -24.0 | -24 |  |
| `GUIDE_Phone` | 1 | -11.1 | -3.0 | 0.8 | 0.226 | -24.0 | -24 |  |
| `KEYSLOT_Beep` | 1 | -16.4 | -4.0 | 0.8 | 0.417 | -24.0 | -24 |  |
| `GUIDE_Wind` | 1 | -19.7 | -3.8 | 0.8 | 0.61 | -24.0 | -24 |  |
| `GUIDE_HeliRadio` | 1 | -11.5 | -3.0 | 0.8 | 0.237 | -24.0 | -24 |  |

## Weapon

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `GUN_Pistol` | 1 | -23.0 | -3.8 | 1 | 1 | -23.0 | -18 | file is 5 dB too quiet: raise it in REAPER |
| `GUN_Empty` | 1 | - | -0.4 *clips* | 1 | 1 | - | keep | too short to measure |
| `GUN_Rifle` | 4 | - | 0.1 *clips* | 1 | 1 | - | keep | too short to measure: peaks at 0 dB, set by ear |

## Enemy

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `SENTRY_Glint` | 1 | -9.3 | -6.5 | 1 | 0.146 | -26.0 | -26 | a small tick before the shot |
| `SENTRY_Crack` | 1 | -16.5 | -4.5 | 1 | 0.841 | -18.0 | -18 |  |
| `ROBOT_Deflect` | 1 | -11.8 | 0.3 *clips* | 1 | 0.389 | -20.0 | -20 |  |
| `ROBOT_Telegraph` | 4 | -17.6 | -5.5 | 1 | 0.756 | -20.0 | -20 |  |
| `ROBOT_Fire` | 1 | -14.9 | -2.8 | 1 | 0.7 | -18.0 | -18 |  |
| `ROBOT_Hit` | 1 | -14.9 | -3.7 | 1 | 0.556 | -20.0 | -20 |  |
| `DRONE_Fire` | 1 | -14.9 | -2.8 | 1 | 0.7 | -18.0 | -18 |  |
| `TURRET_Fire` | 1 | -14.9 | -2.8 | 1 | 0.7 | -18.0 | -18 |  |
| `HELI_Gun` | 1 | -13.8 | -0.1 *clips* | 1 | 0.617 | -18.0 | -18 |  |

## Voice

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `SENTRY_WarningCall` | 1 | -26.3 | -12.1 | 1 | 1 | -26.3 | -22 | file is 4 dB too quiet: raise it in REAPER |
| `VO_Contact` | 1 | -8.8 | 0.1 *clips* | 1 | 0.219 | -22.0 | -22 |  |

## Loop

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `ALARM_Siren` | 1 | -21.0 | -13.8 | 0.5 | 0.5 | -27.0 | keep | your choice |
| `ROBOT_Servo` | 1 | -10.2 | 0.6 *clips* | 1 | 0.129 | -28.0 | -28 | loops while the robot walks |
| `DRONE_Hum` | 1 | -22.6 | -12.0 | 0 | 0 | - | keep | you muted it (0) |
| `HELI_Rotor` | 1 | -17.1 | -1.2 | 1 | 0.569 | -22.0 | -22 |  |

## Event

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `SHUTTER_Slam` | 1 | -22.5 | -1.0 | 1 | 1 | -22.5 | -18 | file is 4 dB too quiet: raise it in REAPER |
| `SHUTTER_Roll` | 1 | -17.9 | -4.8 | 1 | 0.785 | -20.0 | -20 |  |
| `BOSS_Reveal` | 1 | -23.2 | -11.7 | 1 | 1 | -23.2 | -22 | file is 1 dB too quiet: raise it in REAPER |
| `BOSS_Roar` | 1 | -22.3 | -9.7 | 1 | 1 | -22.3 | -20 | file is 2 dB too quiet: raise it in REAPER |
| `BODY_Fall` | 1 | -28.5 | -3.0 | 1 | 1 | -28.5 | -24 | file is 4 dB too quiet: raise it in REAPER |
| `GATE_Open` | 1 | -18.0 | -4.8 | 1 | 0.794 | -20.0 | -20 |  |

## Pickup

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `KEY_Pickup` | 1 | -15.0 | -0.2 *clips* | 1 | 0.562 | -20.0 | -20 |  |
| `KEYSLOT_Turn` | 1 | -20.8 | -2.3 | 1 | 1 | -20.8 | -20 | file is 1 dB too quiet: raise it in REAPER |
| `COIN_Collect` | 1 | -12.2 | -0.9 *clips* | 1 | 0.407 | -20.0 | -20 |  |
| `PICKUP_Weapon` | 1 | -29.8 | -5.2 | 1 | 1 | -29.8 | -20 | file is 10 dB too quiet: raise it in REAPER |

## Cutscene

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `CAM_Swoosh` | 2 | -17.0 | -0.1 *clips* | 1 | 0.71 | -20.0 | -20 |  |

## UI

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `UI_Move` | 1 | -11.7 | -0.1 *clips* | 0.15 | 0.153 | -28.0 | -28 |  |
| `UI_Select` | 1 | -25.6 | -10.0 | 1 | 0.759 | -28.0 | -28 |  |

## Footsteps

| Sound | Files | File loudness | True peak | Volume before | Volume now | In game | Render target | Note |
|---|---|---|---|---|---|---|---|---|
| `FS_Concrete` | 4 | - | -12.0 | 0.8 | 0.8 | - | keep | too short to measure: peak -12 dB |
| `FS_Metal` | 4 | - | -12.0 | 0.8 | 0.8 | - | keep | too short to measure: peak -12 dB |
| `FS_Dirt` | 4 | -33.7 | -13.9 | 0.8 | 0.8 | -35.7 | keep | kept at 0.8 like the other steps |
| `PLAYER_Jump` | 1 | - | -6.0 | 1 | 1 | - | keep | too short to measure |
