// Anything that can be hit and take damage implements this.
// sourceElement is optional context for future weakness/resistance logic.
public interface IDamageable
{
    void TakeDamage(float amount, ElementType sourceElement = ElementType.None);
}
