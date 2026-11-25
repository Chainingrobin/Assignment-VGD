using UnityEngine;

// Abe at his camp. The first F in a region is a conversation (the region's story beat, so the
// player chooses when to hear it instead of being stopped by a trigger); every F after that opens
// his shop. The prompt says which: "talk to" or "trade with".
[RequireComponent(typeof(Interactable))]
public class Merchant : MonoBehaviour
{
    [SerializeField] private string npcName = "Abe";
    [SerializeField] private Sprite portrait;
    [SerializeField] private ItemData[] stock;
    [Tooltip("Played on the first F in this region; empty = straight to the shop.")]
    [SerializeField] private DialogueData conversation;
    [Tooltip("Line Abe says the first time the shop opens in this region.")]
    // Each camp's greeting is written from the Story Text asset (Wulang > Story > Apply story text).
    [SerializeField] private string greeting;

    private Interactable interactable;
    private bool greeted;

    // His camps are safe ground: enemies give up the chase within this radius of him (VampireMelee).
    private const float SafeRadius = 12f;
    private static readonly System.Collections.Generic.List<Merchant> camps = new();
    private void OnEnable() => camps.Add(this);
    private void OnDisable() => camps.Remove(this);

    public static bool Sanctuary(Vector3 position)
    {
        foreach (var camp in camps)
            if ((camp.transform.position - position).sqrMagnitude < SafeRadius * SafeRadius) return true;
        return false;
    }

    // Remembered as a story flag so a reloaded region doesn't replay the conversation.
    private string TalkedFlag => conversation != null ? "talked: " + conversation.name : null;
    private bool HasTalked => conversation == null || StoryStateManager.Instance.HasFlag(TalkedFlag);

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        interactable.OnInteract.AddListener(Interact);
        UpdatePrompt();
    }

    private void Interact()
    {
        if (!HasTalked)
        {
            StoryStateManager.Instance.SetFlag(TalkedFlag);
            DialogueManager.Instance.Play(conversation, UpdatePrompt);
            StartCoroutine(FaceHim());
            return;
        }
        Open();
    }

    // He sits low, so turn the view onto his chest as he starts talking: his face sits above the dialogue box.
    private System.Collections.IEnumerator FaceHim()
    {
        var player = FindFirstObjectByType<StarterAssets.FirstPersonController>();
        var animator = GetComponentInChildren<Animator>();
        var chest = animator != null ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
        if (player == null || chest == null || Camera.main == null) yield break;
        var eye = Camera.main.transform;
        var from = eye.position + eye.forward * Vector3.Distance(eye.position, chest.position);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            player.LookAt(Vector3.Lerp(from, chest.position, Mathf.SmoothStep(0f, 1f, t / 0.5f)));
            yield return null;
        }
        player.LookAt(chest.position);
    }

    private void UpdatePrompt() => interactable.SetPrompt((HasTalked ? "Press F to trade with " : "Press F to talk to ") + npcName);

    public void Open()
    {
        if (ShopUI.IsOpen) return;
        if (!greeted && !string.IsNullOrEmpty(greeting)) { greeted = true; Subtitle.Show(greeting); }
        ShopUI.Open(npcName, portrait, stock);
    }
}
