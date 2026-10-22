using UnityEngine;

// One pair of pools shared by every affinity. Switching never touches this state.
public sealed class SpellCharges
{
    public const int ProjectileCapacity = 15;
    public const int AreaCapacity = 5;
    public const float ReloadDuration = 3f;
    public const float AreaRecoveryDuration = 10f;

    public int Projectiles { get; private set; } = ProjectileCapacity;
    public int Areas { get; private set; } = AreaCapacity;
    public float ReloadRemaining { get; private set; }
    public float RecoveryElapsed { get; private set; }
    public bool IsReloading => ReloadRemaining > 0f;

    public bool SpendProjectile()
    {
        if (IsReloading || Projectiles == 0) return false;
        Projectiles--;
        return true;
    }

    public bool SpendArea()
    {
        if (Areas == 0) return false;
        Areas--;
        return true;
    }

    public void Reload()
    {
        if (!IsReloading && Projectiles < ProjectileCapacity) ReloadRemaining = ReloadDuration;
    }

    public void Advance(float seconds)
    {
        if (seconds <= 0f) return;
        if (IsReloading)
        {
            ReloadRemaining = Mathf.Max(0f, ReloadRemaining - seconds);
            if (!IsReloading) Projectiles = ProjectileCapacity;
        }
        if (Areas == AreaCapacity) return;
        RecoveryElapsed += seconds;
        while (RecoveryElapsed >= AreaRecoveryDuration && Areas < AreaCapacity)
        {
            RecoveryElapsed -= AreaRecoveryDuration;
            Areas++;
        }
        if (Areas == AreaCapacity) RecoveryElapsed = 0f;
    }
}
