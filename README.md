# BovineLabs Timeline Traverse

DOTS Timeline tracks that drive [BovineLabs Traverse](https://gitlab.com/tertle/com.bovinelabs.traverse) navigation from Unity Timeline.

## What it does

A single **Navigation** track, bound to a navigating agent's `TargetsAuthoring`, hosts three clips:

| Clip | Effect | Writes |
|------|--------|--------|
| **Move To** | Path the agent to a `Target` entity or a static world point. `Follow` re-writes the destination each frame to chase a moving target; `Stop Distance` (>0, Follow only) halts inside the radius and auto-resumes when the target leaves it. | `CrowdAgentData.TargetPosition` + enables/disables `IsPathfinding` |
| **Steer Direction** | Move in a world-space direction (velocity mode, no corridor). | `CrowdAgentData.TargetDirection` + enables `IsPathfinding` |
| **Stop** | Halt navigation. | disables `IsPathfinding` |

The agent prefab must carry `MoveAgentAuthoring` (Traverse). Binding the track to a non-agent entity is a silent no-op.

## How it fits

This is the **effect** half. To fire a navigation timeline from gameplay, reuse the existing
`BovineLabs.Reaction.Timeline` bridge (`ActionTimelineAuthoring`): a reaction plays a director
that contains a Navigation track. This package adds no trigger of its own.

## Requirements

- `com.bovinelabs.traverse` (Movement/NavMesh/Avoidance), `com.bovinelabs.reaction`, `com.bovinelabs.timeline`.
- `BL_ESSENCE` must be defined for Traverse movement to actually apply (`MoveApplySystem` is gated on it).
- Traverse settings default to the `Server` world; agents may not bake in a client-only world without the `BL_NAVMESH_CLIENT*` defines. The runtime assembly mirrors `BovineLabs.Movement.Data`'s define constraints so it compiles out together when navmesh is disabled.

## Making it actually move (the checklist that is silent when wrong)

Every line below fails **without a console error** when missed. In order:

1. **Settings**: `NavMeshSettings`, `MovementSettings`, `AvoidanceSettings` all wired into a baked SubScene
   `SettingsAuthoring`. (A missing one is the single *loud* failure: per-frame `GetSingleton` throws.)
2. **Burst ON** (Jobs > Burst > Enable Compilation). The navmesh build runs in Burst jobs — off means zero tiles,
   frozen agents, clean console.
3. **Ground**: `NavMeshSourceAuthoring` with **`Area = 63`** (the default `0` is *unwalkable*), `Type = MeshFilter`
   + its own MeshFilter, plus `LifeCycleAuthoring`.
4. **Surface**: `NavMeshSurfaceAuthoring` with **`Is Baked = Scene` (prebaked)** + the baked TextAsset, surfaces[0]
   agent 0, plus `LifeCycleAuthoring`. Runtime build (`Is Baked = None`) is unreliable — prebake
   (`BovineLabs > Traverse > Bake All Scenes`, subscene open; fast once the Burst cache is warm; never during play).
5. **Agent**: `MoveAgentAuthoring` (Agent 0) + `StatAuthoring{AddStats}` (no Stat buffer = invisible to movement) and
   **no `PhysicsBodyAuthoring`** (`MoveApplyPhysicsJob` stomps `PhysicsVelocity.Linear` every frame, even idle).
6. **Timeline auto-play**: `TimelineBeginAuthoring{OnLoad}` (or `playOnAwake`) on the director. Bind the Navigation
   track to the agent's `TargetsAuthoring` and **save the subscene** (bindings are scene-side). Continuous nav = one
   very long clip (e.g. 100000 s) — a short looping clip fires `stopOnExit` on every wrap.

## Known traps

- **Never** put `NavDemoSeek`/`ChaseTarget` (the script drivers) on a NavigationTrack-bound agent — they fight `stopOnExit`.
- Obstacles = `NavMeshSourceAuthoring{Area = 0, Solid}`; **every obstacle add/move needs a re-bake**, and keep walkable
  gaps **≥ ~4 m**: an upstream Traverse bug permanently zombifies an agent ORCA-pushed onto a marginal strip
  (`CrowdAgentCache.State = Invalid` + `TargetState = Requesting` forever; immune to new targets and teleports).
- `Sample~` folders are invisible to `AssetDatabase.LoadAssetAtPath` — never reference Sample~ assets from a scene or
  editor script (returns null, silently no navmesh).
- Destination resolution needs the agent's `Targets` slot set; unresolved = silent per-frame retry, never an error.
- Moving the nav stage in the scene without re-baking leaves the tiles at the old location (destinations off-mesh).

The full agent-facing field guide — drive contract, gotcha catalogue with signatures, unity-cli triage recipes — lives
in `Plugins~/skills/unity-track-navigation/SKILL.md`.

## Debug drawer

`BovineLabs.Timeline.Traverse.Debug` (compiled only under `UNITY_EDITOR || BL_DEBUG`) ships `NavDebugSystem`, a
Quill-based gizmo drawer for agents currently driven by a Navigation track. Toggle at runtime with the ConfigVar
`bovinelabs.timeline.traverse.debug.draw` (default **off**; BovineLabs > ConfigVars, or set it from the console).
Per active clip it draws:

- **Move To** — destination sphere + line from the agent; `Stop Distance` ring when > 0; `MOVETO HALT` /
  `MOVETO` state text.
- **Steer Direction** — world-space direction arrow from the agent.
- **Patrol** — the waypoint route polyline with per-waypoint markers, arrival-radius circles, and current-index
  highlight; phase text (`Moving`/`Waiting`/`Done`).
- Latched/unresolved destinations render dimmed, so a delivered-but-stale target is visually distinct from a live one.

The drawer only reads component state — it never writes navigation data, so it is safe to leave enabled while
reproducing bugs.

## Not included (yet)

- Continuous manual-velocity drive (`DesiredVelocity` WriteGroup + ORCA `ManuallyControlled`) — needs live solver tuning.
- `EntityLink` destination resolution — the `Target` enum covers the common cases.
