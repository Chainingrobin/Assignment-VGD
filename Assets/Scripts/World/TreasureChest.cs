using System.Collections.Generic;
using UnityEngine;

// A reward cache at a point of interest. F opens it: the lid swings up and the player gets what is
// inside (Wubucks, items such as potions or a journal page, and sometimes a vitality relic that
// raises maximum health). Opening sets a story flag, so a looted chest stays open and empty after a
// region reload or Continue. Its light beam marks it from afar until it is opened.
[RequireComponent(typeof(Interactable))]
public class TreasureChest : MonoBehaviour
{
    [SerializeField] private string id;
    [SerializeField] private Transform lid;
    [SerializeField] private GameObject beacon;
    [SerializeField, Min(0)] private int wubucks;
    [SerializeField] private ItemData[] items;
    [Tooltip("Holds a vitality relic that raises maximum health.")]
    [SerializeField] private bool vitality;
    [SerializeField] private GameObject openEffect;

    private const float OpenSeconds = 0.7f;
    private float openedAt = -1f;

    private string Flag => "chest: " + id;

    private void Start()
    {
        var interactable = GetComponent<Interactable>();
        if (StoryStateManager.Instance.HasFlag(Flag))
        {
            interactable.enabled = false;
            Pose(1f);
            return;
        }
        interactable.OnInteract.AddListener(Open);
    }

    private void Open()
    {
        if (openedAt >= 0f) return;
        openedAt = Time.time;
        GetComponent<Interactable>().enabled = false;
        var story = StoryStateManager.Instance;
        story.SetFlag(Flag);
        if (vitality) story.SetFlag(PlayerHealth.VitalityPrefix + id); // PlayerHealth raises its maximum
        var wallet = FindFirstObjectByType<PlayerCoins>();
        if (wubucks > 0 && wallet != null) wallet.Add(wubucks);
        var given = new List<ItemData>();
        if (PlayerInventory.Instance != null)
            foreach (var item in items)
                if (item != null) { PlayerInventory.Instance.Add(item); given.Add(item); }
        // What was inside, as icons: heart, coins, flask, scroll.
        LootToast.Show(wallet != null ? wubucks : 0, vitality ? PlayerHealth.VitalityPerRelic : 0f, given);

        if (openEffect != null) Destroy(Instantiate(openEffect, transform.position + Vector3.up * 0.6f, Quaternion.identity), 4f);
        GameAudio.Play("Sfx/Chest", transform.position, 0.9f);
    }

    private void Update()
    {
        if (openedAt < 0f) return;
        float t = (Time.time - openedAt) / OpenSeconds;
        Pose(t);
        if (t >= 1f) enabled = false;
    }

    // 0 = shut, 1 = lid thrown back and the beacon out.
    private void Pose(float t)
    {
        if (lid != null) lid.localRotation = Quaternion.Euler(-105f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)), 0f, 0f);
        if (t >= 1f && beacon != null) beacon.SetActive(false);
    }
}
