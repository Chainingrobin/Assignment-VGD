using UnityEditor;
using UnityEditor.SceneManagement;

// Pressing Play in the Editor starts at the main menu, whatever scene is open, so the game always
// opens the way a build does (menu, then New Game or Chapters). Untick "Wulang > Play from main menu"
// to play a region scene directly again (it loads the World and grants its prerequisites).
[InitializeOnLoad]
public static class PlayFromMenu
{
    private const string MenuPath = "Wulang/Play from main menu";
    private const string Key = "Wulang.PlayFromMenu";
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";

    static PlayFromMenu() => EditorApplication.delayCall += Apply;

    private static bool Enabled => EditorPrefs.GetBool(Key, true);

    private static void Apply() =>
        EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) : null;

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(Key, !Enabled);
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
}
