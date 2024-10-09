HEADER
{
	Description = "Standard shader but it bends, proof of concept, will be made into a proper feature with compute shaders";
}

MODES
{
	VrForward();
	Depth();
	ToolsVis( S_MODE_TOOLS_VIS );
}

FEATURES
{
    #include "common/features.hlsl"
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	StructuredBuffer<float4> _Positions < Attribute("_Positions"); >;
	StructuredBuffer<uint> _IDs < Attribute("_IDs"); >;

	//float _Scale <Attribute("_Scale"); >;

	PixelInput MainVs( VertexInput i, uint id : SV_VertexID )
	{

		i.vPositionOs = _Positions[_IDs[id]];

		PixelInput o = ProcessVertex( i );

		return FinalizeVertex( o );

	}
}

//=========================================================================================================================

PS
{
    #include "common/pixel.hlsl"
	

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );
		return ShadingModelStandard::Shade( i, m );
	}
}