using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// Teleports the bound agent on the clip's enter edge (fires once per activation, retrying until the
    /// destination resolves). Optionally reorients, zeroes physics momentum, and resumes pathfinding to the
    /// agent's current nav target from the new position.
    /// </summary>
    public sealed class WarpNavClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Destination")]
        [Tooltip(
            "Entity to warp to, resolved from the bound agent's Targets. Set to None to use the World Position below instead.")]
        public Target destination = Target.Target;

        [Tooltip("World-space point to warp to when Destination is None.")]
        public Vector3 worldPosition;

        [Header("Orientation")]
        [Tooltip("Face a direction or target after warping.")]
        public bool reorient;

        [Tooltip("Whom to face after the warp, resolved from the bound agent's Targets. Set to None to use the Face Direction below instead.")]
        public Target faceTarget = Target.Target;

        [Tooltip("World-space direction to face when Face Target is None.")]
        public Vector3 faceDirection = Vector3.forward;

        [Header("Navigation")]
        [Tooltip(
            "After the warp: Halt stops navigation; Resume Current Target replans to the agent's current nav target from the new position.")]
        public WarpResume resume = WarpResume.Halt;

        [Header("Physics")]
        [Tooltip("Zero PhysicsVelocity so momentum doesn't carry through the warp (physics-driven agents).")]
        public bool zeroVelocity = true;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new WarpData
            {
                Destination = destination,
                WorldPosition = worldPosition,
                Reorient = reorient,
                FaceTarget = faceTarget,
                FaceDirection = faceDirection,
                Resume = resume,
                ZeroVelocity = zeroVelocity,
            });
            commands.AddComponent(default(WarpState));

            base.Bake(clipEntity, context);
        }
    }
}
