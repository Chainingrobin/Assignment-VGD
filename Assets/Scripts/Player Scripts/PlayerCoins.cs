using System;
using UnityEngine;

public class PlayerCoins : MonoBehaviour
{
    public int Count { get; private set; }
    public event Action<int> OnChanged;
    public void Collect()
    {
        Count++;
        OnChanged?.Invoke(Count);
    }
}
