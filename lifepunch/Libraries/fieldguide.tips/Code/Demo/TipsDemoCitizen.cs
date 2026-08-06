using System;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// Builds and drives the demo pawn's look: a dressed stock citizen, spawned in code as a child of the pawn
/// object. Everything here ships with the engine (the citizen model, its clothing, its animgraph), so the
/// demo still carries zero art of its own.
///
/// WHY IN CODE. The pawn object is authored in <c>tips_demo.scene</c> with a plain box renderer, and the
/// scene stays exactly that on disk. <see cref="TipsDemoPawn"/> switches the box off at boot and builds
/// this instead, so the swap costs no scene churn and the editor viewport still shows the simple authored
/// block when nothing is playing.
///
/// WHY NOT CitizenAnimationHelper. The engine ships a helper component that wraps these same animgraph
/// parameters, but it lives in a namespace a Field Guide kit is not allowed to reference (the isolation
/// lint severs it). The parameter names below are the ones that helper sets, driven directly on the
/// renderer, which is also what the placement and vehicle physics kits do.
///
/// Not part of the kit's runtime surface: delete <c>Code/Demo</c> and <c>Assets/demo</c> when you drop the
/// kit into your own project.
/// </summary>
internal static class TipsDemoCitizen
{
	private const string ModelPath = "models/citizen/citizen.vmdl";

	/// <summary>A plain outfit from the shipped citizen clothing resources, the same two items the
	/// placement kit's demo wears. Each is null-checked, so a missing asset degrades to a barer citizen
	/// rather than a broken spawn.</summary>
	private static readonly string[] Outfit =
	{
		"models/citizen_clothes/shirt/Jumpsuit/blue_jumpsuit.clothing",
		"models/citizen_clothes/shoes/Trainers/trainers.clothing",
	};

	/// <summary>
	/// Spawn the citizen as a child of the pawn. <paramref name="footOffset"/> is its local Z: the pawn
	/// object sits half a block above the ground because the authored box is centred on it, and the citizen
	/// model's origin is at its feet, so the visual drops by that half height to stand on the floor.
	/// Returns null if the model did not load, which leaves the caller free to keep the block.
	/// </summary>
	public static SkinnedModelRenderer Build( GameObject pawn, float footOffset )
	{
		var go = pawn.Scene.CreateObject();
		go.Name = "Demo Citizen Visual";
		go.SetParent( pawn, false );
		go.LocalPosition = Vector3.Up * footOffset;

		var model = Model.Load( ModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"[tips] demo citizen model '{ModelPath}' did not load; keeping the authored block." );
			go.Destroy();
			return null;
		}

		var renderer = go.Components.Create<SkinnedModelRenderer>();
		renderer.Model = model;
		Dress( renderer );

		// Grounded with no move input is the citizen animgraph's rest state: a standing idle that breathes
		// and shifts weight, which is what the demo wants between tips.
		renderer.Set( "b_grounded", true );

		return renderer;
	}

	/// <summary>
	/// Push a frame of locomotion at the animgraph. <paramref name="velocity"/> is what the pawn actually
	/// travelled, <paramref name="wishVelocity"/> is what the stick asked for; the graph uses the first for
	/// the legs and the second for arm swing in the air, which is why both go across.
	/// </summary>
	public static void Drive( SkinnedModelRenderer renderer, Vector3 velocity, Vector3 wishVelocity, bool grounded )
	{
		if ( !renderer.IsValid() )
			return;

		renderer.Set( "b_grounded", grounded );
		SetMotion( renderer, "move", velocity );
		SetMotion( renderer, "wish", wishVelocity );
	}

	/// <summary>Fire the animgraph's one-shot hop. It self-clears, so it is set and forgotten.</summary>
	public static void TriggerJump( SkinnedModelRenderer renderer )
	{
		if ( renderer.IsValid() )
			renderer.Set( "b_jump", true );
	}

	/// <summary>The six parameters the citizen animgraph reads per motion channel, resolved against the
	/// renderer's own facing so a sideways walk plays the strafe blend rather than a forward one.</summary>
	private static void SetMotion( SkinnedModelRenderer renderer, string channel, Vector3 velocity )
	{
		var rotation = renderer.WorldRotation;
		var forward = rotation.Forward.Dot( velocity );
		var sideward = rotation.Right.Dot( velocity );
		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		renderer.Set( $"{channel}_direction", angle );
		renderer.Set( $"{channel}_speed", velocity.Length );
		renderer.Set( $"{channel}_groundspeed", velocity.WithZ( 0f ).Length );
		renderer.Set( $"{channel}_x", forward );
		renderer.Set( $"{channel}_y", sideward );
		renderer.Set( $"{channel}_z", velocity.z );
	}

	private static void Dress( SkinnedModelRenderer renderer )
	{
		var outfit = new ClothingContainer();
		var any = false;

		foreach ( var path in Outfit )
		{
			var item = ResourceLibrary.Get<Clothing>( path );
			if ( item is null )
			{
				Log.Warning( $"[tips] demo clothing '{path}' did not resolve, skipping that slot." );
				continue;
			}

			outfit.Add( item );
			any = true;
		}

		if ( any )
			outfit.Apply( renderer );
	}
}
