using System;

public class MySpringJoint : Component
{
	[RequireComponent] public Rigidbody Body { get; private set; }
	[Property] public Rigidbody Other {  get; private set; }

	public void ConnectTo(Rigidbody body, float connectionDistance = 0 )
	{
		Other = body;

		if ( connectionDistance == 0 )
		{
			WantedDistance = ( Other.WorldPosition - this.WorldPosition ).Length;
		}
		else
		{
			WantedDistance = connectionDistance;
		}

	}
	[Property] public float WantedDistance { get; set; }

	[Property][Range(0f, 1f)] public float Damping { get; set; } = 1f;
	[Property] public float Stiffness { get; set; } = 20f;
	[Property][Range( 1f, 10f )] public float MaxStretch { get; set; } = 1.5f;

	[Property] public bool Draw { get; set; } = true;

	public Color GizmoColor { get; set; } = Color.White;

	protected override void OnAwake()
	{
		base.OnAwake();

		if ( Other == null ) { return; }

		WantedDistance = ( this.WorldPosition - Other.WorldPosition ).Length;

	}

	protected override void OnFixedUpdate()
	{
		if(Other == null) { return; }

		ConstrainBodies( Body, Other );
		//ConstrainBodies( Other, Body );
	}

	Vector3 WantedPos;
	void ConstrainBodies(Rigidbody from, Rigidbody to)
	{

		Vector3 vec = to.WorldPosition - from.WorldPosition;
		Vector3 dir = vec.Normal;
		float dst = vec.Length;

		WantedPos = from.WorldPosition + dir * WantedDistance;

		// How off is current pos from wanted pos?
		float stretch = WantedDistance - dst;

		// hooke's law
		Vector3 springForce = dir * (Stiffness * stretch);

		float relativeVelocity = Vector3.Dot( (to.Velocity - from.Velocity) * Time.Delta, dir );
		Vector3 damperForce = dir * (-Damping * relativeVelocity);

		// Apply force proportional to the mass ratio
		float massRatio = (from.PhysicsBody.Mass / (from.PhysicsBody.Mass + to.PhysicsBody.Mass));
		to.ApplyForce( ( springForce + damperForce ) * massRatio );

		float maxDistance = WantedDistance * MaxStretch; 
		if ( dst > maxDistance )
		{
			// Move 'to' closer to 'from'
			Vector3 clampedPosition = from.WorldPosition + dir * maxDistance;
			to.Velocity += to.WorldPosition - clampedPosition;
			//to.ApplyForce( to.WorldPosition - clampedPosition );
			//to.WorldPosition = clampedPosition;
		}

	}

	protected override void OnPreRender()
	{
		base.OnUpdate();

		if(!Draw) { return; }
		if(Other == null) { return; }

		Vector3 vec = Other.WorldPosition - WorldPosition;
		Vector3 dir = vec.Normal;

		Gizmo.Draw.Color = GizmoColor;
		Gizmo.Draw.Line( WorldPosition, WorldPosition + dir * 2);

		return;

		//Gizmo.Draw.LineSphere( WantedPos, 10 );
		Gizmo.Draw.Color = Color.Red;

		float offset = (WantedDistance - vec.Length);
		Gizmo.Draw.Line( Other.WorldPosition, Other.WorldPosition + dir * offset );

	}

}
