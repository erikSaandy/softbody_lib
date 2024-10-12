using Saandy;
using Editor;
using Editor.MeshEditor;

public class SoftBodyEditorTool : EditorTool<SoftBody>
{

	public override void OnEnabled()
	{

	}

	public override void OnUpdate()
	{
		//Gizmo.
		//bool ass = Gizmo.Control.Position( "ass", 0, out Vector3 pos );

		//if ( ass) 
		//{

		//	Vector2 delta = Gizmo.Pressed.CursorDelta;
		//	Log.Info( Gizmo.CursorMoveDelta );	
		//}

	}

	public override void OnDisabled()
	{

	}

	public override void OnSelectionChanged()
	{
		var target = GetSelectedComponent<MyComponent>();

	}

}
