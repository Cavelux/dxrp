using System.Linq;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// The demo scene's stand-in player: a citizen that slides along the ground on the movement stick (or
/// W/A/S/D) and hops on the jump action. Deliberately the simplest thing that can be coached: plain
/// transform movement, one hand-integrated hop, no rigidbody, no collider, no controller. It exists so
/// the tips in <c>Assets/demo/</c> have real actions to retire on.
///
/// THE LOOK. The scene authors this object with a plain box renderer. At boot the pawn switches that off
/// and builds a dressed stock citizen in its place (<see cref="TipsDemoCitizen"/>), so the scene file
/// stays as authored and the editor viewport still shows the simple block when nothing is playing. The
/// movement, the play radius and the hop are untouched by the swap: only the visual changed.
///
/// Not part of the kit's runtime surface: delete <c>Code/Demo</c> and <c>Assets/demo</c> when you drop
/// the kit into your own project, and coach your own player instead.
/// </summary>
[Title( "Tips Demo Pawn" )]
[Category( "Field Guide Tips" )]
[Icon( "smart_toy" )]
public sealed class TipsDemoPawn : Component
{
	/// <summary>Ground speed in world units per second.</summary>
	[Property] public float MoveSpeed { get; set; } = 220f;

	/// <summary>Upward speed of one hop, in world units per second.</summary>
	[Property] public float JumpSpeed { get; set; } = 260f;

	/// <summary>Downward acceleration applied to a hop, in world units per second squared.</summary>
	[Property] public float Gravity { get; set; } = 900f;

	/// <summary>How far from its start the citizen may wander. The demo camera is fixed, so this is what
	/// keeps the citizen in frame, and the scene's camera is framed to contain exactly this disc. The
	/// marker sits 205 units from the start, so 220 lets the citizen walk onto it and a little past
	/// without opening up a corner of the yard that the camera would then have to cover for nothing.</summary>
	[Property] public float PlayRadius { get; set; } = 220f;

	/// <summary>The Input.config action that hops. Bound to Space on a keyboard and A on a pad in the
	/// s&amp;box default config, which is what the demo tips prompt.</summary>
	[Property] public string JumpAction { get; set; } = "Jump";

	/// <summary>Yaw the demo camera looks along, so pushing forward moves the citizen away from the camera
	/// instead of sideways. Change it with the camera.</summary>
	[Property] public float CameraYaw { get; set; } = 45f;

	/// <summary>Local Z of the citizen visual. The pawn object sits half a block above the ground because
	/// the authored box is centred on it, and the citizen's origin is at its feet, so the visual drops by
	/// that half height to stand on the floor instead of hovering.</summary>
	[Property] public float VisualZOffset { get; set; } = -25f;

	/// <summary>How briskly the citizen turns to face where it is going, in turns per second-ish. High
	/// enough to read as responsive, low enough that a flick of the stick does not snap it.</summary>
	[Property] public float TurnSpeed { get; set; } = 12f;

	private Vector3 _start;
	private float _height;
	private float _riseSpeed;
	private SkinnedModelRenderer _visual;
	private Rotation _facing;

	protected override void OnStart()
	{
		_start = WorldPosition;

		// Resting yaw looks back down the camera's line, so the citizen greets the player instead of
		// showing its back on the first frame.
		_facing = Rotation.FromYaw( CameraYaw + 180f );

		HideAuthoredBlock();

		_visual = TipsDemoCitizen.Build( GameObject, VisualZOffset );
		if ( _visual.IsValid() )
		{
			_visual.WorldRotation = _facing;
			Log.Info( "[tips] demo pawn: dressed citizen built in code, authored block renderer switched off." );
		}
	}

	protected override void OnUpdate()
	{
		var facing = Rotation.FromYaw( CameraYaw );
		var move = ReadMove();
		var dir = facing.Forward * move.x + facing.Left * move.y;
		if ( dir.Length > 1f )
			dir = dir.Normal;

		var flat = ( WorldPosition + dir * MoveSpeed * Time.Delta - _start ).WithZ( 0f );
		if ( flat.Length > PlayRadius )
			flat = flat.Normal * PlayRadius;

		var hopped = false;
		if ( _height <= 0f && _riseSpeed <= 0f && Input.Pressed( JumpAction ) )
		{
			_riseSpeed = JumpSpeed;
			hopped = true;
		}

		if ( _height > 0f || _riseSpeed > 0f )
		{
			_riseSpeed -= Gravity * Time.Delta;
			_height += _riseSpeed * Time.Delta;
			if ( _height <= 0f )
			{
				_height = 0f;
				_riseSpeed = 0f;
			}
		}

		var previous = WorldPosition;
		WorldPosition = _start + flat + Vector3.Up * _height;

		DriveVisual( previous, dir, hopped );
	}

	/// <summary>
	/// Movement as a forward/left pair. <c>Input.AnalogMove</c> carries the movement stick and, in a
	/// project whose Input.config binds the standard movement actions, the keyboard too. The raw W/A/S/D
	/// fallback keeps the demo drivable in a project that binds movement under other names, which is the
	/// same reason the movement tip completes on either the stick or those keys.
	/// </summary>
	private static Vector3 ReadMove()
	{
		var move = Input.AnalogMove;
		if ( move.Length > 0.01f )
			return move;

		var forward = ( Input.Keyboard.Down( "w" ) ? 1f : 0f ) - ( Input.Keyboard.Down( "s" ) ? 1f : 0f );
		var left = ( Input.Keyboard.Down( "a" ) ? 1f : 0f ) - ( Input.Keyboard.Down( "d" ) ? 1f : 0f );
		return new Vector3( forward, left, 0f );
	}

	/// <summary>
	/// Turn the citizen to face its travel and hand the animgraph a frame of locomotion. The legs read the
	/// distance actually covered rather than the stick, so at the play radius the clamp reads as standing
	/// still instead of running on the spot.
	/// </summary>
	private void DriveVisual( Vector3 previous, Vector3 wishDirection, bool hopped )
	{
		if ( !_visual.IsValid() )
			return;

		var travelled = ( WorldPosition - previous ).WithZ( 0f );
		var velocity = ( Time.Delta > 0f ? travelled / Time.Delta : Vector3.Zero ).WithZ( _riseSpeed );

		if ( travelled.Length > 0.01f )
		{
			var target = Rotation.LookAt( travelled.Normal, Vector3.Up );
			_facing = Rotation.Slerp( _facing, target, ( Time.Delta * TurnSpeed ).Clamp( 0f, 1f ) );
		}

		_visual.WorldRotation = _facing;

		var grounded = _height <= 0f && _riseSpeed <= 0f;
		TipsDemoCitizen.Drive( _visual, velocity, wishDirection * MoveSpeed, grounded );

		if ( hopped )
			TipsDemoCitizen.TriggerJump( _visual );
	}

	/// <summary>
	/// Switch off the box renderer the scene authors on this object, so the citizen stands alone. Disabling
	/// the component at runtime leaves the scene file untouched: reopen it in the editor and the simple
	/// authored block is still what you see. The skinned renderer is skipped by type because it derives
	/// from <c>ModelRenderer</c> too.
	/// </summary>
	private void HideAuthoredBlock()
	{
		var authored = Components.GetAll<ModelRenderer>( FindMode.EverythingInSelf )
			.Where( r => r is not SkinnedModelRenderer )
			.ToArray();

		foreach ( var renderer in authored )
			renderer.Enabled = false;
	}

	/// <summary>Put the citizen back where it started (the demo's replay key).</summary>
	public void ResetPawn()
	{
		_height = 0f;
		_riseSpeed = 0f;
		WorldPosition = _start;

		_facing = Rotation.FromYaw( CameraYaw + 180f );
		if ( _visual.IsValid() )
			_visual.WorldRotation = _facing;
	}
}
