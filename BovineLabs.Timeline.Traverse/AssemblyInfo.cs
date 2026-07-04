using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BovineLabs.Timeline.Traverse.Debug")]
[assembly: InternalsVisibleTo("BovineLabs.Timeline.Traverse.Editor")]
[assembly: InternalsVisibleTo("BovineLabs.Timeline.Traverse.Tests")]

// NOTE: TrackBlendImpl<...> RegisterGenericJobType registrations live in
// BovineLabs.Timeline.Traverse.Data/AssemblyInfo.cs, beside the type-argument definitions.
// Burst's entry-point scan could not resolve the Traverse.Data args from this assembly's
// attribute context (see Data/AssemblyInfo.cs for details).
