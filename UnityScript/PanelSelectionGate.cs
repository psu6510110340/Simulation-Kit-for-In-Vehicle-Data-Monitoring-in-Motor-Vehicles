using UnityEngine;

public class PanelSelectionGate : MonoBehaviour
{
    public static PanelSelectionGate Instance { get; private set; }

    // ✅ ให้สคริปต์อื่นอ่านชื่อเมนูที่ถูกเลือกได้
    public string SelectedName { get; private set; } = "";

    [Header("Debug")]
    [SerializeField] private string currentSelected = "";

    // ของเดิมยังใช้ได้
    public string CurrentSelected => currentSelected;

    [Header("Logging")]
    public bool logGate = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ✅ ซิงก์ตอนเริ่ม
        SyncSelectedName();
    }

    public bool IsSelected(string name) => currentSelected == name;

    /// <summary>
    /// เลือกเมนูใหม่ได้ทันที:
    /// - ถ้าเลือกอันเดิมซ้ำ => ยกเลิก (กลับเป็น "")
    /// - ถ้าเลือกอันอื่นขณะมีอันเดิมอยู่ => ยกเลิกอันเดิม แล้วเปลี่ยนเป็นอันใหม่ทันที
    /// </summary>
    public void Select(string name)
    {
        name = (name ?? "").Trim();

        // ❗ ของเดิมคุณ return เมื่อชื่อว่าง เราคงพฤติกรรมเดิมไว้
        if (string.IsNullOrEmpty(name))
            return;

        // คลิกอันเดิมซ้ำ = toggle off
        if (currentSelected == name)
        {
            currentSelected = "";
            SyncSelectedName();

            if (logGate) Debug.Log("[Gate] Selected cleared (toggle off)");
            return;
        }

        // เลือกอันใหม่ทับอันเก่า
        string prev = currentSelected;
        currentSelected = name;
        SyncSelectedName();

        if (logGate)
        {
            if (!string.IsNullOrEmpty(prev))
                Debug.Log($"[Gate] Switched: {prev} -> {currentSelected}");
            else
                Debug.Log($"[Gate] Selected = {currentSelected}");
        }
    }

    /// <summary>
    /// ใช้ล้างแบบระบุชื่อ (เผื่อบางระบบเรียก)
    /// </summary>
    public void Clear(string name)
    {
        name = (name ?? "").Trim();

        if (currentSelected == name)
        {
            currentSelected = "";
            SyncSelectedName();

            if (logGate) Debug.Log("[Gate] Selected cleared");
        }
    }

    /// <summary>
    /// ล้างทันที ไม่สนชื่อ
    /// </summary>
    public void ClearAll()
    {
        currentSelected = "";
        SyncSelectedName();

        if (logGate) Debug.Log("[Gate] Cleared all");
    }

    // --------------------
    // Helper
    // --------------------
    void SyncSelectedName()
    {
        // ✅ ให้ SelectedName ตรงกับ currentSelected เสมอ
        SelectedName = currentSelected;
    }
}
