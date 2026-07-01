using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    public sealed class StopNavClip : DOTSClip, ITimelineClipAsset
    {
        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(default(StopData));

            base.Bake(clipEntity, context);
        }
    }
}
