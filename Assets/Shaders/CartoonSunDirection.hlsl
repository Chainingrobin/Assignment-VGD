float4 _CartoonSunDirection;
void MainLightDirection_float(out float3 Direction)
{
#if SHADERGRAPH_PREVIEW
Direction = float3(0.5,0.5,0);
#else
Direction = _CartoonSunDirection.xyz;
#endif
}
