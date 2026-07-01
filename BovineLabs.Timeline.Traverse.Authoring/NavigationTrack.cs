using System;
using System.ComponentModel;
using BovineLabs.Reaction.Authoring.Core;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// Timeline track that drives Traverse navigation on the bound agent. Bind it to the agent's
    /// <see cref="TargetsAuthoring" /> (the prefab carrying MoveAgentAuthoring). Hosts MoveTo / Steer / Stop clips.
    /// </summary>
    [Serializable]
    [TrackClipType(typeof(MoveToClip))]
    [TrackClipType(typeof(SteerDirectionClip))]
    [TrackClipType(typeof(StopNavClip))]
    [TrackColor(0.3f, 0.55f, 0.95f)]
    [TrackBindingType(typeof(TargetsAuthoring))]
    [DisplayName("BovineLabs/Traverse/Navigation")]
    public sealed class NavigationTrack : DOTSTrack
    {
    }
}
