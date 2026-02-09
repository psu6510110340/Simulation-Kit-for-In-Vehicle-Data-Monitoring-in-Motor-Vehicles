using UnityEngine;

public class ItemClickDebug : MonoBehaviour
{
    public string itemName;

    public void OnItemClicked()
    {
        Debug.Log($"[SidePanel] Clicked: {itemName}");
    }
}
