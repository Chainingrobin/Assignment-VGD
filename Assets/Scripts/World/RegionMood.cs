using UnityEngine;
using UnityEngine.Rendering;

// A region's colour grade. It lives in the region scene, so it arrives and leaves with its
// chapter: walking from the warm Forest into the cold Frozen Reach changes the look of the world
// exactly when the level changes. It eases in over the transition flight instead of popping.
[RequireComponent(typeof(Volume))]
public class RegionMood : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fadeIn = 1.5f;
    private Volume volume;

    private void OnEnable()
    {
        volume = GetComponent<Volume>();
        volume.weight = 0f;
        // The region's music and ambience arrive with it (Resources/Audio/Music|Ambience/<scene>).
        GameAudio.Mood(gameObject.scene.name);
    }

    private void Update()
    {
        if (volume.weight >= 1f) return;
        volume.weight = fadeIn > 0f ? Mathf.Min(1f, volume.weight + Time.unscaledDeltaTime / fadeIn) : 1f;
    }
}
