using Sandbox;
using System;

namespace FieldGuide.Placement;

/// <summary>
/// Orbit / pan / zoom tool camera for scene authoring. Hold RIGHT mouse (the "attack2"
/// action, mouse2 by default) to orbit yaw/pitch around a ground-plane focus point; the
/// scroll wheel zooms (exponential, clamped); WASD pans the focus point relative to the
/// camera's current yaw; Q / E orbit the view left / right from the keyboard. The cursor
/// stays visible the whole time (MouseVisibility.Visible), because this is a cursor-driven
/// authoring camera, not an FPS look camera, so the manipulator UI stays clickable.
///
/// Ported near drop-in from World Builder's OrbitCamera. The two tuning constants it used to
/// read from a project-global god-object (units-per-meter and the keyboard orbit rate) are
/// now <see cref="UnitsPerMeter"/> and <see cref="OrbitKeyRotateDegS"/> [Property] fields with
/// the same defaults, so the component carries its own config and reaches into nothing else.
///
/// Distances and pan speeds are expressed in METERS and multiplied by <see cref="UnitsPerMeter"/>
/// to reach engine units. If your scene is authored directly in engine units (1 unit == 1),
/// set <see cref="UnitsPerMeter"/> to 1 and treat the "M" fields as plain units.
/// </summary>
[Title( "Orbit Camera" )]
[Category( "Field Guide · Placement" )]
[Icon( "videocam" )]
public sealed class OrbitCamera : Component
{
	[Property] public float MinPitch { get; set; } = 5f;
	[Property] public float MaxPitch { get; set; } = 85f;
	[Property] public float MinDistanceM { get; set; } = 10f;
	[Property] public float MaxDistanceM { get; set; } = 400f;
	[Property] public float OrbitSensitivity { get; set; } = 0.2f;
	[Property] public float ZoomExponent { get; set; } = 0.92f; // per wheel-notch multiplier
	[Property] public float PanSpeedM { get; set; } = 20f;      // meters/sec, scaled by distance

	/// <summary>Engine units per meter. World Builder read this from a global constant (39.37,
	/// one inch per unit); it is lifted here so the camera carries its own scale. Set to 1 to
	/// author directly in engine units.</summary>
	[Property] public float UnitsPerMeter { get; set; } = 39.37f;

	/// <summary>Degrees/second the Q / E keys sweep the view around the focus (keyboard yaw,
	/// composes with RMB-drag orbit and WASD pan). Lifted from the World Builder tuning global.</summary>
	[Property] public float OrbitKeyRotateDegS { get; set; } = 90f;

	/// <summary>Scene extent in meters, used to pick the starting zoom distance. Assign right after
	/// Components.Create (before OnStart fires) if you build the camera in code.</summary>
	[Property] public float WorldWidthM { get; set; } = 128f;

	/// <summary>Where the camera starts looking, in the same METERS the rest of this component uses.
	/// Zero (the ground origin) is right for a scene-authoring pass; raise the z to frame a subject that
	/// stands up off the ground, such as a character, instead of putting its feet in the middle of the
	/// view. WASD panning moves off from here.</summary>
	[Property] public Vector3 FocusStartM { get; set; } = Vector3.Zero;

	/// <summary>The camera's ground-plane focus point in METERS (the point it orbits/pans over),
	/// mirrored to a public static so an overlay can draw a "you are looking here" marker without
	/// reaching into a private field. Read-only to the outside; only this component writes it.</summary>
	public static Vector3 FocusWorldM { get; private set; }

	Vector3 _focusM;
	float _yaw;
	float _pitch;
	float _targetDistanceM;
	float _smoothDistanceM;

	protected override void OnStart()
	{
		// Cursor free and visible, exactly what a cursor-driven authoring camera wants.
		Mouse.Visibility = MouseVisibility.Visible;

		_focusM = FocusStartM;
		_yaw = 45f;
		_pitch = 30f;
		_targetDistanceM = (WorldWidthM * 0.9f).Clamp( MinDistanceM, MaxDistanceM );
		_smoothDistanceM = _targetDistanceM;

		Apply();
	}

	protected override void OnUpdate()
	{
		if ( Input.Down( "attack2" ) )
		{
			var delta = Mouse.Delta;
			_yaw -= delta.x * OrbitSensitivity;
			_pitch = (_pitch - delta.y * OrbitSensitivity).Clamp( MinPitch, MaxPitch );
		}

		// Q / E orbit the view around the focus (keyboard yaw). Raw keyboard reads, so the kit needs
		// no custom Input.config action for them. Sign matches the RMB-drag convention (yaw DECREASES
		// for a leftward sweep): Q sweeps the view LEFT, E sweeps it RIGHT.
		if ( Input.Keyboard.Down( "Q" ) ) _yaw += OrbitKeyRotateDegS * Time.Delta;
		if ( Input.Keyboard.Down( "E" ) ) _yaw -= OrbitKeyRotateDegS * Time.Delta;

		float wheel = Input.MouseWheel.y;
		if ( wheel != 0f )
			_targetDistanceM = (_targetDistanceM * MathF.Pow( ZoomExponent, wheel )).Clamp( MinDistanceM, MaxDistanceM );

		// MathX.Lerp (not MathF.Lerp, which doesn't exist here) smooths the zoom so scroll notches
		// don't snap the camera.
		_smoothDistanceM = MathX.Lerp( _smoothDistanceM, _targetDistanceM, Time.Delta * 8f );

		var yawRot = Rotation.FromYaw( _yaw );
		var move = Input.AnalogMove;
		float panSpeed = PanSpeedM * (_smoothDistanceM / 100f).Clamp( 0.25f, 4f );
		_focusM += (yawRot.Forward * move.x + yawRot.Left * move.y) * panSpeed * Time.Delta;

		Apply();
	}

	void Apply()
	{
		FocusWorldM = _focusM;   // publish the orbit focus for an optional marker overlay
		var rot = new Angles( _pitch, _yaw, 0f ).ToRotation();
		var camPosM = _focusM - rot.Forward * _smoothDistanceM;
		WorldPosition = camPosM * UnitsPerMeter;
		WorldRotation = rot;
	}
}
