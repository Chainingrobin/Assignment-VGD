using UnityEngine;
using UnityEngine.SceneManagement;

// Sits in every region scene. Declares where the player arrives and which story flags must
// already be true to be here, so a region played directly from the Editor or chosen from
// the chapter menu starts with the right elements. Prerequisites only ever grow region to
// region, so setting them is always safe. If the shared World scene isn't loaded yet
// (pressing Play on this scene), it loads it, which makes every region scene playable alone.
[DefaultExecutionOrder(-200)]
public class RegionStart : MonoBehaviour
{
    [SerializeField] private Transform spawn;
    [SerializeField] private string[] prerequisites;

    public Transform Spawn => spawn != null ? spawn : transform;
    public string Region => gameObject.scene.name;

    private void Awake()
    {
        var story = StoryStateManager.Instance;
        foreach (var flag in prerequisites) story.SetFlag(flag);
        if (RegionManager.Instance == null && !SceneManager.GetSceneByName(RegionManager.WorldScene).isLoaded)
            SceneManager.LoadSceneAsync(RegionManager.WorldScene, LoadSceneMode.Additive);
    }

    public static RegionStart Find(string region)
    {
        foreach (var start in FindObjectsByType<RegionStart>(FindObjectsSortMode.None))
            if (start.Region == region) return start;
        return null;
    }
}
