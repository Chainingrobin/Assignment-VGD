#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(scr_skyboxManipulatorUtility))]
public class scr_skyboxManipulatorUtility_inspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        scr_skyboxManipulatorUtility SkyManipulator = (scr_skyboxManipulatorUtility)target;

        if (GUILayout.Button("Get & Apply Material Data"))
        {
            SkyManipulator.UpdateDataBasedOnMaterial();
        }
    }
}
#endif
