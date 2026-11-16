using UnityEngine;

// Scatters Wubucks coins when the enemy it sits on dies. Vampires carry it; their respawn
// timer (CombatTarget) lets the player farm a little for Abe's potions.
[RequireComponent(typeof(CombatTarget))]
public class LootDrop : MonoBehaviour
{
    [SerializeField] private GameObject coinPrefab;
    [SerializeField, Min(0)] private int minCoins = 1;
    [SerializeField, Min(0)] private int maxCoins = 3;

    private CombatTarget target;

    private void Awake() => target = GetComponent<CombatTarget>();
    private void OnEnable() => target.Died += Drop;
    private void OnDisable() => target.Died -= Drop;

    private void Drop()
    {
        if (coinPrefab == null) return;
        int count = Random.Range(minCoins, maxCoins + 1);
        for (int i = 0; i < count; i++)
        {
            var offset = Random.insideUnitCircle * 1.2f;
            var position = target.transform.position + new Vector3(offset.x, 0f, offset.y);
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;
            // Coins live in the region's scene so they unload with it.
            var coin = Instantiate(coinPrefab, position + Vector3.up * 1.2f, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(coin, gameObject.scene);
        }
    }
}
