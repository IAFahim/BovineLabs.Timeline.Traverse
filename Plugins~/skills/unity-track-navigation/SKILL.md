---
name: unity-track-navigation
description: Master of NavigationTrack + MoveToClip/SteerDirectionClip/StopNavClip (package BovineLabs.Timeline.Traverse) and the whole com.bovinelabs.traverse navmesh stack it drives — the 2-field CrowdAgentData/IsPathfinding drive contract, the full working-setup checklist (3 settings + Burst + PREBAKED surface + Area=63 + MoveAgentAuthoring), timeline auto-play/loop/binding truth, stopDistance follow-halt, obstacle carve + re-bake, and the complete silent-failure triage table (frozen agent, zombie Invalid state, loop-wrap kill, Sample~ null-loads). Portable to any project containing the package; worked example (3 chasers pursue Player_XX) from vex-ee. Use when a designer asks "make this walk/chase/flee/stop on the navmesh" or when any Traverse agent is frozen with a clean console.
---

# Navigation track + Traverse navmesh specialist

## 1. SCOPE

You are the specialist for **BovineLabs.Timeline.Traverse** (the Navigation timeline track family) and, by necessity,
for standing up the **com.bovinelabs.traverse** stack it drives (NavMesh build → Movement/crowd → ORCA avoidance).
Nearly every failure a designer reports as "the track doesn't work" is actually stack setup — so this skill owns both.

**Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`; use the editor per `unity-cli`.**
Cross-references: `unity-stage-foundations` (stage/SettingsAuthoring), `unity-track-essence-stat` (move-speed stats),
the reaction bridge `ActionTimelineAuthoring` (fire nav timelines from gameplay — this package adds no trigger of its own).

Two drive paths ship; they are EXCLUSIVE per agent:
- **Timeline**: `NavigationTrack` (binds to `TargetsAuthoring`) hosting `MoveToClip` / `SteerDirectionClip` / `StopNavClip`.
- **Script twins** (`Vex.NavDemo` asm): `NavDemoTargetAuthoring`→fixed point; `ChaseTargetAuthoring{GameObject,StopDistance}`→chase entity.

**RULE 0 — never put both drivers on one agent.** The seek systems re-enable `IsPathfinding` every frame it is off,
which fights every `StopOnExit`/Stop clip. Pick one per agent.

## 2. THE DRIVE CONTRACT (2 fields, everything else is Traverse's job)

Write `CrowdAgentData.TargetPosition = float3` (property setter clears `IsDirection`) then
`SetComponentEnabled<IsPathfinding>(agent, true)`. Traverse does path→DesiredVelocity→ORCA→LocalTransform.
- **Re-write TargetPosition EVERY frame for a moving target** — `MoveRequestSystem` is change-version filtered;
  re-enabling `IsPathfinding` alone reuses the old corridor. `MoveToClip.follow=true` does this for you.
- Direction mode: write `TargetDirection` instead (SteerDirectionClip). Stop: disable `IsPathfinding`.
- Arrival auto-disables `IsPathfinding` (corridor-position check, hard-coded 0.1 m — `ApplyMovement.cs:117`).
- Speed/accel come from `MovementSettings` fallbacks (Default Move 3.5 / Accel 8 / Rot 3) or Essence stats (×100
  encoding) — never from a clip field.
- NEVER write `Path`, `CrowdAgentCacheBuffer`, or `ORCAAgentState`. Read them only to diagnose (§7).

## 3. WORKING SETUP CHECKLIST (every line is load-bearing; each miss is SILENT)

| # | Requirement | Exact value (vex-ee worked example) |
|---|---|---|
| 1 | 3 settings assets wired into a baked SubScene `SettingsAuthoring` | `NavMeshSettings`, `MovementSettings`, `AvoidanceSettings` (missing ⇒ per-frame `GetSingleton` throws — the ONE loud failure) |
| 2 | Burst compilation ON | Jobs > Burst > Enable Compilation. OFF ⇒ zero tiles build, frozen agents, **no error** |
| 3 | Ground = `NavMeshSourceAuthoring` | **`Area: 63`** (RC_WALKABLE — the default 0 is UNWALKABLE, silent), `Agents: 0xFFFFFFFF`, `Type: MeshFilter` + its own MeshFilter, + `LifeCycleAuthoring` |
| 4 | Surface = `NavMeshSurfaceAuthoring` | **`IsBaked = Scene` (PREBAKED)** + baked TextAsset + surfaces[0]{agent:0, bounds ≈ ±15/±5.25} + `LifeCycleAuthoring`. Runtime build (`IsBaked=None`) has NEVER produced a path in vex-ee — do not fight it |
| 5 | Agent = `MoveAgentAuthoring` | `Agent: 0` (registered Humanoid), `PathfindingEnabled`, `AvoidanceEnabled`, `UpdateFlags: 255`, `Layer: 1`, `CollidesWith: 255`, `Priority: 0.5` — RequireComponent auto-adds `StatAuthoring`; set `AddStats: 1` (**no Stat buffer ⇒ agent invisible to all movement queries**) |
| 6 | Agent has **NO physics body** | `MoveApplyPhysicsJob` stomps `PhysicsVelocity.Linear` every frame even while idle; use the transform-write path (no `PhysicsBodyAuthoring`). Strip primitive colliders (ECS-pure rule) |
| 7 | Everything inside the baked bounds; navmesh Y ≈ 0.15 | Agents/destinations outside bounds or off-mesh dead-end silently |
| 8 | `BL_ESSENCE` effectively on (versionDefines) | or `MoveApplySystem`/`MoveProcessPathSystem` compile out: plans paths, never moves |

Bake API (editor, subscene OPEN): static `BovineLabs.NavMesh.Editor.NavMeshSurfaceAuthoringEditor.Bake(surface)`
(reflection — asm is autoReferenced:false), then `SaveAssets` + save scene. Seconds when the Burst cache is warm;
first-ever bake can JIT for minutes — NEVER trigger it during play mode. `DoBake` silently skips a surface whose
gathered `Sources.Length == 0`.

## 4. TIMELINE CONTROL (source-verified truth)

- **Auto-play**: `TimelineBeginAuthoring{Mode=OnLoad}` on the director GO (or `playOnAwake=true`) ⇒ bakes
  `TimelinePlayRequest` enabled ⇒ `TimelineBeginSystem` enables `TimelineActive`. **`TimelineReferenceAuthoring` is
  NOT required and does NOT auto-play** — it is only a tag for manual triggers (StartUI/TimelinePlayTrigger).
- **Loop forever**: `director.extrapolationMode = DirectorWrapMode.Loop` bakes `TimerRange{Loop}` — survives baking,
  never disables `TimelineActive`. BUT see trap G6: use ONE long clip (e.g. 100000 s), not a short looping one.
- **Binding**: `director.SetGenericBinding(navTrack, agentTargetsAuthoring)` — scene-side table, so SAVE THE SUBSCENE
  or it silently vanishes. Unbound track ⇒ `TrackBinding.Value == Entity.Null` ⇒ per-frame silent no-op. Read back
  `GetGenericBinding` to verify. One shared `.playable` works for N agents (destination resolves via `Targets` at
  runtime — no scene refs live in the asset).
- **MoveToClip fields**: `destination` (Target enum; `None` ⇒ use `worldPosition`), `follow` (re-resolve every frame —
  REQUIRED for chase), `stopDistance` (>0 + follow ⇒ halt inside radius, auto-resume when target leaves; prevents
  ramming/jitter), `stopOnExit` (halt on clip end), `extents` (0 = agent default), `queryFilterType`.
  Non-follow delivery retries until the target resolves, then latches (`MoveToState.Delivered`).
- **Unresolved destination** (no `Targets` on agent / slot unset / no `LocalToWorld` on target) = silent per-frame
  retry — never delivers, never errors. Set the Targets slot.
- **StopNavClip** fires once on its enter edge and LOSES to any same-frame Move/Steer enable (two-pass ApplyJob:
  enables always beat disables). Don't overlap Stop with an active follow clip.
- **Stop/resume from gameplay**: disabling the director entity's `TimelineActive` mid-run halts the agent via
  `stopOnExit`; re-enabling resumes. Reaction-driven chase = point `ActionTimelineAuthoring` at the same director.
- Programmatic `.playable` edits do NOT re-bake an open SubScene — save/close/reopen (resave subscene) after editing.

## 5. OBSTACLES

Obstacle = Cube + `NavMeshSourceAuthoring{Area: 0 (RCNullArea = carve), Solid: 1, Type: MeshFilter, Agents: 0xFFFFFFFF}`
+ `LifeCycleAuthoring`. (`Solid` only matters when `Area==0`: ResolveSolidType ⇒ solid blocker volume.)
- Prebaked surface ⇒ **any obstacle add/move/resize REQUIRES a re-bake** (§3 API or menu BovineLabs > Traverse >
  Bake All Scenes). A moved obstacle with a stale bake silently keeps the old holes.
- Carve = footprint + agent-radius erosion (~0.6 m each side). **Keep walkable gaps ≥ ~4 m** — see trap G1.

## 6. GOTCHA CATALOGUE (each cost a debugging session; do not rediscover)

- **G1 — UPSTREAM ZOMBIE (unfixed, com.bovinelabs.traverse)**: an agent ORCA-pushed onto a marginal mesh strip
  (narrow-gap corner) goes `CrowdAgentCache.State = CrowdagentStateInvalid` + `TargetState = Requesting` **forever**:
  DesiredVelocity 0, `IsPathfinding` stuck enabled, immune to new targets AND to teleporting the body (recovery
  re-validates off the stale CorridorPosition; `UpdateMoveRequest` early-outs on Invalid — `MoveRequestSystem.cs:348/413`).
  A dead agent also ORCA-jams others in the same gap. MITIGATE: gaps ≥ ~4 m (a 2.8 m gap reproduced it; 4.3 m = zero
  stalls across a 5-teleport gauntlet). Diagnose via §7 cache dump.
- **G2 — Burst OFF**: entire navmesh build runs in Burst jobs. Off ⇒ zero tiles, frozen agents, clean console. The
  editor bake even warns "Trying to bake without burst. Bad idea." Suspect FIRST on DesiredVelocity=0 + no errors.
- **G3 — Area default 0**: a fresh `NavMeshSourceAuthoring` is UNWALKABLE. Ground needs `Area: 63`.
- **G4 — Sample~ is invisible to AssetDatabase**: `LoadAssetAtPath("Packages/.../Sample~/...")` returns null —
  a surface pointed at a Sample~ TextAsset silently has NO navmesh; `InstantiatePrefab(null)` throws. This killed
  `TraverseShowcaseBuilder` (also: borrowed baked bytes never match your geometry — bake your own).
- **G5 — source-without-built-surface latches a GAME-WIDE pause**: a `NavMeshSource` in a subscene with no working
  surface leaves `NavMeshLoadSystem`'s pause on forever. "Everything is paused" ⇒ check surfaces/Burst.
- **G6 — short looping nav clips kill agents**: every loop wrap gaps `ClipActive` one frame ⇒ `stopOnExit` fires
  mid-run each wrap. Continuous nav = ONE very long clip (100000 s) with `WrapMode.Loop` as backstop.
- **G7 — PhysicsVelocity stomp**: agents WITH `PhysicsVelocity` get `.Linear` overwritten every frame even while idle
  (`WithPresentRW<IsPathfinding>`). Nav agents carry no physics body; the chase TARGET may (it's only read).
- **G8 — scene moved, bake didn't**: relocating the nav stage (e.g. root to z=33.26) without re-baking leaves tiles at
  the old location — destinations "on the ground" are off-mesh. Bake and geometry move together, always.
- **G9 — stat buffer missing**: `MoveAgentAuthoring` without `StatAuthoring{AddStats:1}` drops the agent from every
  movement query. Frozen + absent from queries ⇒ check Stat.
- **G10 — enter-edge jobs and `ClipActivePrevious`**: on the enter frame it is DISABLED; `EnabledRefRO<>` does NOT
  suppress the enabled-filter — jobs reading the edge need `[WithPresent(typeof(ClipActivePrevious))]` (bug class
  already fixed in `MoveToJob`/`StopJob`; clone them, not Distance's `GatherActiveJob`).
- **G11 — destination off-mesh ≠ error**: `FindNearestPoly` "succeeds" with `nearestRef=0` ⇒ `TargetState=None` ⇒
  agent dead-ends with `IsPathfinding` still enabled. Recovers only because follow re-writes when the target returns
  over the mesh.

## 7. TRIAGE TABLE + DIAGNOSIS RECIPES (unity-cli, play mode)

| Signature | Cause |
|---|---|
| Per-frame `GetSingleton` exceptions | a settings asset unwired (§3.1) — the only LOUD failure |
| ALL agents frozen, DesiredVelocity=0, clean console | Burst off (G2) · Area 0 (G3) · surface missing/stale (G8) · null baked asset (G4) |
| Game-wide pause forever | G5 (or Burst off blocking the build) |
| One agent frozen, `pf=1`, dv=0, cache `Invalid/Requesting` | G1 zombie — restart play; widen gaps |
| `pf` flapping or disabled mid-run every N seconds | G6 loop wrap |
| Agent absent from movement queries entirely | G9 no Stat buffer |
| Clip active but nothing written | unbound track / unset Targets slot (§4) |
| Agent slides while "stopped" | G7 physics stomp |

Recipes (fully-qualify everything; **no `using` directives in exec**; authoring asms are autoReferenced:false ⇒
reflection `Type.GetType("T, Asm")` + `AddComponent(Type)` + `SerializedObject`):
- **Agent telemetry**: query `CrowdAgentData + LocalTransform`, print position + `IsComponentEnabled(IsPathfinding)`
  per entity, several samples apart. Agents cross the demo map in ~3 s — sample IMMEDIATELY after re-targeting or you
  only see the arrival ring.
- **Zombie check**: read `CrowdAgentCacheBuffer[0].Value` fields `TargetState`/`State` via non-generic
  `GetBuffer` reflection. Healthy = `Valid/CrowdAgentStateWalking`; zombie = `Requesting/CrowdagentStateInvalid`.
- **exec compiler traps**: `EntityManager.GetComponentData(entity, Type)` does NOT exist — reflect the generic
  `GetComponentData<T>(Entity)` via `MakeGenericMethod`; never fabricate `Entity.Version`; `EnabledRefRW` writes need
  the entity matched `WithPresent`.
- **Baked-entity audit** (subscene open): expect per agent `CrowdAgent, CrowdAgentData, CrowdAgentCacheBuffer, Path,
  DesiredVelocity, IsPathfinding(disabled), Stat buffer`; surface entity has `NavMeshBaked`; settings entity resolves
  `NavMeshConfig + MovementConfig + MovementStatsConfig + ORCAConfig`.

## 8. WORKED EXAMPLE (vex-ee, verified 2026-07-04)

Main Sub Scene `NavDemo` root at ORIGIN: Ground Cube 20×0.5×20 (source per §3.3) · Surface prebaked
(`Assets/Main Sub SceneNavMesh.bytes`) · wall 9×2×1 @ (0,1,3) + pillars 1.5³ @ (±5,1,−5) (per §5) ·
3 chaser capsules (per §3.5-6) each with `TargetsAuthoring{Target = Player_XX/Movement Physics}` and a
`Chase Director` child (`NavChase.playable`: one NavigationTrack + MoveToClip{Target, follow, stopDistance 1.5,
100000 s}, Loop, `TimelineBeginAuthoring OnLoad`, bound per §4). Proven: converge → halt at 1.5 m ring →
wall detour both gaps → `TimelineActive` off = full stop, on = resume. Tests: 9/9
`BovineLabs.Timeline.Traverse.Tests` (incl. 2 stopDistance cases).
