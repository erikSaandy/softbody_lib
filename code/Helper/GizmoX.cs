using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GizmoX
{
	private static List<GizmoAction> Actions { get; set; } = new();

	private static bool IsDrawing = false;

	public static void DrawLineSphere(Vector3 point, float radius, int rings = 8, float time = 0 )
	{

		if(time > 0f)
		{
			Actions.Add( new GizmoAction()
			{
				GizmoCall = delegate { Gizmo.Draw.LineSphere( point, radius, rings ); },
				AliveTime = time
			} );

			KeepDrawing();
			
		}


	}

	public static async Task KeepDrawing()
	{
		if(IsDrawing) { return; }
		IsDrawing = true;

		do
		{

			for ( int i = Actions.Count - 1; i >= 0; i-- )
			{
				Actions[i].GizmoCall?.Invoke();

				if ( Actions[i].TimeSinceCreated > Actions[i].AliveTime )
				{
					Actions.RemoveAt( i );
				}
			}

			await Task.Delay( 15 );

		}
		while ( Actions.Count > 0 );

		IsDrawing = false;

	}


	struct GizmoAction
	{
		public Action GizmoCall;
		public float AliveTime;
		public TimeSince TimeSinceCreated;
	}

}
