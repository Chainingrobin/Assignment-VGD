using UnityEngine;

// A Wubucks pickup: a single coin, or a bag worth several. A coin stands on its edge and turns about the
// world's up axis (FloatingPickup spins it), whatever rotation it was dropped or placed with.
[DefaultExecutionOrder(-50)] // before FloatingPickup records the rest pose it spins around
public class CoinCollect : MonoBehaviour
{
    [SerializeField, Min(1)] private int amount = 1;
    [Tooltip("Stand the coin upright on its edge (the coin model lies flat at rest).")]
    [SerializeField] private bool standUpright;
    private bool collected;

    private void Awake()
    {
        if (standUpright) transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var wallet = other.GetComponentInParent<PlayerCoins>();
        if (wallet == null) return;
        collected = true;
        wallet.Add(amount);
        GameAudio.Play("Sfx/Coin", null, 0.5f);
        Destroy(gameObject);
    }
}
