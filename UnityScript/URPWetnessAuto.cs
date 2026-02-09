using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class URPWetnessAuto : MonoBehaviour
{
    [Header("Roots ที่จะทำให้เปียก (ลากได้หลายอัน)")]
    public Transform[] roots;

    [Range(0, 1f)]
    public float wetness = 1f;

    [Header("Look")]
    public float drySmoothness = 0.05f;
    public float wetSmoothness = 0.9f;
    public float darkenAmount = 0.15f;
    public float wetMetallic = 0.05f;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");
    static readonly int MetallicID = Shader.PropertyToID("_Metallic");

    // เก็บค่าเดิมของ "shared material" (ตัวต้นฉบับ)
    private readonly Dictionary<Material, Original> originalBySharedMat = new();
    private readonly List<Material> collectedSharedMats = new();
    private bool collected = false;

    struct Original
    {
        public Color baseColor;
        public float smoothness;
        public float metallic;
    }

    void OnEnable()
    {
        CollectOnce();
        ApplyWetness();
    }

    void OnDisable()
    {
        // กันค้างเปียกตอนปิด object/ออก play mode
        RestoreDry();
    }

    // เรียกจากข้างนอกได้ เช่นตอน Toggle Rain
    public void SetWetness(float value01)
    {
        wetness = Mathf.Clamp01(value01);
        CollectOnce();
        ApplyWetness();
    }

    public void RestoreDry()
    {
        if (!collected) return;

        foreach (var m in collectedSharedMats)
        {
            if (!m) continue;
            if (!originalBySharedMat.TryGetValue(m, out var o)) continue;

            if (m.HasProperty(BaseColorID)) m.SetColor(BaseColorID, o.baseColor);
            if (m.HasProperty(SmoothnessID)) m.SetFloat(SmoothnessID, o.smoothness);
            if (m.HasProperty(MetallicID)) m.SetFloat(MetallicID, o.metallic);
        }
    }

    private void CollectOnce()
    {
        if (collected) return;

        if (roots == null || roots.Length == 0)
        {
            Debug.LogError("URPWetnessAuto: roots ว่าง");
            return;
        }

        collectedSharedMats.Clear();
        originalBySharedMat.Clear();

        int rCount = 0;
        int mCount = 0;

        foreach (var root in roots)
        {
            if (!root) continue;

            // ถ้าอยากให้ SkinnedMeshRenderer เปียกด้วย เพิ่ม SkinnedMeshRenderer ได้
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            rCount += renderers.Length;

            foreach (var r in renderers)
            {
                if (!r) continue;

#if UNITY_EDITOR
                // ข้าม prefab asset ใน project (ไม่ใช่ของใน scene)
                if (PrefabUtility.IsPartOfPrefabAsset(r)) continue;
#endif

                var mats = r.sharedMaterials; // ✅ shared
                if (mats == null) continue;

                foreach (var m in mats)
                {
                    if (!m) continue;

                    // เฉพาะ URP/Lit
                    if (!m.shader || !m.shader.name.Contains("Universal Render Pipeline/Lit"))
                        continue;

                    if (originalBySharedMat.ContainsKey(m)) continue;

                    // cache original
                    var o = new Original
                    {
                        baseColor = m.HasProperty(BaseColorID) ? m.GetColor(BaseColorID) : Color.white,
                        smoothness = m.HasProperty(SmoothnessID) ? m.GetFloat(SmoothnessID) : 0f,
                        metallic = m.HasProperty(MetallicID) ? m.GetFloat(MetallicID) : 0f
                    };

                    originalBySharedMat[m] = o;
                    collectedSharedMats.Add(m);
                    mCount++;
                }
            }
        }

        collected = true;
        Debug.Log($"[URPWetnessAuto] Collected Renderers={rCount}, SharedMats={mCount}");
    }

    public void ApplyWetness()
    {
        if (!collected) return;

        foreach (var m in collectedSharedMats)
        {
            if (!m) continue;
            if (!originalBySharedMat.TryGetValue(m, out var o)) continue;

            // สีเข้มลงนิด + เงามันขึ้น
            Color wetColor = Color.Lerp(
                o.baseColor,
                o.baseColor * (1f - darkenAmount),
                wetness
            );

            float s = Mathf.Lerp(drySmoothness, wetSmoothness, wetness);
            float met = Mathf.Lerp(o.metallic, wetMetallic, wetness);

            if (m.HasProperty(BaseColorID)) m.SetColor(BaseColorID, wetColor);
            if (m.HasProperty(SmoothnessID)) m.SetFloat(SmoothnessID, s);
            if (m.HasProperty(MetallicID)) m.SetFloat(MetallicID, met);
        }
    }
}
