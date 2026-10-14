using UnityEngine;

/// <summary>
/// Procedurally builds a falling-rain particle system plus a ground-splash
/// particle system at runtime. Attach this to an empty GameObject, assign
/// rainMaterial / splashMaterial, and tweak the public fields to taste.
///
/// Timing note: every drop falls at the SAME fixed speed and lives for
/// EXACTLY (spawnHeight - groundY) / fallSpeed seconds, with zero
/// randomization. That is deliberate: this is what guarantees a drop's
/// "Death" sub-emitter event always fires exactly at groundY, never mid-air.
/// (An earlier version randomized fall speed and lifetime independently,
/// which let a slow-speed/short-lifetime drop die high in the air; a later
/// version tried a physics Collision module instead, which doesn't play
/// well with Prewarm and wasn't reliably landing on the thin ground band.
/// Fixed-speed timing sidesteps both problems entirely.)
///
/// Depth note: this game is a 2.5D side-scroller - the Floor is a single
/// flat, camera-facing quad with zero real depth; its sense of distance
/// comes entirely from its texture, not from actual 3D depth. So the rain's
/// own Z spread (`depth`) is kept tiny and centered on the Floor's Z, not
/// stretched out in 3D - any noticeable Z spread puts some drops' screen
/// Y position off from groundY's true screen row, due to perspective, which
/// is what made splashes look smeared across the ground (depth=20), and
/// later still visibly poking above/below the ground line (depth=3 was
/// still enough spread to cause a multi-pixel mismatch at this camera
/// distance). depth is now kept near-zero so every drop's Z closely matches
/// the Floor's own Z=0, eliminating the perspective mismatch entirely.
/// </summary>
[DisallowMultipleComponent]
public class RainEffect : MonoBehaviour
{
    [Header("Area (world units, centered on this object's X/Z)")]
    public float width = 55f;
    public float depth = 0.1f;   // kept tiny on purpose - see class doc's Depth note
    public float spawnHeight = 6f;
    public float groundY = -2f;   // Floor Y is now -7 -> top edge = -7 + 5 = -2

    [Header("Rain streaks")]
    public Material rainMaterial;
    public float rateOverTime = 2500f;
    public float fallSpeed = 20f;
    public float streakWidth = 0.09f;
    public float streakLengthScale = 3.5f; // desired visible streak length, in world units (see renderer.velocityScale below)
    [Range(0f, 1f)] public float rainAlpha = 0.85f;

    [Header("Ground splashes (spawned only where a raindrop actually lands)")]
    public Material splashMaterial;
    public int splashParticlesPerDrop = 1;
    public float splashLifetime = 0.25f;
    public float splashStartSize = 0.35f;

    ParticleSystem rainPS;
    ParticleSystem splashPS;

    void Awake()
    {
        // splashPS must exist before BuildRain() wires it up as a sub-emitter.
        BuildSplashes();
        BuildRain();
    }

    void BuildRain()
    {
        var go = new GameObject("RainStreaks_Generated");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, spawnHeight - transform.position.y, 0f);

        rainPS = go.AddComponent<ParticleSystem>();
        float fallTime = Mathf.Max(0.05f, (spawnHeight - groundY) / Mathf.Max(0.01f, fallSpeed));

        var main = rainPS.main;
        main.loop = true;
        main.prewarm = true; // safe now: Death timing is exact, no Collision module involved
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(fallTime); // fixed, not randomized - see class doc
        main.startSpeed = 0f; // actual motion comes from velocityOverLifetime (world space, straight down)
        main.startSize = new ParticleSystem.MinMaxCurve(streakWidth * 0.8f, streakWidth * 1.3f);
        main.startColor = new Color(0.78f, 0.82f, 0.88f, rainAlpha);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 8000;
        main.gravityModifier = 0f;

        var emission = rainPS.emission;
        emission.enabled = true;
        emission.rateOverTime = rateOverTime;

        var shape = rainPS.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.1f, depth);

        var vol = rainPS.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        // All three axes must use the SAME MinMaxCurve mode (Unity requirement).
        vol.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vol.y = new ParticleSystem.MinMaxCurve(-fallSpeed, -fallSpeed);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        // Stretch billboards are stretched by (particleSize * lengthScale) + (speed * velocityScale).
        // Every drop here shares the exact same fixed fallSpeed, so we drive the
        // streak's length entirely from velocityScale and leave lengthScale at 0 -
        // otherwise the streak length is tied to particle SIZE, which is tiny
        // (streakWidth ~0.05), making the streak effectively invisible regardless
        // of how fast it's actually falling. velocityScale = desiredLength / fallSpeed
        // gives a streak exactly streakLengthScale world units long.
        renderer.lengthScale = 0f;
        renderer.velocityScale = streakLengthScale / Mathf.Max(0.01f, fallSpeed);
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (rainMaterial != null) renderer.material = rainMaterial;

        // A drop's lifetime is tuned above to end exactly as it reaches
        // groundY, so "Death" doubles as "landed on the ground band".
        var sub = rainPS.subEmitters;
        sub.enabled = true;
        sub.AddSubEmitter(splashPS, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing, 1f);
    }

    void BuildSplashes()
    {
        var go = new GameObject("RainSplashes_Generated");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        splashPS = go.AddComponent<ParticleSystem>();

        var main = splashPS.main;
        main.loop = false;
        main.prewarm = false;
        main.playOnAwake = false; // only ever triggered as a sub-emitter, never on its own
        main.startLifetime = new ParticleSystem.MinMaxCurve(splashLifetime * 0.8f, splashLifetime * 1.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(splashStartSize * 0.7f, splashStartSize * 1.2f);
        main.startColor = new Color(0.85f, 0.9f, 0.95f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;
        main.gravityModifier = 0f;

        // No independent emission over time and no shape offset: every splash
        // particle is created exactly at the inherited ground-contact point of
        // the raindrop that triggered it, via a single time-0 burst.
        var emission = splashPS.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)splashParticlesPerDrop) });

        var shape = splashPS.shape;
        shape.enabled = false;

        var sol = splashPS.sizeOverLifetime;
        sol.enabled = true;
        var growCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.3f, 1f),
            new Keyframe(1f, 1.4f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, growCurve);

        var col = splashPS.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (splashMaterial != null) renderer.material = splashMaterial;
    }
}
