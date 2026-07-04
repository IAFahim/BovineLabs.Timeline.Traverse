#if BL_ESSENCE
using System;
using System.ComponentModel;
using BovineLabs.Reaction.Authoring.Core;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// Timeline track that scales the bound agent's movement stats while its clips are active. Bind it to the
    /// agent's <see cref="TargetsAuthoring" /> (same binding as <see cref="NavigationTrack" />). Hosts
    /// <see cref="NavSpeedClip" /> clips; overlaps blend and ease handles ramp the multipliers in and out.
    /// </summary>
    [Serializable]
    [TrackClipType(typeof(NavSpeedClip))]
    [TrackColor(0.95f, 0.6f, 0.2f)]
    [TrackBindingType(typeof(TargetsAuthoring))]
    [DisplayName("BovineLabs/Traverse/Nav Speed")]
    public sealed class NavSpeedTrack : DOTSTrack
    {
    }
}
#endif
