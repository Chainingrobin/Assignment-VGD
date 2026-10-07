using System.Collections;
using UnityEngine;

public class WaterFreezer : MonoBehaviour
{
    [SerializeField] Transform ice;
    [SerializeField] Collider iceCollider;
    [SerializeField] float duration = 4f;
    [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    Vector3 fullScale;
    bool started;

    void Awake()
    {
        if (ice == null) { Debug.LogError("WaterFreezer: Ice is not assigned"); return; }
        fullScale = ice.localScale;
        Debug.Log($"WaterFreezer ready. Ice full scale = {fullScale}");
        ice.gameObject.SetActive(false);
        if (iceCollider) iceCollider.enabled = false;
    }

    public void Freeze()
    {
        Debug.Log("Freeze called");
        if (started) return;
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Debug.Log("Freeze started");
        ice.localScale = new Vector3(fullScale.x * 0.01f, fullScale.y, fullScale.z * 0.01f);
        ice.gameObject.SetActive(true);

        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = curve.Evaluate(t / duration);
            ice.localScale = new Vector3(fullScale.x * k, fullScale.y, fullScale.z * k);
            yield return null;
        }

        ice.localScale = fullScale;
        if (iceCollider) iceCollider.enabled = true;
        Debug.Log("Freeze finished");
    }
}