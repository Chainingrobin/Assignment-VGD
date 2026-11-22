using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] InputActionReference interactAction;
    [SerializeField] TMP_Text promptText;
    [SerializeField] float range = 3f;
    [SerializeField] BiomePreview[] biomePreviews;

    Interactable current;
    BiomePreview currentPreview;
    RectTransform promptKey;
    string shownPrompt;

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

    void Update()
    {
        if (cam == null) return;
        Interactable target = null;
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        Debug.DrawRay(origin, dir * range, Color.red);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, range, ~0,
                            QueryTriggerInteraction.Ignore))
        {
            var found = hit.collider.GetComponentInParent<Interactable>();
            if (found != null && found.CanInteract) target = found;
        }

        if (target != current)
        {
            if (current != null) current.SetHighlighted(false);
            current = target;
            if (current != null) current.SetHighlighted(true);
        }

        if (promptText == null) return;
        currentPreview = null;
        if (current == null && biomePreviews != null)
            foreach (var preview in biomePreviews)
                if (preview != null && preview.CanView(cam)) { currentPreview = preview; break; }
        string prompt = current != null ? current.Prompt : currentPreview != null ? "Press F to view area" : null;
        promptText.gameObject.SetActive(prompt != null);
        if (prompt != null && prompt != shownPrompt) ShowPrompt(prompt);
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

    void OnInteract(InputAction.CallbackContext ctx)
    {
        if (current != null) current.Interact();
        else if (currentPreview != null && currentPreview.CanView(cam)) currentPreview.View();
    }
}
