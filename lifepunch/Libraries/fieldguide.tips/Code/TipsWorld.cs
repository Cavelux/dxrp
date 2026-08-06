using System;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// Optional world seams a game sets once at bootstrap so world-anchored triggers can read the local
/// player. A library cannot reach into a game's player, so these stay null until the game wires them.
///
/// All are fail-inert: a trigger that needs an unset seam is simply INERT (it never fires and never
/// throws), so nothing crashes when a game skips them. Input, Signal and Timer triggers need no seam at
/// all. Set the seams you use, leave the rest null.
///
/// <code>
/// using Sandbox;
/// using FieldGuide.Tips;
///
/// TipsWorld.LocalPlayerPosition = () => MyLocalPlayer.WorldPosition;
/// TipsWorld.AimRay = () => new Ray( MyCamera.WorldPosition, MyCamera.WorldRotation.Forward );
/// </code>
/// </summary>
public static class TipsWorld
{
	/// <summary>Whether a local player exists to coach. Read by the self-driving coach when no context is
	/// pushed. Default: always true, so input-only tips show without any bootstrap wiring.</summary>
	public static Func<bool> HasLocalPlayer { get; set; } = static () => true;

	/// <summary>Local player world position, for <see cref="TipTriggerObject.Mode.PlayerEntered"/> triggers.
	/// Null leaves those triggers inert.</summary>
	public static Func<Vector3> LocalPlayerPosition { get; set; }

	/// <summary>Camera / aim ray, for <see cref="TipTriggerObject.Mode.LookedAt"/> triggers. Null leaves
	/// those triggers inert.</summary>
	public static Func<Ray> AimRay { get; set; }
}
