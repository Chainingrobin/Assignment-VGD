# Combat training

Open `SampleScene`, enter Play mode, collect the existing elemental books, and approach the four vampires in front of the armory. The `Combat training` hierarchy contains Fire, Ice, Earth and Lightning targets. They have 100 HP, idle in place, react to spells, and return to their starting positions five seconds after defeat. They do not attack.

## Controls and resources

- Q/E: cycle unlocked elements. LMB: projectile. RMB: area spell beneath the player. R: reload.
- All elements share 15 projectile charges and 5 area charges. Switching does not refill either pool.
- Reload takes 3 seconds and blocks only projectiles. Area charges recover one every 10 seconds.
- Projectile interval: 0.35 seconds. Area interval: 1 second. A failed ground cast spends nothing.

## Starting balance

Projectiles deal 20 damage; areas deal 40 within 5 m. Same-element targets resist at 0.5x. Fire/Ice and Earth/Lightning oppose each other at 1.5x. Other hits deal 1x.

- Ice freezes for 3 seconds. Fire burns for 4 HP/second for 4 seconds. Both refresh without stacking; Burn damage is flat and does not receive another affinity multiplier.
- Earth applies 18 horizontal impulse units and a brief stagger; Earth on Frozen increases the impulse to 27 and preserves Freeze. Targets have mass 2 and damping 2 for a visible push.
- Lightning reaches at most two extra targets, each within 4 m of the preceding target, at half base damage with that target's affinity multiplier. Bright animated chains follow targets for 1.5 seconds and fade out; the lingering visual never repeats damage. Walls block arcs. Direct area hits retain full damage and targets cannot be hit twice by one cast.
- Lightning on Burning transfers the remaining Burn duration through its chain, without shortening an existing longer Burn.
- Fire on Frozen consumes Freeze, applies Burn, and produces a visible 2 m radius steam cloud lasting 7 seconds. Steam blocks enemy sight, including when the enemy's eyes are inside the cloud.
- Ice on Burning consumes Burn, adds 10 damage after the affinity multiplier, and applies Freeze.
- Earth leaves a 4.5-second Grounded mark and a 0.65-second stagger; Lightning leaves a 4.5-second Charged mark and a 1.25-second stun. Following either element with the other consumes these marks and produces a 3-second seismic stun with a larger purple burst and brighter orbiting sparks.

Permanent affinity tint and particles identify the variant. A separate ice coating, larger flames, recoil, and stun sparks show hits and statuses. Hit flash lasts 0.25 seconds, recoil lasts 0.4 seconds, and reaction text stays for 1.8 seconds with an initial size pulse. A compact health bar, status word, and reaction text follow each enemy's animated head. The player HUD contains only compact spell counts and recovery indicators.

An exclamation mark appears above an enemy when the player is within its 18 m, 120-degree view cone with a clear line of sight. Walls and steam hide the alert immediately; it returns when sight clears. Detection does not make enemies chase or attack.

## Checks and tuning

In Play mode, run **Wulang > Combat > Run combat checks (Play mode)**. Checks use temporary isolated targets and cover matchups, resources, status refresh/expiry, reactions, chain limits, walls, steam occlusion/expiry, duplicate/child colliders, defeat, reset and scene wiring. `CombatChecks.BeginKnockbackCheck()` measures ordinary and frozen-target displacement in live physics. `CombatChecks.BeginArcCheck()` checks that chains remain visible, follow moving targets, avoid repeating damage and clean up their renderers and lights.

Tune projectile/area damage and cast intervals on the player's `SpellCaster`; capacities and recovery live in `SpellCharges`. Status and reaction values live in `CombatTarget`; chain range and count live in `SpellHit`. Target health and reset timing are Inspector fields on the prefabs.

Melee AI, attack animations, player health, consumables, drops, rest and hub protection are later milestones. Combat values require another balance pass once enemies move and attack.

## Vampire melee prototype
Vampires now pursue visible players using physics, blend Idle/Walk forward/Run forward, and play a melee swipe within 1.8 m. Attacks cause no player damage. Steam and walls stop pursuit; freeze/stun interrupt attacks. Extracted Humanoid clips are in Vampire animations; source FBXs are retained for reimport. This is direct pursuit, without navigation around obstacles. Run Wulang > Combat > Check vampire animation wiring to validate avatars and clips.


Vampires roam within an 8 m home territory, pause between walks and occasionally play the imported flexing-muscles display. Losing sight starts a 1-second idle before roaming resumes. Pursuit respects the home territory. The Melee swipe state now references mutant swiping.fbx directly; Display uses flexing muscles because the pack has no named shout clip. Short crossfades connect display, attacks and locomotion. Attacks remain harmless.


Attack uses an upper-body Avatar mask over stationary idle legs and waits for facing alignment. Death uses the imported dying clip, stays visible until training respawn, and restores locomotion on reset. Close detection works behind the vampire within 1.5 m of its head, through steam but not solid walls. Lightning has bright blue electric surfaces across its armor and periodic shoulder-to-hand bolts. Run Wulang > Combat > Check grounded attack and death (Play mode) for isolated regression checks.


Latest tuning: full-body mutant swipe restored with extracted horizontal/root rotation and applyRootMotion disabled. Actor stays stationary and holds its facing during the complete swipe; legs animate naturally. Four-second hesitation after steam breaks detection; ordinary sight loss pauses 1.5 seconds. Movement acceleration is 4 m/s/s with 0.35-second animation damping. Overhead UI omits affinity/status/reaction names while retaining health, alert and numeric damage. Lightning uses purple surface/emission with its existing electrical bolts. The in-place attack check verifies moving legs and a stationary actor, replacing the earlier masked-leg check.


Chase fix: reacquiring the player clears pauseRemaining immediately. Swipe now preserves original orientation and bakes its rotation into the full-body pose. A physics-driven forward step runs from 0.95 to 1.4 seconds at up to 1.5 m/s, stopping within 1.05 m of the player; recovery stops horizontal movement. Animator root translation stays disabled to avoid double movement. Run Check chase recovery and lunge (Play mode) for pause recovery and strike-window assertions.


Pursuit domain now has a separate 40 m radius around spawn, while roaming remains 8 m. Initial sight acquisition remains 18 m; alerted tracking extends to 40 m with the same view cone, wall and steam checks. Visible players inside the pursuit domain cancel returning/pauses/displays. Grounded chase regression runs 350 physics steps and asserts movement beyond 15 m without flexing.

