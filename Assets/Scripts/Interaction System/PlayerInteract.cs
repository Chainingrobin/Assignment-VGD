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
    }

    void OnEnable()
    {
        interactAction.action.Enable();
        interactAction.action.performed += OnInteract;
    }

    void OnDisable()
    {
        interactAction.action.performed -= OnInteract;
    }

    void Update()
    {
        current = null;
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

            if (found != null && found.CanInteract) current = found;
        }
        else if (lastHit != null)
        {
            lastHit = null;
        }

        promptText.gameObject.SetActive(current != null);
        if (current != null) promptText.text = current.Prompt;
    }

    void OnInteract(InputAction.CallbackContext ctx)
    {
        Debug.Log($"F pressed. Target: {(current != null ? current.name : "none")}");
        if (current != null) current.Interact();
    }
}