using System;

public class MySpringJoint : Component
{
	[RequireComponent] public Rigidbody Body { get; private set; }
	[Property] public Rigidbody Other {  get; private set; }

	public void ConnectTo(Rigidbody body, bool twoWay = false )
	{
		Other = body;
		WantedDistance = Vector3.DistanceBetween( this.WorldPosition, Other.WorldPosition );
		this.TwoWay = twoWay;
	}

	private Vector3 WantedPos;

	[Property] public float WantedDistance { get; private set; }

	[Property][Range(0f, 1f)] public float Damping { get; set; } = 1f;
	[Property] public float Stiffness { get; set; } = 20f;

	[Property] float? MaxStretchPercentage { get; set; } = 0.5f;

	[Property] public bool Draw { get; set; } = true;

	/// <summary>
	/// Will this spring move both objects towards eachother or just other towards this?
	/// Useful to disable if both objects have springs on eachother.
	/// </summary>
	[Property] bool TwoWay { get; set; } = true;

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

		if(TwoWay)
		{
			ConstrainBodies( Other, Body );
		}
	}

	void ConstrainBodies(Rigidbody from, Rigidbody to)
	{

		Vector3 vec = to.WorldPosition - from.WorldPosition;
		Vector3 dir = vec.Normal;
		float dst = vec.Length;

		WantedPos = this.WorldPosition + dir * WantedDistance;

		// How off is current pos from wanted pos?
		float offset = (WantedDistance - dst);

		// hooke's law
		float springForce = Stiffness * offset;

		float dot = Vector3.Dot( to.Velocity, dir );
		float damperForce = dot * -Damping;

		float mult = TwoWay ? 0.5f : 1f;
		to.ApplyForce( dir * springForce * to.PhysicsBody.Mass * mult );
		to.ApplyForce( dir * damperForce * to.PhysicsBody.Mass * mult );

		if ( Vector3.DistanceBetween( from.WorldPosition, to.WorldPosition ) < WantedDistance )
		{
			//to.ApplyForce( ( to.WorldPosition - (from.WorldPosition + dir * WantedDistance) ) * to.PhysicsBody.Mass );
			//to.ApplyForce( MathF.Abs( dot ) * to.Velocity.Length * dir * to.PhysicsBody.Mass );
		}

	}

	protected override void OnPreRender()
	{
		base.OnUpdate();

		if(!Draw) { return; }
		if(Other == null) { return; }

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( WorldPosition, Other.WorldPosition );

		return;

		//Gizmo.Draw.LineSphere( WantedPos, 10 );
		Gizmo.Draw.Color = Color.Red;

		Vector3 vec = Other.WorldPosition - WorldPosition;
		Vector3 dir = vec.Normal;
		float offset = (WantedDistance - vec.Length);
		Gizmo.Draw.Line( Other.WorldPosition, Other.WorldPosition + dir * offset );

	}

}
