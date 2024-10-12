using System;

namespace Saandy;

public sealed class SoftBody : Component
{
	struct Connection
	{
		public uint A;
		public uint B;
		public float WantedDistance;
	}

	[Flags]
	public enum DebugFlags
	{
		Springs = 1,
		ShellParticles = 2,
		FillParticles = 4
	}

	
	[Category("Debug")][Property] public DebugFlags DebugDrawFlags { get; set; }
	
	[RequireComponent] public ModelRenderer Renderer { get; private set; }

	private string ParticlePath = "prefabs/softbody_particle.prefab";

	List<Rigidbody> Particles { get; set; }
	List<uint> VertexIds;

	List<Connection> Connections { get; set; }

	ComputeBuffer<Vector4> PointsBuffer;
	ComputeBuffer<uint> IDBuffer;

	int VertexParticleCount = 0;

	[Property][Change] public bool Gravity { get; set; } = true;
	void OnGravityChanged( bool oldValue, bool newValue )
	{
		if(!Game.IsPlaying) { return; }
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
	[Property] public float ConnectionDistance { get; set; } = 20;

	[Category( "Characteristics" )]
	[Property] public float ParticleRadius { get; set; } = 2f;

	[Category( "Characteristics" )]
	[Property] public float Stiffness { get; set; } = 700;

	[Category( "Characteristics" )]
	[Property][Range( 0f, 500f )] public float SpringDamping { get; set; } = 0f;

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
	[Property][Range(1f, 10f)] public float MaxStretch { get; set; } = 1.5f;

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

		particle.LinearDamping = LinearDamping;
		particle.AngularDamping = AngularDamping;

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
		Connections = new();

		for(int A = 0; A < Particles.Count; A++ )
		{
			//dirs.Clear();

			// If (B, A) already excist, we don't add (A, B). We also skip B = A.
			for ( int B = A + 1; B < Particles.Count; B++ )
			{

				Vector3 vec = Particles[B].WorldPosition - Particles[A].WorldPosition;
				float dst = vec.Length;
				Vector3 dir = vec.Normal;

				Color col = Color.Random;

				// Distance based check. very unstable. we need to connect diagonal vertices to avoid a shearing collapse effect
				if ( dst < ConnectionDistance )
				{
					Connections.Add( new Connection() { A = (uint)A, B = (uint)B, WantedDistance = dst } );
				}
			}
		}

		Log.Info( Connections.Count );

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


		PointsBuffer.SetData( Particles.Take( VertexParticleCount ).Select( x => new Vector4( (x.WorldPosition - WorldPosition) ) ).ToList() );
		if ( !Renderer.Enabled ) { return; }
		Renderer.SceneObject.Attributes.Set( "_Positions", PointsBuffer );

	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		foreach ( Connection con in Connections )
		{
			// TODO: make this one call.
			ConstrainParticles( con.A, con.B, con.WantedDistance );
			ConstrainParticles( con.B, con.A, con.WantedDistance );
		}

	}

	Vector3 wantedPos;
	void ConstrainParticles( uint A, uint B, float WantedDistance )
	{

		Rigidbody from = Particles[(int)A];
		Rigidbody to = Particles[(int)B];

		Vector3 vec = to.WorldPosition - from.WorldPosition;
		Vector3 dir = vec.Normal;
		float dst = vec.Length;

		wantedPos = from.WorldPosition + dir * WantedDistance;

		// How off is current pos from wanted pos?
		float stretch = WantedDistance - dst;

		// hooke's law
		Vector3 springForce = dir * (Stiffness * stretch);

		float relativeVelocity = Vector3.Dot( (to.Velocity - from.Velocity) * Time.Delta, dir );
		Vector3 damperForce = dir * (-SpringDamping * relativeVelocity);

		// Apply force proportional to the mass ratio
		float massRatio = (from.PhysicsBody.Mass / (from.PhysicsBody.Mass + to.PhysicsBody.Mass));
		to.ApplyForce( (springForce + damperForce) * massRatio );

		float maxDistance = WantedDistance * MaxStretch;
		if ( dst > maxDistance )
		{
			// Move 'to' closer to 'from'
			Vector3 clampedPosition = from.WorldPosition + dir * maxDistance;
			to.Velocity += to.WorldPosition - clampedPosition;
			//to.ApplyForce( to.WorldPosition - clampedPosition );
			//to.WorldPosition = clampedPosition;
		}

#if DEBUG
		if(DebugDrawFlags.Contains(DebugFlags.Springs))
		{
			Gizmo.Draw.Color = ColorX.GooberColors[A % ColorX.GooberColors.Length];
			Gizmo.Draw.Line( to.WorldPosition, to.WorldPosition + (from.WorldPosition - to.WorldPosition).Normal );
		}
#endif

	}

}
