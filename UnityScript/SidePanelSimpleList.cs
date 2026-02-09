using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SidePanelSimpleList : MonoBehaviour
{
    [Header("UI")]
    public RectTransform contentRoot;
    public Button itemPrefab;

    [Header("Items")]
    public List<string> items = new()
    {
        "Steering","Headlight","Turnlight","Break","Reverselight","Door","Gear","Speed","Brakelight","Windshield Wiper","Air"
    };

    [Header("Visual")]
    [Tooltip("สีตอนยังไม่เลือก (มืด)")]
    public Color dimColor = new Color(1f, 1f, 1f, 0.35f);

    public SidePanel3DPreviewManager previewManager;

    [Tooltip("สีตอนเลือก (สว่าง)")]
    public Color brightColor = new Color(1f, 1f, 1f, 1f);

    // เก็บปุ่มที่ถูกเลือกอยู่ (ได้แค่ 1)
    private Button selectedButton = null;

    void Start() => Build();

    public void Build()
    {
        if (!contentRoot || !itemPrefab)
        {
            Debug.LogError("SidePanelSimpleList: contentRoot/itemPrefab not assigned");
            return;
        }

        // ลบของเก่า
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        selectedButton = null; // เริ่มต้นไม่มีการเลือก

        // สร้างปุ่ม
        foreach (var name in items)
        {
            var btn = Instantiate(itemPrefab, contentRoot);

            // ตั้งชื่อ text
            var tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp) tmp.text = name;

            // เริ่มต้นให้ "มืด"
            SetVisual(btn, selected: false);

            // กัน closure
            string captured = name;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnItemClicked(btn, captured));

            // ถ้าคุณอยากใช้ ItemClickDebug ด้วย ให้แนบ component นี้ไว้ที่ prefab แล้ว set itemName
            var debug = btn.GetComponent<ItemClickDebug>();
            if (debug) debug.itemName = captured;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void OnItemClicked(Button btn, string itemName)
    {
        if (selectedButton == btn)
        {
            SetVisual(btn, false);
            selectedButton = null;

            if (PanelSelectionGate.Instance) PanelSelectionGate.Instance.Select(itemName);

            // toggle off -> ซ่อนพรีวิว
            if (previewManager) previewManager.ShowByName("");
            return;
        }

        if (selectedButton != null)
            SetVisual(selectedButton, false);

        selectedButton = btn;
        SetVisual(btn, true);

        if (PanelSelectionGate.Instance) PanelSelectionGate.Instance.Select(itemName);

        // เลือกอันใหม่ -> อัปเดตพรีวิว
        if (previewManager) previewManager.ShowByName(itemName);
    }

    private void SetVisual(Button btn, bool selected)
    {
        // วิธีง่ายสุด: เปลี่ยนสี Image ของปุ่ม (ต้องมี Image ที่ Button ตัวนั้น)
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = selected ? brightColor : dimColor;
        }

        // ถ้าตัวหนังสืออยากให้มืดด้วย สามารถปรับ TMP_Text เพิ่มได้
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            // ให้ตัวหนังสือจางลงเมื่อไม่เลือก
            var c = tmp.color;
            c.a = selected ? 1f : 0.55f;
            tmp.color = c;
        }
    }

    private void CallItemDebug(Button btn)
    {
        var debug = btn.GetComponent<ItemClickDebug>();
        if (debug != null)
        {
            debug.OnItemClicked();
        }
    }
}
