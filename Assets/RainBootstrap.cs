using UnityEngine;

/// <summary>
/// The scene file has repeatedly lost the hand-placed "RainEffect" GameObject
/// (external editor saves reverting it away), so instead of depending on the
/// scene remembering it, the rain effect now creates itself in code the
/// moment any scene loads - this makes it immune to that whole class of bug.
/// Its materials are loaded from Resources so this works both in the Editor
/// and in a real build.
///
/// Ordering note: the GameObject is created INACTIVE first, so AddComponent
/// does not fire RainEffect.Awake() yet. That lets us assign rainMaterial /
/// splashMaterial before Awake ever runs; only then do we activate it, which
/// is what actually triggers Awake(). Doing AddComponent while already active
/// fires Awake() immediately (before the material fields below are set),
/// which built the particle systems with null materials - that is what was
/// rendering as pink (URP's "no material assigned" fallback color).
/// </summary>
public static class RainBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRainExists()
    {
        if (Object.FindFirstObjectByType<RainEffect>() != null) return; // already present, don't duplicate

        var go = new GameObject("RainEffect (auto-created)");
        go.SetActive(false); // delay Awake() until materials are assigned below

        var effect = go.AddComponent<RainEffect>();
        effect.rainMaterial = Resources.Load<Material>("RainStreak_Particle");
        effect.splashMaterial = Resources.Load<Material>("Splash_Particle");

        if (effect.rainMaterial == null || effect.splashMaterial == null)
        {
            Debug.LogWarning("RainBootstrap: couldn't load rain/splash materials from Resources - rain will be invisible (missing material).");
        }

        go.SetActive(true); // now Awake() runs, with materials already in place
    }
}
