public interface IDamageable
{
    void TakeDamage(float amount);
    bool IsTargetable { get; }
}
