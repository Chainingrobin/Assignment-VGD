using UnityEngine;

public class BookCollect : MonoBehaviour
{
    [SerializeField] private ElementType element; // set this per-book in the Inspector
    [SerializeField] private AudioClip pickupSound; // optional

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerMagicAffinity.Instance.UnlockElement(element);

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        Destroy(gameObject);
    }
}