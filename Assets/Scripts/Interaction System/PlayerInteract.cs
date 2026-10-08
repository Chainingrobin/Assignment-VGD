using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] InputActionReference interactAction;
    [SerializeField] TMP_Text promptText;
    [SerializeField] float range = 3f;

    Interactable current;
    Collider lastHit;

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
            promptText.fontSize = 28f;
            promptText.raycastTarget = false;

            var promptRect = (RectTransform)promptText.transform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0f, -48f);
            promptRect.sizeDelta = new Vector2(440f, 56f);
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

            if (hit.collider != lastHit)
            {
                lastHit = hit.collider;

                // print the chain the script searched: hit object -> its parents
                string chain = "";
                for (Transform t = hit.collider.transform; t != null; t = t.parent)
                    chain += t.name + " > ";
                Debug.Log($"Ray hit: {chain}| Interactable found: {(found != null)}", hit.collider);

                Debug.Log($"Interactables in scene: {FindObjectsByType<Interactable>(FindObjectsSortMode.None).Length}");
            }

            if (found != null && found.CanInteract) target = found;
        }
        else if (lastHit != null)
        {
            lastHit = null;
        }

        if (target != current)
        {
            if (current != null) current.SetHighlighted(false);
            current = target;
            if (current != null) current.SetHighlighted(true);
        }

        if (promptText == null) return;
        promptText.gameObject.SetActive(current != null);
        if (current != null)
            promptText.text = current.Prompt.Replace("F", "<mark=#245A8D><b> F </b></mark>");
    }

    void OnInteract(InputAction.CallbackContext ctx)
    {
        Debug.Log($"F pressed. Target: {(current != null ? current.name : "none")}");
        if (current != null) current.Interact();
    }
}
