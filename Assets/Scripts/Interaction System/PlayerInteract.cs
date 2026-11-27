using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// The player's interact control (F): finds what Wulang is looking at, highlights it, shows a "[F] Talk to Abe"
/// style prompt, and uses it when F is pressed.
/// </summary>
/// <remarks>
/// <para><b>How it targets.</b> Every frame a ray is cast from the camera, up to <c>range</c> metres. The first
/// <see cref="Interactable"/> it hits (on the collider or a parent) that can currently be used becomes the target:
/// it gets an outline and its prompt is shown under the crosshair. Interactables switched off by a
/// <c>FlagGate</c> report that they can't be used, so locked story objects never show a prompt.</para>
/// <para><b>Story role.</b> F is how the player reaches most narrative content: talking to and trading with Abe,
/// reading notes and plaques (<c>NarrativeTrigger</c> in Interact mode), waking the ice statue, and using the
/// waystone and rift exits (<c>RegionTravel</c>). Each of those sets story flags.</para>
/// <para>Input comes from the Input System's Interact action (F), so it can be rebound without code.</para>
/// </remarks>
public class PlayerInteract : MonoBehaviour
{
    [Tooltip("Camera the targeting ray starts from; defaults to the main camera.")]
    [SerializeField] Camera cam;
    [Tooltip("Input System action that interacts (bound to F).")]
    [SerializeField] InputActionReference interactAction;
    [Tooltip("HUD text that shows the current prompt.")]
    [SerializeField] TMP_Text promptText;
    [Tooltip("How far away, in metres, something can be used.")]
    [SerializeField] float range = 3.5f;
    // Forgiving aim: with nothing under the crosshair, anything usable this many degrees off the view still counts.
    const float AimCone = 30f;
    readonly Collider[] nearby = new Collider[32];
    [Tooltip("Landmarks that offer an optional fly-over when looked at from afar.")]
    [SerializeField] BiomePreview[] biomePreviews;

    Interactable current;
    BiomePreview currentPreview;
    RectTransform promptKey;
    string shownPrompt;

    // Checks its references (logging what is missing) and styles the prompt: parchment text with an F keycap.
    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) Debug.LogError("PlayerInteract: no camera assigned");
        if (interactAction == null) Debug.LogError("PlayerInteract: no Interact action assigned");
        if (promptText == null) Debug.LogError("PlayerInteract: no prompt text assigned");

        if (promptText != null)
        {
            promptText.richText = true;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 24f;
            promptText.color = FantasyUI.Parchment;
            promptText.raycastTarget = false;
            promptText.textWrappingMode = TextWrappingModes.NoWrap;
            promptText.outlineWidth = 0.2f;
            promptText.outlineColor = new Color32(12, 7, 4, 255);

            var promptRect = (RectTransform)promptText.transform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0f, -52f);
            promptRect.sizeDelta = new Vector2(700f, 40f);
            promptKey = UIKit.Keycap(promptRect, "F", Vector2.zero, new Vector2(0.5f, 0.5f));
            promptRect.localPosition = new Vector3(promptRect.localPosition.x, promptRect.localPosition.y, 0f);
            promptRect.localRotation = Quaternion.identity;
            promptRect.localScale = Vector3.one;
        }
    }

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null) return;
        interactAction.action.Enable();
        interactAction.action.performed += OnInteract;
    }

    void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.performed -= OnInteract;
        if (current != null) current.SetHighlighted(false);
        current = null;
        currentPreview = null;
        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    // Picks the target under the crosshair, moves the highlight to it, and updates the prompt.
    void Update()
    {
        if (cam == null) return;
        Interactable target = null;
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        Debug.DrawRay(origin, dir * range, Color.red);

        // Trigger volumes (zones, hazards) are ignored so they never block the view of a real object.
        if (Physics.Raycast(origin, dir, out RaycastHit hit, range, ~0,
                            QueryTriggerInteraction.Ignore))
        {
            var found = hit.collider.GetComponentInParent<Interactable>();
            if (found != null && found.CanInteract) target = found;
        }
        if (target == null) target = NearestInView(origin, dir);

        if (target != current)
        {
            if (current != null) current.SetHighlighted(false);
            current = target;
            if (current != null) current.SetHighlighted(true);
        }

        if (promptText == null) return;
        // With nothing close at hand, a distant landmark in view can offer its fly-over instead.
        currentPreview = null;
        if (current == null && biomePreviews != null)
            foreach (var preview in biomePreviews)
                if (preview != null && preview.CanView(cam)) { currentPreview = preview; break; }
        string prompt = current != null ? current.Prompt : currentPreview != null ? "Press F to view area" : null;
        promptText.gameObject.SetActive(prompt != null);
        if (prompt != null && prompt != shownPrompt) ShowPrompt(prompt);
    }

    // The usable object closest to the centre of the view within reach and the aim cone, if nothing blocks the way
    // to it. Looking roughly at Abe or a chest is enough; it no longer has to be under the exact pixel.
    Interactable NearestInView(Vector3 origin, Vector3 dir)
    {
        Interactable best = null;
        float bestAngle = AimCone;
        int count = Physics.OverlapSphereNonAlloc(origin, range + 0.5f, nearby, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var candidate = nearby[i].GetComponentInParent<Interactable>();
            if (candidate == null || !candidate.CanInteract) continue;
            var point = nearby[i].ClosestPoint(origin + dir * Mathf.Min(range, Vector3.Distance(origin, nearby[i].bounds.center)));
            var to = point - origin;
            float distance = to.magnitude;
            if (distance > range + 0.5f) continue;
            float angle = distance < 0.01f ? 0f : Vector3.Angle(dir, to);
            if (angle >= bestAngle) continue;
            if (distance > 0.05f && Physics.Raycast(origin, to / distance, out var block, distance - 0.05f, ~0, QueryTriggerInteraction.Ignore) &&
                block.collider.GetComponentInParent<Interactable>() != candidate && block.collider.GetComponentInParent<CharacterController>() == null)
                continue;
            best = candidate;
            bestAngle = angle;
        }
        return best;
    }

    // "Press F to read the ledger" becomes [F] Read the ledger: the keycap carries the key, the text

    // only the action (and no other capital F gets mistaken for the key).
    void ShowPrompt(string prompt)
    {
        shownPrompt = prompt;
        const string lead = "Press F to ";
        string action = prompt.StartsWith(lead) ? prompt.Substring(lead.Length) : prompt;
        promptText.text = action.Length > 0 ? char.ToUpper(action[0]) + action.Substring(1) : action;
        promptKey.anchoredPosition = new Vector2(-promptText.GetPreferredValues(promptText.text).x * 0.5f - promptKey.sizeDelta.x * 0.5f - 10f, 0f);
    }

    // F pressed: use the target (Interactable.Interact raises its OnInteract event), or start the landmark fly-over.
    void OnInteract(InputAction.CallbackContext ctx)
    {
        if (current != null) current.Interact();
        else if (currentPreview != null && currentPreview.CanView(cam)) currentPreview.View();
    }
}
