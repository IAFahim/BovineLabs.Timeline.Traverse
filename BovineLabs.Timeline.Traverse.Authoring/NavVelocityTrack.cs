using System;
using System.ComponentModel;
using BovineLabs.Reaction.Authoring.Core;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// Timeline track that manually drives the bound agent's desired velocity while its clips are active,
    /// overriding the navigation pipeline (MoveTo targets resume the instant the clip ends). Bind it to the
    /// agent's <see cref="TargetsAuthoring" /> (same binding as <see cref="NavigationTrack" />). Hosts
    /// <see cref="NavVelocityClip" /> clips; overlaps blend and ease handles ramp velocity from/to zero.
    /// </summary>
    [Serializable]
    [TrackClipType(typeof(NavVelocityClip))]
    [TrackColor(0.2f, 0.8f, 0.55f)]
    [TrackBindingType(typeof(TargetsAuthoring))]
    [DisplayName("BovineLabs/Traverse/Nav Velocity")]
    public sealed class NavVelocityTrack : DOTSTrack
    {
    }
}
