using Saandy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public static class TransformHelper
{

	public static Matrix4x4 WorldToLocalMatrix( this GameTransform transform )
	{
		//Rotation inverse = Rotation.Inverse;
		//Transform result = new Transform();
		//result.Position = (child.Position - Position) * inverse / Scale;
		//result.Rotation = inverse * child.Rotation;
		//result.Scale = child.Scale / Scale;
		//return result;

		return MatrixHelper.CreateTRS( -transform.Position, transform.Rotation.Inverse, -transform.Scale );
	}

}
