using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarGhostCollision : MonoBehaviour
{
    [Header("Settings")]
    public string carTag = "Car";
    public float ghostDuration = 0.8f;       // อย่างน้อยทะลุกันกี่วินาที
    public float reenableDistance = 2.0f;    // ห่างกันเท่านี้ค่อยเปิดชนกลับ
    public bool log = false;

    Collider[] _myCols;
    Transform _myRoot;

    // กันชนซ้ำซ้อนเป็นคู่ ๆ
    static HashSet<(int, int)> _activePairs = new HashSet<(int, int)>();

    void Awake()
    {
        _myRoot = transform.root;
        _myCols = _myRoot.GetComponentsInChildren<Collider>(true);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider) return;

        Transform otherRoot = collision.collider.transform.root;
        if (otherRoot == _myRoot) return;
        if (!otherRoot.CompareTag(carTag)) return;

        int a = _myRoot.GetInstanceID();
        int b = otherRoot.GetInstanceID();
        var key = a < b ? (a, b) : (b, a);

        if (_activePairs.Contains(key)) return;

        StartCoroutine(GhostPair(otherRoot, key));
    }

    IEnumerator GhostPair(Transform otherRoot, (int, int) key)
    {
        _activePairs.Add(key);

        var otherCols = otherRoot.GetComponentsInChildren<Collider>(true);

        // ignore ทุก collider ของ 2 คัน
        SetIgnoreBetween(_myCols, otherCols, true);
        if (log) Debug.Log($"[Ghost] Ignore ON: {_myRoot.name} <-> {otherRoot.name}");

        float t = 0f;

        // บังคับทะลุขั้นต่ำก่อน
        while (t < ghostDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // แล้วรอจนห่างกันพอ (กันเปิดเร็วเกินไป)
        while (Vector3.Distance(_myRoot.position, otherRoot.position) < reenableDistance)
        {
            yield return null;
        }

        SetIgnoreBetween(_myCols, otherCols, false);
        if (log) Debug.Log($"[Ghost] Ignore OFF: {_myRoot.name} <-> {otherRoot.name}");

        _activePairs.Remove(key);
    }

    void SetIgnoreBetween(Collider[] aCols, Collider[] bCols, bool ignore)
    {
        for (int i = 0; i < aCols.Length; i++)
        {
            var a = aCols[i];
            if (!a || a.isTrigger) continue;

            for (int j = 0; j < bCols.Length; j++)
            {
                var b = bCols[j];
                if (!b || b.isTrigger) continue;

                Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }
}
