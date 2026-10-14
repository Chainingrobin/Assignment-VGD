using UnityEngine;
using System.Collections;
class scr_skyboxManipulatorUtility : MonoBehaviour
{
    /*
    WARNING
    --------------
        This utility is on BETA stage and could produce some undesired results
        It is highly recomended that before using this script you clone or save a preset of your skybox material as it can be changed unintentionally

    INSTRUCTIONS
    ---------------------
        - Create a new Material using the [sg_ZL_cartoonSky] shader and assign the material to the unity Skybox 
        - Add this script to an empty GameObject in your scene
        - Assign an animator to your this and animate the exposed script parameters using timeline
        - Once you press PLAY,  the skybox will be animated accordingly

    TRICK assign the same material to a 3d sphere so you can preview the material first

    PROPERTIES NOT ANIMATABLE
    --------------------------------
        _Sky_gradient_map
        _Clouds_map
        _Stars_map
    */    
    public bool update_GI_realtime = false;

    //Expose shader properties so they can be animated in the timeline
    public float _Day_cycle_time = 0f;
    public Color _Sky_top_tint = Color.white;
    public Color _Sky_bottom_tint = Color.white;

    public float _Clouds_gradient_shift = 0.15f;
    public float _Clouds_tiling = 4f;
    public float _Clouds_separation = 0.2f;
    public float _Clouds_position = 0f;
    public float _Clouds_speed = 0.2f;

    public float _Sun_size = 0.1f;
    public float _Sun_brightness=1.0f;
    public Color _Sun_zenith_color = Color.yellow;
    public Color _Sun_dawn_color = Color.red;
    public float _Sun_bloom_size = 0.05f;
    public float _Sun_bloom_intensity = 0.2f;

    public float _Stars_intensity = 1.0f;
    public float _Stars_Tiling = 1.0f;

    public Material SkyboxMaterialReference;

    private bool hasAnimator = false;
    private string shaderName = "sg_ZL_cartoonSky";

    private void Start()
    {
        if (gameObject.GetComponent<Animator>() != null)
        {
            hasAnimator = true;
        }
        else 
        {
            Debug.Log("WARNING: no animator assigned to this object. You need an animator to animate the skybox parameters!");
        }
        
        if (RenderSettings.skybox.shader.name.Contains(shaderName) == true)
        {
            _Day_cycle_time = RenderSettings.skybox.GetFloat("_Day_cycle_time");
            _Sky_top_tint = RenderSettings.skybox.GetColor("_Sky_top_tint");
            _Sky_bottom_tint = RenderSettings.skybox.GetColor("_Sky_bottom_tint");

            _Clouds_gradient_shift = RenderSettings.skybox.GetFloat("_Clouds_gradient_shift");
            _Clouds_tiling = RenderSettings.skybox.GetFloat("_Clouds_tiling");
            _Clouds_separation = RenderSettings.skybox.GetFloat("_Clouds_separation");
            _Clouds_position = RenderSettings.skybox.GetFloat("_Clouds_position");
            _Clouds_speed = RenderSettings.skybox.GetFloat("_Clouds_speed");

            _Sun_size = RenderSettings.skybox.GetFloat("_Sun_size");
            _Sun_brightness = RenderSettings.skybox.GetFloat("_Sun_brightness");
            _Sun_zenith_color = RenderSettings.skybox.GetColor("_Sun_zenith_color");
            _Sun_dawn_color = RenderSettings.skybox.GetColor("_Sun_dawn_color");
            _Sun_bloom_size = RenderSettings.skybox.GetFloat("_Sun_bloom_size");
            _Sun_bloom_intensity = RenderSettings.skybox.GetFloat("_Sun_bloom_intensity");

            _Stars_intensity = RenderSettings.skybox.GetFloat("_Stars_intensity");
            _Stars_Tiling = RenderSettings.skybox.GetFloat("_Stars_Tiling");
        }
        else
        {
            Debug.Log("WARNING: Shader [sg_ZL_cartoonSky] not assigend to skybox material.");
        }
    }

    private void Update()
    {
        if (hasAnimator == true)
        {

            if (RenderSettings.skybox.shader.name.Contains(shaderName) == true)
            {

                RenderSettings.skybox.SetFloat("_Day_cycle_time", _Day_cycle_time);

                RenderSettings.skybox.SetFloat("_Day_cycle_time", _Day_cycle_time);
                RenderSettings.skybox.SetColor("_Sky_top_tint", _Sky_top_tint);
                RenderSettings.skybox.SetColor("_Sky_bottom_tint", _Sky_bottom_tint);

                RenderSettings.skybox.SetFloat("_Clouds_gradient_shift", _Clouds_gradient_shift);
                RenderSettings.skybox.SetFloat("_Clouds_tiling", _Clouds_tiling);
                RenderSettings.skybox.SetFloat("_Clouds_separation", _Clouds_separation);
                RenderSettings.skybox.SetFloat("_Clouds_position", _Clouds_position);
                RenderSettings.skybox.SetFloat("_Clouds_speed", _Clouds_speed);

                RenderSettings.skybox.SetFloat("_Sun_size", _Sun_size);
                RenderSettings.skybox.SetFloat("_Sun_brightness", _Sun_brightness);
                RenderSettings.skybox.SetColor("_Sun_zenith_color", _Sun_zenith_color);
                RenderSettings.skybox.SetColor("_Sun_dawn_color", _Sun_dawn_color);
                RenderSettings.skybox.SetFloat("_Sun_bloom_size", _Sun_bloom_size);
                RenderSettings.skybox.SetFloat("_Sun_bloom_intensity", _Sun_bloom_intensity);

                RenderSettings.skybox.SetFloat("_Stars_intensity", _Stars_intensity);
                RenderSettings.skybox.SetFloat("_Stars_Tiling", _Stars_Tiling);

                if (update_GI_realtime == true)
                {
                    DynamicGI.UpdateEnvironment(); //WARNING!! this function is expensive, was set here for demo purposes only!
                }
            }
        }
    }

    public void UpdateDataBasedOnMaterial()
    {
        if (SkyboxMaterialReference != null)
        {
            if (SkyboxMaterialReference.shader.name.Contains(shaderName) == true)
            {
                _Day_cycle_time = SkyboxMaterialReference.GetFloat("_Day_cycle_time");
                _Sky_top_tint = SkyboxMaterialReference.GetColor("_Sky_top_tint");
                _Sky_bottom_tint = SkyboxMaterialReference.GetColor("_Sky_bottom_tint");

                _Clouds_gradient_shift = SkyboxMaterialReference.GetFloat("_Clouds_gradient_shift");
                _Clouds_tiling = SkyboxMaterialReference.GetFloat("_Clouds_tiling");
                _Clouds_separation = SkyboxMaterialReference.GetFloat("_Clouds_separation");
                _Clouds_position = SkyboxMaterialReference.GetFloat("_Clouds_position");
                _Clouds_speed = SkyboxMaterialReference.GetFloat("_Clouds_speed");

                _Sun_size = SkyboxMaterialReference.GetFloat("_Sun_size");
                _Sun_brightness = SkyboxMaterialReference.GetFloat("_Sun_brightness");
                _Sun_zenith_color = SkyboxMaterialReference.GetColor("_Sun_zenith_color");
                _Sun_dawn_color = SkyboxMaterialReference.GetColor("_Sun_dawn_color");
                _Sun_bloom_size = SkyboxMaterialReference.GetFloat("_Sun_bloom_size");
                _Sun_bloom_intensity = SkyboxMaterialReference.GetFloat("_Sun_bloom_intensity");

                _Stars_intensity = SkyboxMaterialReference.GetFloat("_Stars_intensity");
                _Stars_Tiling = SkyboxMaterialReference.GetFloat("_Stars_Tiling");
            }
            else 
            {
                Debug.Log("ERROR: Shader [sg_ZL_cartoonSky] not assigend to [SkyboxMaterialReference] material.");
            }
        }
        else 
        {
            Debug.LogError("ERROR: No material was assigned to the [SkyboxMaterialReference] slot");
        }
    }
}
