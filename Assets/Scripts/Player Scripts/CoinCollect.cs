using UnityEngine;

public class CoinCollect : MonoBehaviour
{
    private bool collected;
    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var wallet = other.GetComponentInParent<PlayerCoins>();
        if (wallet == null) return;
        collected = true;
        wallet.Collect();
        Destroy(gameObject);
    }
}
