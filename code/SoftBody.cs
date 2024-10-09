using System;
using System.Diagnostics;

namespace Saandy;

public sealed class SoftBody : Component
{
	[Flags]
	public enum DebugFlags
	{
		Springs = 1,
		Particles = 2,
	}

	[Property][Change] public DebugFlags DebugDrawFlags { get; set; }

	[RequireComponent] public ModelRenderer Renderer { get; private set; }

	private string ParticlePath = "prefabs/softbody_particle.prefab";

	List<Rigidbody> Particles { get; set; }
	List<uint> VertexIds;

	ComputeBuffer<Vector4> PointsBuffer;
	ComputeBuffer<uint> IDBuffer;

	int VertexParticleCount = 0;

	[Property] public float ConnectionDistance { get; set; } = 400;
	[Property][Change] public float Stiffness { get; set; } = 700;
	[Property][Change][Range(0f, 1f)] public float Damping { get; set; } = 0.6f;

	[Property][Change( "ToggleGravity" )] public bool UseGravity { get; set; } = true;
	[Property][Change][Range( 1f, 50000f )] public float? MassOverride { get; set; } = 0;

	[Property] public float ParticleRadius { get; set; } = 2f;

	void ToggleGravity(bool oldValue, bool newValue)
	{

		if(!Game.IsPlaying) { return; }
		if(Particles?.Count == 0) { return; }

		foreach(Rigidbody particle in Particles)
		{
			particle.Gravity = newValue;
		}

	}

	void OnMassOverrideChanged( float? oldValue, float? newValue )
	{
		if ( Particles == null ) { return; }

		float value = newValue.HasValue ? newValue.Value : 0;
		foreach ( Rigidbody particle in Particles )
		{
			particle.MassOverride = value;
		}
	}

	void OnStiffnessChanged(float oldValue, float newValue)
	{
		if(Particles == null) { return; } 

		foreach ( Rigidbody particle in Particles )
		{
			IEnumerable<MySpringJoint> joints = particle.Components.GetAll<MySpringJoint>();

			foreach(MySpringJoint joint in joints)
			{
				joint.Stiffness = newValue;
			}

		}
	}

	void OnDampingChanged( float oldValue, float newValue )
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

	[Button("Recreate")]
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
		particle.Gravity = UseGravity;
		particle.WorldPosition = WorldPosition + position;
		particle.GameObject.BreakFromPrefab();
		particle.MassOverride = MassOverride.HasValue ? MassOverride.Value : 0f;
		particle.Components.Get<SphereCollider>().Radius = ParticleRadius;
		//particle.GameObject.Flags = GameObjectFlags.Hidden;

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

		foreach( Rigidbody particle in Particles)
		{
			foreach( Rigidbody other in Particles)
			{
				if(particle == other) { continue; }

				float dst = Vector3.DistanceBetween( particle.WorldPosition, other.WorldPosition );

				// Distance based check. very unstable. we need to connect diagonal vertices to avoid a shearing collapse effect
				if( dst < ConnectionDistance * Renderer.WorldScale.x )
				{
					MySpringJoint spring = particle.Components.Create<MySpringJoint>();
					spring.Stiffness = Stiffness;
					spring.Damping = Damping;
					spring.ConnectTo( other.Components.Get<Rigidbody>() );

					spring.Other.LinearDamping = 3f;
					spring.Other.AngularDamping = 1f;

					spring.Draw = DebugDrawFlags.HasFlag(DebugFlags.Springs);
				} 

			}
		}

	}

	void FillWithParticles()
	{
		BBox bounds = Renderer.Model.Bounds;
		Vector3 min = bounds.Mins * Renderer.WorldScale + Renderer.WorldPosition;
		Vector3 max = bounds.Maxs * Renderer.WorldScale + Renderer.WorldPosition;

		float pRadius = ParticleRadius;
		float maxDim = MathF.Max( bounds.Size.x, bounds.Size.y );
		maxDim = MathF.Max( maxDim, bounds.Size.y );
		float step = MathF.Max( maxDim / 9f, pRadius * 3f );


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

					bool tooClose = Particles.TakeWhile( x => Vector3.DistanceBetween( x.WorldPosition, pos ) > minDst ).Count() < Particles.Count;
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

			if ( DebugDrawFlags.HasFlag(DebugFlags.Particles) ) {
				Gizmo.Draw.Color = i < VertexParticleCount ? Color.Red : Color.White;
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
