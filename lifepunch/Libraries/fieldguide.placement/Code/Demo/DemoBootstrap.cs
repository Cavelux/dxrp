using Sandbox;
using System.Collections.Generic;

namespace FieldGuide.Placement;

/// <summary>
/// Wires the demo scene in code so the whole kit is exercised from one component.
///
/// The scene it builds is the kit's hero case: a dressed stock citizen standing in the middle, with
/// three accessories parented to mounts on its skeleton (a tool in the right hand, a hat on the head, a
/// pack on the spine). Each is registered with the <see cref="TweakSession"/>, so pressing P gives you
/// three tabs of sliders that move the accessory RELATIVE to the bone it hangs from, and Copy hands you
/// the paste-ready offset for your own game code. Everything ships with the engine: the citizen model,
/// its clothing, and the dev primitives standing in for your accessories.
///
/// TWO-LEVEL ACCESSORY SHAPE. Each accessory is a bare root GameObject holding one or more child parts
/// that carry the models. The split is load-bearing: the tweak panel's Scale row writes a UNIFORM
/// LocalScale on the root, so any non-uniform proportions put there would be flattened the first time
/// someone touched the slider. Proportions live on the children, authored directly in engine units; the
/// root's scale stays a clean multiplier that reads 1 at the authored size.
///
/// The ghost-placement flow is still here as the second beat: press B and drop boxes on the ground, and
/// they export as world-space placements alongside the accessory offsets.
///
/// Not part of the kit's runtime surface: delete Code/Demo when you drop the kit into your own project.
/// </summary>
[Title( "Placement Demo Bootstrap" )]
[Category( "Field Guide · Placement" )]
[Icon( "auto_awesome" )]
public sealed class DemoBootstrap : Component
{
	/// <summary>Radius (engine units) inside which the demo validity seam reports a spot as valid, to show
	/// the ghost's green/red tint switching. Set to 0 or negative to allow placement anywhere.</summary>
	[Property] public float DemoValidRadius { get; set; } = 512f;

	/// <summary>Yaw the demo citizen faces. 225 puts its front toward the orbit camera's starting angle.</summary>
	[Property] public float CitizenYaw { get; set; } = 225f;

	const string CitizenModel = "models/citizen/citizen.vmdl";
	const string FallbackMaterial = "materials/default.vmat";

	/// <summary>A plain outfit from the shipped citizen clothing resources. Each item is null-checked, so a
	/// missing asset degrades to a barer citizen rather than a broken spawn.</summary>
	static readonly string[] Outfit =
	{
		"models/citizen_clothes/shirt/Jumpsuit/blue_jumpsuit.clothing",
		"models/citizen_clothes/shoes/Trainers/trainers.clothing",
	};

	/// <summary>Accessories waiting for their mount to resolve before their starting transform is applied.
	/// See <see cref="SeedPending"/>.</summary>
	readonly List<Pending> _pending = new();

	/// <summary>One accessory's starting transform, with two paths. <see cref="TunedFrame"/> plus
	/// <see cref="TunedPosition"/> are the values the owner dialled in on the stock citizen and are applied
	/// verbatim when the mount resolves to exactly that bone. <see cref="BodySeed"/> is the generic
	/// fallback for every other outcome, because bone-local numbers mean nothing on a rig they were not
	/// measured on.</summary>
	readonly record struct Pending(
		GameObject Accessory,
		CharacterAttachPoint Mount,
		string TunedFrame,
		Vector3 TunedPosition,
		Angles TunedAngles,
		float TunedScale,
		Vector3 BodySeed );

	/// <summary>One primitive making up an accessory's silhouette. <see cref="SizeUnits"/> is the part's
	/// intended size in ENGINE UNITS per axis; the builder divides it by the model's own bounds to get the
	/// scale, so the numbers below read as real dimensions rather than model-relative multipliers.</summary>
	readonly record struct Part( string ModelPath, Vector3 SizeUnits, Vector3 LocalPosition );

	GameObject _citizen;

	protected override void OnStart()
	{
		BuildCatalog();
		_citizen = BuildCitizen();
		BuildAccessories( _citizen );
		BuildUi();

		Log.Info( "[placement] demo ready. The tweak panel is open, P toggles it, right mouse orbits, B enters ghost placement." );
	}

	protected override void OnUpdate() => SeedPending();

	// ---- the ghost-placement catalog (the second beat) ----

	void BuildCatalog()
	{
		var cat = PlacementCatalog.Instance ?? Components.GetOrCreate<PlacementCatalog>();
		cat.Entries.Clear();
		cat.Entries.Add( new PlaceableEntry( "box", "Box", "models/dev/box.vmdl" ) );
		cat.Entries.Add( new PlaceableEntry( "sphere", "Sphere", "models/dev/sphere.vmdl" ) );
		cat.Entries.Add( new PlaceableEntry( "plane", "Plane", "models/dev/plane.vmdl" ) );

		// Demo validity seam: only allow placement within DemoValidRadius of the origin (illustrates the
		// green/red ghost tint). Replace or clear this in your own project.
		if ( DemoValidRadius > 0f )
			cat.ValidityCheck = ( pos, rot ) => pos.WithZ( 0f ).Length <= DemoValidRadius;
	}

	// ---- the character ----

	GameObject BuildCitizen()
	{
		var go = Scene.CreateObject();
		go.Name = "Citizen";
		go.WorldPosition = Vector3.Zero;
		go.WorldRotation = Rotation.FromYaw( CitizenYaw );

		var renderer = go.Components.Create<SkinnedModelRenderer>();
		var model = Model.Load( CitizenModel );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"[placement] demo citizen model '{CitizenModel}' did not load; the mounts will fall back to the root." );
			return go;
		}
		renderer.Model = model;
		Dress( renderer );

		// Standing idle straight off the citizen animgraph: grounded with no move input is its rest state,
		// which breathes and shifts weight a little. That subtle motion is the point here, because an
		// accessory that only looks right on a frozen T-pose is not actually fitted.
		renderer.Set( "b_grounded", true );

		// holdtype 6 is Swing on the citizen animgraph: a one-handed closed fist, so the hand actually grips
		// the tool instead of leaving a flat open palm under it. holdtype_handedness 1 is the right hand.
		renderer.Set( "holdtype", 6 );
		renderer.Set( "holdtype_handedness", 1 );

		return go;
	}

	static void Dress( SkinnedModelRenderer renderer )
	{
		var outfit = new ClothingContainer();
		bool any = false;
		foreach ( var path in Outfit )
		{
			var item = ResourceLibrary.Get<Clothing>( path );
			if ( item is null )
			{
				Log.Warning( $"[placement] demo clothing '{path}' did not resolve, skipping that slot." );
				continue;
			}
			outfit.Add( item );
			any = true;
		}
		if ( any ) outfit.Apply( renderer );
	}

	// ---- the three accessories ----

	void BuildAccessories( GameObject citizen )
	{
		var session = TweakSession.Instance ?? Components.GetOrCreate<TweakSession>();

		// OBSERVED ON THE SHIPPED CITIZEN (2026-07-30): none of the attachment names below resolved, and all
		// three mounts landed on their bone backstop (hand_R, head, spine_2). The attachment candidates stay
		// in the list because they are correct on rigs that do expose them and they exercise the
		// attachment-first path, but the bones are what actually carries this demo.

		// Right hand. hold_R is the usual weapon-hold attachment name; the casing varies between models.
		var hand = BuildMount( citizen, "Hand Mount",
			attachments: new List<string> { "hold_R", "hold_r" },
			bones: new List<string> { "hand_R", "hand_r" } );

		// Head. "hat" is the usual head-top attachment point; the head bone is the backstop that resolves here.
		var head = BuildMount( citizen, "Head Mount",
			attachments: new List<string> { "hat", "head" },
			bones: new List<string> { "head" } );

		// Upper spine. No attachment here, so this one exercises the bone path: the mount is re-pinned to
		// spine_2 every frame, which is what makes a back-worn item bob with the animation instead of
		// sliding along the root.
		var back = BuildMount( citizen, "Back Mount",
			attachments: new List<string>(),
			bones: new List<string> { "spine_2", "spine_1", "spine_0", "spine" } );

		// SILHOUETTES, and the axis they are built along. All three tuned offsets came back with their large
		// component on local X (hand +5.5, head +15, spine +1.6 with the big move on Y), which is the bone
		// chain running +X down its length: out past the fingers, up out of the skull, up the spine. So each
		// accessory is authored with its long axis on X. If a rig ever disagrees, the panel's rotation rows
		// fix it in one drag; nothing here depends on the guess being right.
		//
		// SIZES are the ones the owner settled on, in engine units on the longest axis: tool 5, hat 7,
		// pack 14. See BuildPart for why they are written as dimensions rather than scale factors.

		// Tool: a handle with a heavier head on the end, reading along +X (out past the fingers).
		Register( session, hand, "Hand Tool",
			tunedFrame: "citizen/hand_R",
			tunedPosition: new Vector3( 4.25f, 0.5f, -2.5f ),
			tunedAngles: new Angles( 5f, 91f, 5f ),
			tunedScale: 1.9f,
			bodySeed: new Vector3( 5f, 0f, -3f ),
			tint: new Color( 1f, 0.62f, 0.25f ),
			parts: new[]
			{
				new Part( "models/dev/box.vmdl", new Vector3( 3.4f, 1f, 1f ), new Vector3( -0.8f, 0f, 0f ) ),
				new Part( "models/dev/box.vmdl", new Vector3( 1.6f, 1.7f, 1.7f ), new Vector3( 1.7f, 0f, 0f ) ),
			} );

		// Hat: a flat brim disk with a dome crown above it. No cylinder ships in models/dev, so both are
		// squashed spheres; the brim is thin on X (the up-out-of-the-skull axis) and round across Y and Z.
		Register( session, head, "Hat",
			tunedFrame: "citizen/head",
			tunedPosition: new Vector3( 15f, 1.901744f, 0.000369f ),
			tunedAngles: new Angles( 0f, 0f, 0f ),
			tunedScale: 1f,
			bodySeed: new Vector3( 2f, 0f, 6f ),
			tint: new Color( 0.42f, 0.86f, 1f ),
			parts: new[]
			{
				new Part( "models/dev/sphere.vmdl", new Vector3( 1.6f, 7f, 7f ), new Vector3( 0f, 0f, 0f ) ),
				new Part( "models/dev/sphere.vmdl", new Vector3( 2.6f, 4.2f, 4.2f ), new Vector3( 2f, 0f, 0f ) ),
			} );

		// Pack: the box was close, so this is only a proportion change, taller up the spine than it is deep
		// off the back. Kept chunky rather than slab-thin so it still reads as a pack under any axis order.
		Register( session, back, "Back Pack",
			tunedFrame: "citizen/spine_2",
			tunedPosition: new Vector3( 1.625522f, -9.075111f, -0.00035f ),
			tunedAngles: new Angles( 0f, 0f, 0f ),
			tunedScale: 1f,
			bodySeed: new Vector3( -9f, 0f, 2f ),
			tint: new Color( 0.85f, 0.45f, 0.95f ),
			parts: new[]
			{
				new Part( "models/dev/box.vmdl", new Vector3( 14f, 7f, 11f ), new Vector3( 0f, 0f, 0f ) ),
			} );
	}

	CharacterAttachPoint BuildMount( GameObject citizen, string name, List<string> attachments, List<string> bones )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.SetParent( citizen, false );
		go.LocalPosition = Vector3.Zero;
		go.LocalRotation = Rotation.Identity;

		var mount = go.Components.Create<CharacterAttachPoint>();
		mount.AttachmentNames = attachments;
		mount.BoneNames = bones;
		mount.CharacterName = "citizen";
		return mount;
	}

	/// <summary>
	/// Build one accessory under a mount and register it for tweaking. The root is a bare GameObject: it
	/// owns the offset the panel edits and a uniform scale that starts at 1, and nothing else. The parts
	/// hang under it carrying the models and the proportions.
	///
	/// The starting transform is applied later, once the mount resolves. See <see cref="SeedPending"/>.
	/// </summary>
	void Register( TweakSession session, CharacterAttachPoint mount, string label,
		string tunedFrame, Vector3 tunedPosition, Angles tunedAngles, float tunedScale, Vector3 bodySeed,
		Color tint, Part[] parts )
	{
		var go = Scene.CreateObject();
		go.Name = label;
		go.SetParent( mount.GameObject, false );
		go.LocalPosition = Vector3.Zero;
		go.LocalRotation = Rotation.Identity;
		go.LocalScale = Vector3.One;   // the panel's Scale row is a multiplier on the authored size, so 1 is "as built"

		for ( int i = 0; i < parts.Length; i++ )
			BuildPart( go, $"{label} part {i + 1}", parts[i], tint );

		session.Add( new AttachedTweakTarget( go, mount, label ) );
		_pending.Add( new Pending( go, mount, tunedFrame, tunedPosition, tunedAngles, tunedScale, bodySeed ) );
	}

	/// <summary>
	/// One primitive under an accessory root, sized in engine units.
	///
	/// The scale is the requested size divided by the model's OWN bounds, per axis, rather than a
	/// hand-tuned multiplier. That keeps the part table readable as real dimensions and survives the engine
	/// changing what a dev primitive measures: the shipped box is 50 units and the sphere is 64, and
	/// nothing here has to know that.
	/// </summary>
	void BuildPart( GameObject parent, string name, Part part, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.SetParent( parent, false );
		go.LocalPosition = part.LocalPosition;
		go.LocalRotation = Rotation.Identity;

		var renderer = go.Components.Create<ModelRenderer>();
		var model = Model.Load( part.ModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"[placement] demo part model '{part.ModelPath}' did not load; '{name}' will be invisible." );
			return;
		}
		renderer.Model = model;

		var bounds = model.Bounds.Size;
		go.LocalScale = new Vector3(
			bounds.x > 0.001f ? part.SizeUnits.x / bounds.x : 1f,
			bounds.y > 0.001f ? part.SizeUnits.y / bounds.y : 1f,
			bounds.z > 0.001f ? part.SizeUnits.z / bounds.z : 1f );

		// The engine's models/dev primitives render as missing-material magenta unless a real material is
		// forced on, which would swallow the tint that tells the three accessories apart.
		var mat = Material.Load( FallbackMaterial );
		if ( mat is not null ) renderer.MaterialOverride = mat;
		renderer.Tint = tint;
	}

	/// <summary>
	/// Apply each accessory's starting transform once its mount has resolved, then re-baseline it as the
	/// authored default so the panel's Reset comes back here.
	///
	/// Two paths, and which one runs depends on what the rig gave us:
	///
	///  - The mount landed on exactly the bone the demo values were measured against: apply them verbatim.
	///    These are real numbers, dialled in on the stock citizen, so the demo opens already fitted and the
	///    first thing you see is the finished result rather than three primitives in a heap.
	///  - Anything else, including the character-root fallback and any renamed or substituted bone: fall
	///    back to a seed expressed in the CHARACTER's frame (x forward, y left, z up) and let the engine
	///    convert it to local. Bone-local numbers are only valid on the rig they were measured on; reusing
	///    them elsewhere buries an accessory in the chest on one rig and flings it into orbit on the next.
	///    The character frame is not accurate, but it is always visible, which is all a fallback owes you.
	///
	/// The frame test requires a BONE mount, not just a matching name. An attachment that happens to be
	/// called "head" is a different transform from the head bone, and the tuned numbers would be wrong on it.
	/// </summary>
	void SeedPending()
	{
		if ( _pending.Count == 0 ) return;

		var session = TweakSession.Instance;
		var bodyRot = _citizen.IsValid() ? _citizen.WorldRotation : Rotation.Identity;

		for ( int i = _pending.Count - 1; i >= 0; i-- )
		{
			var p = _pending[i];
			if ( !p.Accessory.IsValid() || !p.Mount.IsValid() ) { _pending.RemoveAt( i ); continue; }
			if ( p.Mount.Kind == CharacterAttachPoint.MountKind.Unresolved ) continue;

			bool tuned = p.Mount.Kind == CharacterAttachPoint.MountKind.Bone
				&& p.Mount.FrameName == p.TunedFrame;

			if ( tuned )
			{
				p.Accessory.LocalPosition = p.TunedPosition;
				p.Accessory.LocalRotation = p.TunedAngles.ToRotation();
				p.Accessory.LocalScale = new Vector3( p.TunedScale, p.TunedScale, p.TunedScale );
				Log.Info( $"[placement] '{p.Accessory.Name}' seated at the tuned offset for {p.TunedFrame}" );
			}
			else
			{
				p.Accessory.WorldPosition = p.Mount.WorldPosition + bodyRot * p.BodySeed;
				Log.Info( $"[placement] '{p.Accessory.Name}' mounted on {p.Mount.FrameName}, not the tuned "
					+ $"{p.TunedFrame}; using the generic character-frame seed instead. Fit it with P." );
			}

			session?.CaptureSeed( p.Accessory );
			_pending.RemoveAt( i );
		}
	}

	// ---- screen UI ----

	void BuildUi()
	{
		// One ScreenPanel per PanelComponent (the World Builder UI idiom). Built in code so the demo scene
		// needs no razor wiring.
		var panelHost = Scene.CreateObject();
		panelHost.Name = "Placement UI";
		panelHost.Components.Create<ScreenPanel>();

		// Open on arrival. Fitting the accessories is what this scene is FOR, so making the visitor find the
		// key first is a toll booth on the way to the point. P and the header x still close it. The panel
		// reads this on its first update, after every OnStart in the frame, so setting it here always lands.
		var panel = panelHost.Components.Create<TweakPanel>();
		panel.OpenOnStart = true;

		var hintHost = Scene.CreateObject();
		hintHost.Name = "Placement Hint";
		hintHost.Components.Create<ScreenPanel>();
		hintHost.Components.Create<DemoHintCard>();
	}
}
