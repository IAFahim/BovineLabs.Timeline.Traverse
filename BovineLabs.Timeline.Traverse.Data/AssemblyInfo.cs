using System.Runtime.CompilerServices;
using BovineLabs.Timeline;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Jobs;
using Unity.Mathematics;

[assembly: InternalsVisibleTo("BovineLabs.Timeline.Traverse")]
[assembly: InternalsVisibleTo("BovineLabs.Timeline.Traverse.Authoring")]
[assembly: InternalsVisibleTo("BovineLabs.Timeline.Traverse.Editor")]
[assembly: InternalsVisibleTo("BovineLabs.Timeline.Traverse.Tests")]

// TrackBlendImpl's nested jobs are scheduled from generic code in BovineLabs.Timeline, so the
// concrete instantiations must be registered for Burst. These registrations live HERE — in the
// assembly that DEFINES the type arguments — because Burst 1.8.29's entry-point scan failed to
// resolve the args ("Unable to resolve type `...NavSpeedMultipliers`. Reason: Unknown") when the
// attribute was hosted in BovineLabs.Timeline.Traverse. Keeping args module-local sidesteps that.
#if BL_ESSENCE
[assembly: RegisterGenericJobType(typeof(TrackBlendImpl<NavSpeedMultipliers, NavSpeedAnimated>.ResizeJob))]
[assembly: RegisterGenericJobType(typeof(TrackBlendImpl<NavSpeedMultipliers, NavSpeedAnimated>.AnimateUnblendedJob))]
[assembly: RegisterGenericJobType(typeof(TrackBlendImpl<NavSpeedMultipliers, NavSpeedAnimated>.AccumulateWeightedAnimationJob))]
#endif
[assembly: RegisterGenericJobType(typeof(TrackBlendImpl<float2, NavVelocityAnimated>.ResizeJob))]
[assembly: RegisterGenericJobType(typeof(TrackBlendImpl<float2, NavVelocityAnimated>.AnimateUnblendedJob))]
[assembly: RegisterGenericJobType(typeof(TrackBlendImpl<float2, NavVelocityAnimated>.AccumulateWeightedAnimationJob))]
