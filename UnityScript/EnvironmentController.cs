using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnvironmentController : MonoBehaviour
{
    [Serializable]
    public class Item
    {
        public string label;        // ข้อความบนปุ่ม (ตั้งอะไรก็ได้)
        public string tag;          // Tag ของ object ที่จะเปิด/ปิด
        public bool startActive;    // ค่าเริ่มต้นให้ object เป็น Active ไหม
        public Sprite icon;         // ✅ รูปของปุ่ม (แยกแต่ละปุ่มได้)
    }

    [Header("UI")]
    public RectTransform contentRoot;
    public Button itemPrefab;

    [Header("Child names in Button Prefab")]
    public string iconChildName = "Icon";
    public string borderChildName = "Border";
    public string labelChildName = "Text (TMP)"; // ถ้าชื่อไม่ตรง เปลี่ยนได้

    [Header("Items (Label != Tag ได้)")]
    public List<Item> items = new();

    [Header("Border Colors (Hold by state)")]
    public Color activeBorderColor = Color.white;                       // ✅ active = ขาว
    public Color inactiveBorderColor = new Color(0.6f, 0.6f, 0.6f, 1f);  // ✅ inactive = เทา

    [Header("Optional: Wetness Link")]
    public URPWetnessAuto wetnessController;
    public string wetnessTag = "Rain";
    public float wetWhenOn = 1f;
    public float wetWhenOff = 0f;

    // tag -> active state
    private readonly Dictionary<string, bool> activeByTag = new();

    // tag -> cached targets (รวม inactive ด้วยการ cache ตอนเริ่ม)
    private readonly Dictionary<string, List<GameObject>> targetsByTag = new();

    // tag -> ui refs
    private class UiRefs
    {
        public Button button;
        public Image iconImage;
        public Image borderImage;
        public TMP_Text labelText;
    }
    private readonly Dictionary<string, UiRefs> uiByTag = new();

    void Start() => Build();

    public void Build()
    {
        if (!contentRoot || !itemPrefab)
        {
            Debug.LogError("EnvironmentController: contentRoot/itemPrefab not assigned");
            return;
        }

        // ลบของเก่า
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        activeByTag.Clear();
        targetsByTag.Clear();
        uiByTag.Clear();

        // 1) cache targets + set initial active
        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.tag)) continue;

            // cache objects ที่มี tag ตอนเริ่ม (ตอนนี้ยังหาเจอ)
            var list = new List<GameObject>(GameObject.FindGameObjectsWithTag(it.tag));
            targetsByTag[it.tag] = list;

            // ตั้งค่า active เริ่มต้น
            activeByTag[it.tag] = it.startActive;
            foreach (var go in list)
                if (go) go.SetActive(it.startActive);
        }

        // 2) สร้างปุ่ม + ผูก icon/border/label
        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.tag)) continue;

            var btn = Instantiate(itemPrefab, contentRoot);

            // ✅ กัน Button มาทับสี (สำคัญ)
            btn.transition = Selectable.Transition.None;

            // หา child references
            var refs = new UiRefs();
            refs.button = btn;

            var iconTf = btn.transform.Find(iconChildName);
            if (iconTf) refs.iconImage = iconTf.GetComponent<Image>();

            var borderTf = btn.transform.Find(borderChildName);
            if (borderTf) refs.borderImage = borderTf.GetComponent<Image>();

            // label (หา TMP ในลูก ๆ)
            refs.labelText = btn.GetComponentInChildren<TMP_Text>(true);

            // set label
            if (refs.labelText)
                refs.labelText.text = string.IsNullOrEmpty(it.label) ? it.tag : it.label;

            // set icon (✅ แก้ปัญหา icon เหมือนกันหมด)
            if (refs.iconImage)
                refs.iconImage.sprite = it.icon;

            uiByTag[it.tag] = refs;

            // set initial border color (ค้างตาม state)
            ApplyBorderVisual(it.tag);

            // onClick
            string capturedTag = it.tag;
            btn.onClick.AddListener(() => ToggleTag(capturedTag));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private const string TAG_CAR = "Car";
    private const string TAG_LIGHT_CAR = "Light_Car";

    private void ToggleTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || !activeByTag.ContainsKey(tag))
            return;

        bool newActive = !activeByTag[tag];

        // ✅ Rule: ห้ามเปิดไฟ ถ้ารถปิดอยู่
        if (tag == TAG_LIGHT_CAR && newActive)
        {
            if (activeByTag.TryGetValue(TAG_CAR, out bool carActive) && !carActive)
            {
                Debug.LogWarning("[Environment] Blocked: cannot enable Light_Car while Car is OFF.");
                // คงสถานะเดิมไว้ แล้วอัปเดต UI ให้ตรง
                ApplyBorderVisual(tag);
                return;
            }
        }

        // อัปเดต state ของ tag ที่กด
        activeByTag[tag] = newActive;

        // เปิด/ปิด object ของ tag นั้น
        ApplyActiveToTag(tag, newActive);

        // ✅ Rule: ถ้าปิด Car -> ต้องปิด Light_Car อัตโนมัติ
        if (tag == TAG_CAR && newActive == false)
        {
            if (activeByTag.TryGetValue(TAG_LIGHT_CAR, out bool lightActive) && lightActive)
            {
                activeByTag[TAG_LIGHT_CAR] = false;
                ApplyActiveToTag(TAG_LIGHT_CAR, false);
                ApplyBorderVisual(TAG_LIGHT_CAR);
            }
        }

        // อัปเดต UI ของปุ่มที่กด
        ApplyBorderVisual(tag);

        // wetness เดิม
        if (wetnessController != null && tag == wetnessTag)
            wetnessController.SetWetness(newActive ? wetWhenOn : wetWhenOff);

        Debug.Log($"[Environment] {tag} Active = {newActive}");
    }

    private void ApplyActiveToTag(string tag, bool active)
    {
        if (!targetsByTag.TryGetValue(tag, out var targets)) return;

        foreach (var go in targets)
        {
            if (!go) continue;

            // ถ้ายังใช้ HasIllegalShapes อยู่ก็ใส่ไว้ได้ (กัน PhysX)
            if (active && HasIllegalShapes(go))
            {
                Debug.LogWarning($"[Environment] Skip enabling '{go.name}' because it has illegal PhysX shapes.", go);
                continue;
            }

            go.SetActive(active);
        }
    }

    private void ApplyBorderVisual(string tag)
    {
        if (!uiByTag.TryGetValue(tag, out var refs)) return;
        if (!refs.borderImage) return;

        bool isActive = activeByTag.TryGetValue(tag, out var a) && a;

        // ✅ สีขอบค้างตามสถานะ
        refs.borderImage.color = isActive ? activeBorderColor : inactiveBorderColor;
    }

    static bool HasIllegalShapes(GameObject go)
    {
        // ถ้ามี Rigidbody ที่ไหนใต้ go
        var rb = go.GetComponentInChildren<Rigidbody>(true);
        if (!rb) return false;

        var mcs = go.GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in mcs)
        {
            if (!mc || !mc.enabled) continue;
            if (!mc.convex && !mc.isTrigger) return true;
        }
        return false;
    }
}
