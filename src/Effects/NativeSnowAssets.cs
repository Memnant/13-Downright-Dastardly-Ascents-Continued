using System;
using System.IO;
using UnityEngine;

namespace dda;

// Exact Alpine visual assets copied from the supported local game build. They are
// loaded once per plugin lifetime, never by loading/activating an Alpine scene.
internal static class NativeSnowAssets
{
    private static AssetBundle bundle;
    private static GameObject particlesPrefab;
    internal static Texture2D FogTexture { get; private set; }

    internal static ParticleSystem InstantiateParticles(Transform parent)
    {
        EnsureLoaded();
        var instance = UnityEngine.Object.Instantiate(particlesPrefab, parent, false);
        instance.name = "Particle System";
        return instance.GetComponent<ParticleSystem>();
    }

    private static void EnsureLoaded()
    {
        if (bundle != null) return;
        try
        {
            using (var stream = typeof(Plugin).Assembly.GetManifestResourceStream("dda.NativeAlpineSnow.bundle"))
            {
                if (stream == null) throw new InvalidOperationException("Native Alpine snow resource is missing.");
                using var reader = new BinaryReader(stream);
                bundle = AssetBundle.LoadFromMemory(reader.ReadBytes(checked((int)stream.Length)));
            }
            if (bundle == null) throw new InvalidOperationException("Native Alpine snow bundle could not be loaded.");
            particlesPrefab = bundle.LoadAsset<GameObject>("assets/continued/native-alpine-snow.prefab");
            FogTexture = bundle.LoadAsset<Texture2D>("assets/continued/native-alpine-fog.texture");
            if (particlesPrefab == null || particlesPrefab.GetComponent<ParticleSystem>() == null || FogTexture == null)
                throw new InvalidOperationException("Native Alpine snow assets are incomplete.");
            var material = particlesPrefab.GetComponent<ParticleSystemRenderer>()?.sharedMaterial;
            if (material == null || material.name != "M_VFX_Snow" || material.shader == null || material.shader.name != "Storm")
                throw new InvalidOperationException("Native Alpine snow material or shader is missing.");
        }
        catch { Release(); throw; }
    }

    internal static void Release()
    {
        particlesPrefab = null; FogTexture = null;
        if (bundle != null) bundle.Unload(true);
        bundle = null;
    }
}
