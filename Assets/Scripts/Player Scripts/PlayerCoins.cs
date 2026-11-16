using System;
using UnityEngine;

// Wulang's Wubucks wallet. Coins from pickups and vampire drops go in; Abe's shop takes them out.
public class PlayerCoins : MonoBehaviour
{
    public int Count { get; private set; }
    public event Action<int> OnChanged;

    public void Collect() => Add(1);

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Count += amount;
        OnChanged?.Invoke(Count);
    }

    public bool Spend(int amount)
    {
        if (amount < 0 || amount > Count) return false;
        Count -= amount;
        OnChanged?.Invoke(Count);
        return true;
    }
}
