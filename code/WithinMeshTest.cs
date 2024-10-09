using Saandy;

namespace Sandbox;

public sealed class WithinMeshTest : Component
{
	[Property] ModelRenderer Renderer { get; set; }

	protected override void OnUpdate()
	{
		bool inside = Math2d.PointIsWithinMesh( WorldPosition, Renderer );

		if( inside )
		{
			Gizmo.Draw.Color = Color.Green;
		}
		else
		{
			Gizmo.Draw.Color = Color.Red;
		}

		Gizmo.Draw.LineSphere( WorldPosition, 32 );

	}
}
