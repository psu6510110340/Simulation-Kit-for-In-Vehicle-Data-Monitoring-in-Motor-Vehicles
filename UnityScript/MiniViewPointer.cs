using UnityEngine;
using UnityEngine.EventSystems;

public class MiniViewPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool IsPointerOver { get; private set; }

    public void OnPointerEnter(PointerEventData eventData) => IsPointerOver = true;
    public void OnPointerExit(PointerEventData eventData) => IsPointerOver = false;
}
