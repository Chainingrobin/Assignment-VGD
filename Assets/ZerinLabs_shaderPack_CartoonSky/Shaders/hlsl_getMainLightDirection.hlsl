void MainLightDirection_float (out float3 Direction)
{
	#if SHADERGRAPH_PREVIEW
	  Direction = float3(0.5, 0.5, 0);
	#else
	   Direction = _WorldSpaceLightPos0;
	#endif
}