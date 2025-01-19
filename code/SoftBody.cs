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

	Vector3[] StartPositions;

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

	GpuBuffer<Vector4> PointsBuffer;
	GpuBuffer<uint> IDBuffer;

	int VertexParticleCount = 0;

	Vector3 CenterOfMass { get; set; } = 0;

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

	public float ConnectionDistance { get; set; }

	[Category( "Characteristics" )]
	[Property] public float ParticleRadius { get; set; } = 2f;

	[Category( "Characteristics" )]
	[Property] public float Stiffness { get; set; } = 700;
	[Property] public float ShapeRetentionStiffness { get; set; } = 4000;

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

		BuildConnections();

		PointsBuffer = new GpuBuffer<Vector4>( VertexParticleCount, GpuBuffer.UsageFlags.Structured );

		IDBuffer = new GpuBuffer<uint>( VertexIds.Count, GpuBuffer.UsageFlags.Structured );
		IDBuffer.SetData( VertexIds );
		Renderer.SceneObject.Attributes.Set( "_IDs", IDBuffer );
		Renderer.SceneObject.Attributes.Set( "_Scale", WorldScale );

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
				positions.Add( allVertices[i].Position * WorldScale );
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


	void BuildConnections()
	{
		Connections = new();

		for(int A = 0; A < Particles.Count; A++ )
		{

			// If (B, A) already excist, we don't add (A, B). We also skip B = A.
			for ( int B = A + 1; B < Particles.Count; B++ )
			{

				Vector3 vec = Particles[B].WorldPosition - Particles[A].WorldPosition;
				float dst = vec.Length;
				Vector3 dir = vec.Normal;

				Color col = Color.Random;

				// Distance based check. we need to connect diagonal vertices to avoid a shearing collapse effect
				if ( dst < ConnectionDistance )
				{
					Connections.Add( new Connection() { A = (uint)A, B = (uint)B, WantedDistance = ConnectionDistance } );
				}
			}
		}

		Log.Info( Connections.Count );

		// Get rest positions

		Vector3 centerOfMass = Vector3.Zero;

		StartPositions = new Vector3[Particles.Count];

		foreach ( var particle in Particles )
			centerOfMass += particle.WorldPosition;

		centerOfMass /= Particles.Count;

		for ( int i = 0; i < Particles.Count; i++ )
			StartPositions[i] = Particles[i].WorldPosition - centerOfMass;

	}

	void FillWithParticles()
	{
		BBox bounds = Renderer.Model.Bounds;
		Vector3 min = bounds.Mins * Renderer.WorldScale + Renderer.WorldPosition;
		Vector3 max = bounds.Maxs * Renderer.WorldScale + Renderer.WorldPosition;

		float maxDim = MathF.Max( MathF.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.y );
		float step = MathF.Max( maxDim / 6f, ParticleRadius * 2.5f );

		float minDst = ParticleRadius * 2f;

		// calculate max connection distance as the diagonal distance between particles.
		ConnectionDistance = step * Renderer.WorldScale.y * 1.415f;

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

					if ( Math2d.PointIsWithinMesh( pos - WorldPosition, Renderer ) )
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
			ConstrainParticles( con.A, con.B, con.WantedDistance );
			ConstrainParticles( con.B, con.A, con.WantedDistance );
		}

		// Calculate center of mass
		CenterOfMass = 0;

		foreach ( var particle in Particles )
		{
			CenterOfMass += particle.WorldPosition;
		}

		CenterOfMass /= Particles.Count;


		//for ( int i = 0; i < Particles.Count; i++ )
		//{
		//	Vector3 targetPosition = centerOfMass + RestPositions[i];
		//	Vector3 displacement = targetPosition - Particles[i].WorldPosition;

		//	Vector3 shapeForce = displacement * ShapeRetentionStiffness; // Tune this stiffness


		//	//Particles[i].ApplyForce( shapeForce );
		//}

		// Update bounds
		BBox bounds = Renderer.Model.Bounds;
		Vector3 min = bounds.Mins * Renderer.WorldScale + Renderer.WorldPosition;
		Vector3 max = bounds.Maxs * Renderer.WorldScale + Renderer.WorldPosition;

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

		Vector3 totalForce = springForce + damperForce;

		from.ApplyForce( -totalForce * 1-massRatio );
		to.ApplyForce( totalForce * massRatio );

		// Shape Retention Logic with Better Shape Preservation
		if ( ShapeRetentionStiffness > 0 )
		{
			// Compute the initial relative vector between particles A and B
			Vector3 initialRelative = StartPositions[(int)B] - StartPositions[(int)A];
			float initialLength = initialRelative.Length;

			// Calculate the current relative vector and its length
			Vector3 currentRelative = to.WorldPosition - from.WorldPosition;
			float currentLength = currentRelative.Length;

			// Normalize the current relative vector
			Vector3 currentNormal = currentRelative.Normal;

			// Project the initial relative vector onto the current normal to calculate the target position
			Vector3 targetRelative = currentNormal * initialLength;

			// Compute the corrective displacement vector
			Vector3 correctiveDisplacement = targetRelative - currentRelative;

			// Apply corrective forces only if the displacement is significant
			if ( correctiveDisplacement.Length > 0.01f )
			{
				// Calculate corrective force
				Vector3 correctiveForce = correctiveDisplacement * ShapeRetentionStiffness;

				// Apply forces to the two particles
				from.ApplyForce( -correctiveForce );
				to.ApplyForce( correctiveForce );
			}
		}

#if DEBUG
		if (DebugDrawFlags.Contains(DebugFlags.Springs))
		{
			Gizmo.Draw.Color = ColorX.GooberColors[A % ColorX.GooberColors.Length];
			Gizmo.Draw.Line( to.WorldPosition, to.WorldPosition + (from.WorldPosition - to.WorldPosition) );
		}
#endif

	}

}
