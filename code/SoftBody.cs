using System;
using System.Diagnostics;

namespace Saandy;

public sealed class SoftBody : Component
{
	[Flags]
	public enum DebugFlags
	{
		Springs = 1,
		ShellParticles = 2,
		FillParticles = 4
	}

	
	[Category("Debug")][Property][Change] public DebugFlags DebugDrawFlags { get; set; }
	
	[RequireComponent] public ModelRenderer Renderer { get; private set; }

	private string ParticlePath = "prefabs/softbody_particle.prefab";

	List<Rigidbody> Particles { get; set; }
	List<uint> VertexIds;

	ComputeBuffer<Vector4> PointsBuffer;
	ComputeBuffer<uint> IDBuffer;

	int VertexParticleCount = 0;

	[Property][Change] public bool Gravity { get; set; } = true;
	void OnGravityChanged( bool oldValue, bool newValue )
	{

		if ( !Game.IsPlaying ) { return; }
		if ( Particles?.Count == 0 ) { return; }

		foreach ( Rigidbody particle in Particles )
		{
			particle.Gravity = newValue;
			particle.PhysicsBody.Sleeping = false;
		}

	}

	[Property][Change][Range( 1f, 50000f )] public float? MassOverride { get; set; }
	void OnMassOverrideChanged( float? oldValue, float? newValue )
	{
		if ( Particles == null ) { return; }

		float value = newValue.HasValue ? newValue.Value : 0;
		foreach ( Rigidbody particle in Particles )
		{
			particle.MassOverride = value;
		}
	}

	[Category( "Characteristics" )]
	[Property] public float ConnectionDistance { get; set; } = 400;

	[Category( "Characteristics" )]
	[Property] public float ParticleRadius { get; set; } = 2f;

	[Category( "Characteristics" )]
	[Property][Change] public float Stiffness { get; set; } = 700;
	void OnStiffnessChanged( float oldValue, float newValue )
	{
		if ( Particles == null ) { return; }

		foreach ( Rigidbody particle in Particles )
		{
			IEnumerable<MySpringJoint> joints = particle.Components.GetAll<MySpringJoint>();

			foreach ( MySpringJoint joint in joints )
			{
				joint.Stiffness = newValue;
			}

		}
	}

	[Category( "Characteristics" )]
	[Property][Change][Range( 0f, 200f )] public float SpringDamping { get; set; } = 0f;
	void OnSpringDampingChanged( float oldValue, float newValue )
	{
		if ( Particles == null ) { return; }

		foreach ( Rigidbody particle in Particles )
		{
			IEnumerable<MySpringJoint> joints = particle.Components.GetAll<MySpringJoint>();

			foreach ( MySpringJoint joint in joints )
			{
				joint.Damping = newValue;
			}

		}
	}

	[Category( "Characteristics" )]
	[Property][Change] public float LinearDamping { get; set; } = 3f;
	void OnLinearDampingChanged( float oldValue, float newValue )
	{
		if ( Particles == null ) { return; }

		foreach ( Rigidbody particle in Particles )
		{
			particle.LinearDamping = newValue;
		}
	}

	[Category( "Characteristics" )]
	[Property][Change] public float AngularDamping { get; set; } = 1f;
	void OnAngularDampingChanged( float oldValue, float newValue )
	{
		if ( Particles == null ) { return; }

		foreach ( Rigidbody particle in Particles )
		{
			particle.AngularDamping = newValue;
		}
	}

	[Category( "Characteristics" )]
	[Property][Change][Range(1f, 10f)] public float MaxStretch { get; set; } = 1.5f;
	void OnMaxStretchChanged( float oldValue, float newValue )
	{
		if ( Particles == null ) { return; }

		foreach ( Rigidbody particle in Particles )
		{
			IEnumerable<MySpringJoint> joints = particle.Components.GetAll<MySpringJoint>();

			foreach ( MySpringJoint joint in joints )
			{
				joint.MaxStretch = newValue;
			}

		}
	}

	[Category( "Characteristics" )]
	[Property][Change] public PhysicsLock Locking { get; set; }
	void OnLockingChanged( PhysicsLock oldValue, PhysicsLock newValue )
	{
		if ( Particles == null ) { return; }

		foreach ( Rigidbody particle in Particles )
		{
			particle.Locking = Locking;
		}
	}

	void OnDebugDrawFlagsChanged( DebugFlags oldValue, DebugFlags newValue)
	{
		if ( Particles == null ) { return; }

		foreach ( Rigidbody particle in Particles )
		{

			bool drawSpring = DebugDrawFlags.HasFlag( DebugFlags.Springs );
			IEnumerable<MySpringJoint> springs = particle.Components.GetAll<MySpringJoint>();
			foreach ( MySpringJoint spring in springs )
			{

				spring.Draw = drawSpring;
			}

		}
	}

	void Create()
	{
		DestroyParticles();

		// TODO: Rotation shouldn't matter. Right now particles will be created outside of the mesh if it is rotated.
		WorldRotation = Rotation.Identity;

		Renderer = GetComponent<ModelRenderer>();

		List<Vector3> positions = GetUniqueVertices();
		VertexParticleCount = positions.Count;

		DestroyParticles();
		Particles = new();

		for ( int i = 0; i < positions.Count; i++ )
		{
			CreateParticle( positions[i] );
		}

		FillWithParticles();

		ConnectParticles();

		PointsBuffer = new ComputeBuffer<Vector4>( VertexParticleCount, ComputeBufferType.Structured );

		IDBuffer = new ComputeBuffer<uint>( VertexIds.Count, ComputeBufferType.Structured );
		IDBuffer.SetData( VertexIds );
		Renderer.SceneObject.Attributes.Set( "_IDs", IDBuffer );

	}

	protected override void OnStart()
	{
		base.OnStart();

		Create();

	}

	List<Vector3> GetUniqueVertices()
	{

		Sandbox.Vertex[] allVertices = Renderer.Model.GetVertices();

		List<Vector3> positions = new();
		
		VertexIds = new();

		HashSet<Vector3> points = new();

		for ( int i = 0; i < allVertices.Length; i++ )
		{

			// Is this position already counted for?
			if ( !points.Add( allVertices[i].Position ) )
			{
				int id = positions.TakeWhile( x => x != allVertices[i].Position ).Count();

				VertexIds.Add( (uint)id );
			}
			else
			{
				VertexIds.Add( (uint)positions.Count );
				positions.Add( allVertices[i].Position );
			}

		}

		return positions;

	}

	void CreateParticle( Vector3 position )
	{
		Rigidbody particle = PrefabScene.Clone( ParticlePath ).Components.Get<Rigidbody>();
		particle.Gravity = Gravity;
		particle.WorldPosition = WorldPosition + position;
		particle.GameObject.BreakFromPrefab();
		particle.MassOverride = MassOverride.HasValue ? MassOverride.Value : 0f;
		particle.Components.Get<SphereCollider>().Radius = ParticleRadius;
		particle.Locking = Locking;
		
		particle.GameObject.Flags = GameObjectFlags.Hidden;
		particle.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;

		Particles.Add( particle );
	}

	void DestroyParticles()
	{
		if( Particles  == null) { return; }
		if(Particles.Count == 0) { return; }

		for(int i = Particles.Count-1; i > 0; i-- )
		{
			Particles[i].GameObject.Destroy();
		}
	}

	void ConnectParticles()
	{
		List<Vector3> dirs = new();

		foreach ( Rigidbody particle in Particles)
		{
			dirs.Clear();

			foreach( Rigidbody other in Particles)
			{
				if(particle == other) { continue; }

				Vector3 vec = other.WorldPosition - particle.WorldPosition;
				float dst = vec.Length;
				Vector3 dir = vec.Normal;

				Color col = Color.Random;

				// Distance based check. very unstable. we need to connect diagonal vertices to avoid a shearing collapse effect
				if ( dst < ConnectionDistance )
				{
					// This direction is too similar to excisting direction, skip.
					//if ( IsTooSimilar( dir ) ) { continue; }
					//dirs.Add( dir );

					MySpringJoint spring = particle.Components.Create<MySpringJoint>();
					spring.Stiffness = Stiffness;
					spring.Damping = SpringDamping;
					spring.ConnectTo( other.Components.Get<Rigidbody>(), dst );
					
					spring.WantedDistance = dst;
					spring.MaxStretch = MaxStretch;

					spring.Other.LinearDamping = LinearDamping;
					spring.Other.AngularDamping = AngularDamping;

					spring.Draw = DebugDrawFlags.HasFlag(DebugFlags.Springs);
					spring.GizmoColor = col;
				} 

			}
		}

		//bool IsTooSimilar( Vector3 dir )
		//{
		//	foreach ( Vector3 other in dirs )
		//	{
		//		if(Vector3.Dot(dir, other) > 0.9f) { return true; }
		//	}

		//	return false;
		//}

	}

	void FillWithParticles()
	{
		BBox bounds = Renderer.Model.Bounds;
		Vector3 min = bounds.Mins * Renderer.WorldScale + Renderer.WorldPosition;
		Vector3 max = bounds.Maxs * Renderer.WorldScale + Renderer.WorldPosition;

		float maxDim = MathF.Max( MathF.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.y );
		float pRadius = ParticleRadius;
		float step = MathF.Max( maxDim / 6f, pRadius * 3f );

		float minDst = pRadius * 2;

		Vector3 pos = new Vector3( min.x, min.y, min.z );
		while ( pos.y < max.y )
		{
			pos.y += step * Renderer.WorldScale.y;

			while ( pos.x < max.x )
			{
				pos.x += step * Renderer.WorldScale.x;

				while ( pos.z < max.z )
				{
					pos.z += step * Renderer.WorldScale.z;

					bool tooClose = Particles.TakeWhile( x => (x.WorldPosition - pos).Length > minDst ).Count() < Particles.Count;
					// Found excisting particle too close to this.
					if ( tooClose ) { continue; }

					if ( Math2d.PointIsWithinMesh( pos, Renderer ) )
					{
						// Todo, use matrix to rotate / scale position.
						CreateParticle( pos - WorldPosition );
					}	
						
				}

				pos.z = min.z;

			}

			pos.x = min.x;

		}

	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		Vector3 pos = 0;
		for ( int i = 0; i < Particles.Count; i++ )
		{
			pos += Particles[i].WorldPosition;

			if (i < VertexParticleCount ) {
				if( DebugDrawFlags.HasFlag( DebugFlags.ShellParticles ) )
				{
					Gizmo.Draw.Color = Color.Red;
					Gizmo.Draw.LineSphere( Particles[i].WorldPosition, ParticleRadius );
				}
			}
			else if ( DebugDrawFlags.HasFlag( DebugFlags.FillParticles ) )
			{
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.LineSphere( Particles[i].WorldPosition, ParticleRadius );
			}
		}

		// Center object position on average particle position.
		WorldPosition = pos / Particles.Count;

		if ( !Renderer.Enabled ) { return; }

		PointsBuffer.SetData( Particles.Take( VertexParticleCount ).Select( x => new Vector4( (x.WorldPosition - WorldPosition) ) ).ToList() );
		Renderer.SceneObject.Attributes.Set( "_Positions", PointsBuffer );

	}

}
