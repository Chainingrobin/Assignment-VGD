using UnityEngine;

// A Wubucks pickup: a single coin, or a bag worth several.
public class CoinCollect : MonoBehaviour
{
    [SerializeField, Min(1)] private int amount = 1;
    private bool collected;
    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var wallet = other.GetComponentInParent<PlayerCoins>();
        if (wallet == null) return;
        collected = true;
        wallet.Add(amount);
        Destroy(gameObject);
    }
}
