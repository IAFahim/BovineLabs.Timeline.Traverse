# BovineLabs Timeline Traverse

DOTS Timeline tracks that drive [BovineLabs Traverse](https://gitlab.com/tertle/com.bovinelabs.traverse) navigation from Unity Timeline.

## What it does

A single **Navigation** track, bound to a navigating agent's `TargetsAuthoring`, hosts three clips:

| Clip | Effect | Writes |
|------|--------|--------|
| **Move To** | Path the agent to a `Target` entity or a static world point. `Follow` re-writes the destination each frame to chase a moving target. | `CrowdAgentData.TargetPosition` + enables `IsPathfinding` |
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

## Not included (yet)

- Continuous manual-velocity drive (`DesiredVelocity` WriteGroup + ORCA `ManuallyControlled`) — needs live solver tuning.
- `EntityLink` destination resolution — the `Target` enum covers the common cases.
