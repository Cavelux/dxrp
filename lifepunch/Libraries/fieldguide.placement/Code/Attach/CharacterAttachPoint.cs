using Sandbox;
using System.Collections.Generic;

namespace FieldGuide.Placement;

/// <summary>
/// A mount frame on a skinned character: this GameObject rides a named ATTACHMENT (a hand hold point,
/// a hat point) or, failing that, a named BONE, so anything parented under it inherits that frame and
/// can be positioned with a plain LocalPosition / LocalRotation.
///
/// This is the piece the accessory-fitting flow is built on. Put this component on an empty child of
/// the character, list the attachment and bone names you want in preference order, then parent your
/// model under it and register the model with the <see cref="TweakSession"/>. The offsets you drag in
/// the panel are then genuinely relative to the hand or the spine, which is what
/// <see cref="PlacedOffset"/> bakes and what your game code will reproduce.
///
/// Resolution is DEFERRED and RETRIED, not done once at start: a skinned model created this frame has no
/// attachment objects yet, and reading a bone before the first pose evaluates gives the bind pose. The
/// component keeps trying for <see cref="ResolveTimeout"/> seconds, then settles on the character root
/// and says so in the console. A renamed or missing attachment therefore degrades to a visible,
/// diagnosable fallback instead of a null reference.
/// </summary>
[Title( "Character Attach Point" )]
[Category( "Field Guide · Placement" )]
[Icon( "accessibility" )]
public sealed class CharacterAttachPoint : Component
{
	/// <summary>How the mount ended up anchored, once resolution settles.</summary>
	public enum MountKind
	{
		/// <summary>Still looking (the model may not have posed yet).</summary>
		Unresolved,
		/// <summary>Parented to a model attachment object; the engine drives it.</summary>
		Attachment,
		/// <summary>Re-pinned to a bone transform every frame before render.</summary>
		Bone,
		/// <summary>Nothing resolved; sitting on the character root so the accessory is still visible.</summary>
		Root,
	}

	/// <summary>The skinned model to mount against. Left null, the component searches its ancestors and
	/// then their descendants, which covers "empty child of the character GameObject".</summary>
	[Property] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>Attachment names to try, in order. Casing varies between models, so list both forms
	/// (for example "hold_R" then "hold_r"). Tried before <see cref="BoneNames"/>.</summary>
	[Property] public List<string> AttachmentNames { get; set; } = new();

	/// <summary>Bone names to try if no attachment resolves. A bone mount is re-pinned every frame, so the
	/// accessory bobs and leans with the animation instead of sliding with the root.</summary>
	[Property] public List<string> BoneNames { get; set; } = new();

	/// <summary>Label for the character in the frame string, e.g. "citizen" gives "citizen/hold_R". This is
	/// what ends up in the export and the bake comment, so name it the way your code talks about it.</summary>
	[Property] public string CharacterName { get; set; } = "character";

	/// <summary>Seconds to keep retrying before giving up and falling back to the character root.</summary>
	[Property] public float ResolveTimeout { get; set; } = 3f;

	/// <summary>How this mount is anchored right now.</summary>
	public MountKind Kind { get; private set; } = MountKind.Unresolved;

	/// <summary>The attachment or bone name that actually resolved, or "root" on the fallback.</summary>
	public string ResolvedName { get; private set; } = "root";

	/// <summary>The frame string to hand <see cref="TweakSession.Add(GameObject, string, string, TweakRanges)"/>:
	/// "citizen/hold_R", "citizen/spine_2", "citizen/root". Safe to read before resolution settles; it just
	/// reports the fallback until then.</summary>
	public string FrameName => $"{CharacterName}/{ResolvedName}";

	float _elapsed;
	bool _gaveUp;

	protected override void OnStart()
	{
		Renderer ??= Components.GetInAncestorsOrSelf<SkinnedModelRenderer>()
			?? Components.GetInDescendantsOrSelf<SkinnedModelRenderer>( false );
		TryResolve();
	}

	protected override void OnUpdate()
	{
		if ( Kind is MountKind.Attachment or MountKind.Bone ) return;

		_elapsed += Time.Delta;
		if ( TryResolve() ) return;

		if ( _gaveUp || _elapsed < ResolveTimeout ) return;

		_gaveUp = true;
		Kind = MountKind.Root;
		ResolvedName = "root";
		Log.Warning( $"[placement] attach point '{GameObject?.Name}' resolved no attachment {Names( AttachmentNames )} "
			+ $"and no bone {Names( BoneNames )} on the model; falling back to the character root." );
	}

	/// <summary>A bone mount is re-pinned here rather than in OnUpdate: PreRender runs after the pose is
	/// evaluated, so the accessory sits on the bone position that will actually be drawn this frame.</summary>
	protected override void OnPreRender()
	{
		if ( Kind != MountKind.Bone || !Renderer.IsValid() ) return;
		if ( !Renderer.TryGetBoneTransform( ResolvedName, out Transform t ) ) return;
		WorldPosition = t.Position;
		WorldRotation = t.Rotation;
	}

	/// <summary>One resolution attempt. Attachments win (the engine parents and drives them for free);
	/// bones are the fallback that still tracks the animation. Returns true once anchored.</summary>
	bool TryResolve()
	{
		if ( !Renderer.IsValid() ) return false;

		foreach ( var name in AttachmentNames )
		{
			if ( string.IsNullOrEmpty( name ) ) continue;
			var mount = Renderer.GetAttachmentObject( name );
			if ( mount is null || !mount.IsValid() ) continue;

			GameObject.SetParent( mount, false );
			GameObject.LocalPosition = Vector3.Zero;
			GameObject.LocalRotation = Rotation.Identity;
			Kind = MountKind.Attachment;
			ResolvedName = name;
			Log.Info( $"[placement] attach point '{GameObject.Name}' mounted on attachment {FrameName}" );
			return true;
		}

		foreach ( var name in BoneNames )
		{
			if ( string.IsNullOrEmpty( name ) ) continue;
			if ( !Renderer.TryGetBoneTransform( name, out Transform t ) ) continue;

			WorldPosition = t.Position;
			WorldRotation = t.Rotation;
			Kind = MountKind.Bone;
			ResolvedName = name;
			Log.Info( $"[placement] attach point '{GameObject.Name}' mounted on bone {FrameName} (re-pinned each frame)" );
			return true;
		}

		return false;
	}

	static string Names( List<string> names )
		=> names is null || names.Count == 0 ? "(none listed)" : "[" + string.Join( ", ", names ) + "]";
}
