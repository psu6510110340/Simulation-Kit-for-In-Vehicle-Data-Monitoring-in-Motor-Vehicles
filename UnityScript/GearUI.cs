using System.Collections;
using TMPro;
using UnityEngine;

public class GearUI : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text pText, rText, nText, dText, bText;

    [Header("Arrow (RectTransform)")]
    public RectTransform arrow;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color activeColor = Color.white;
    public Color blinkColor = Color.red;

    [Header("Blink")]
    public float blinkTime = 0.18f;
    public int blinkTimes = 2;
    Coroutine _blinkCo;

    [Header("Scale Levels")]
    public bool enableScale = true;
    public float selectedScale = 1.55f;    // เกียร์ที่เลือก (ใหญ่สุด)
    public float nearScale = 1.05f;        // เกียร์ติดบน/ล่าง (เล็กลง)
    public float farScale = 0.75f;         // เกียร์ที่เหลือ (เล็กสุด)
    public float scaleLerpSpeed = 14f;

    // target scale per slot (P,R,N,D,B)
    float tp, tr, tn, td, tb;

    void Awake()
    {
        tp = tr = tn = td = tb = farScale;
        ApplyScaleImmediate();
    }

    void Update()
    {
        if (!enableScale) return;
        LerpScale(pText, tp);
        LerpScale(rText, tr);
        LerpScale(nText, tn);
        LerpScale(dText, td);
        LerpScale(bText, tb);
    }

    void LerpScale(TMP_Text t, float targetS)
    {
        if (!t) return;
        float cur = t.rectTransform.localScale.x;
        float next = Mathf.Lerp(cur, targetS, scaleLerpSpeed * Time.unscaledDeltaTime);
        t.rectTransform.localScale = Vector3.one * next;
    }

    void ApplyScaleImmediate()
    {
        if (!enableScale) return;
        SetScale(pText, tp); SetScale(rText, tr); SetScale(nText, tn); SetScale(dText, td); SetScale(bText, tb);
    }

    void SetScale(TMP_Text t, float s)
    {
        if (!t) return;
        t.rectTransform.localScale = Vector3.one * s;
    }

    int GearIndex(char g) => g switch
    {
        'P' => 0,
        'R' => 1,
        'N' => 2,
        'D' => 3,
        'B' => 4,
        _ => 0
    };

    float ScaleByDistance(int dist)
    {
        if (dist == 0) return selectedScale;
        if (dist == 1) return nearScale;
        return farScale;
    }

    public void SetActive(char gearChar)
    {
        int sel = GearIndex(gearChar);

        // คำนวณ scale 3 ระดับตามระยะห่าง
        tp = ScaleByDistance(Mathf.Abs(0 - sel));
        tr = ScaleByDistance(Mathf.Abs(1 - sel));
        tn = ScaleByDistance(Mathf.Abs(2 - sel));
        td = ScaleByDistance(Mathf.Abs(3 - sel));
        tb = ScaleByDistance(Mathf.Abs(4 - sel));

        // สี/ตัวหนา
        SetStyle(pText, gearChar == 'P');
        SetStyle(rText, gearChar == 'R');
        SetStyle(nText, gearChar == 'N');
        SetStyle(dText, gearChar == 'D');
        SetStyle(bText, gearChar == 'B');

        UpdateArrow(gearChar);
    }

    void SetStyle(TMP_Text t, bool active)
    {
        if (!t) return;
        t.color = active ? activeColor : normalColor;
        t.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
    }

    void UpdateArrow(char gearChar)
    {
        if (!arrow) return;

        TMP_Text t = gearChar switch
        {
            'P' => pText,
            'R' => rText,
            'N' => nText,
            'D' => dText,
            'B' => bText,
            _ => null
        };
        if (!t) return;

        Vector3 localPos = arrow.parent.InverseTransformPoint(t.rectTransform.position);
        Vector3 a = arrow.localPosition;
        a.y = localPos.y;
        arrow.localPosition = a;
    }

    public void BlinkCurrent(char gearChar)
    {
        if (_blinkCo != null) StopCoroutine(_blinkCo);
        _blinkCo = StartCoroutine(CoBlink(gearChar));
    }

    IEnumerator CoBlink(char gearChar)
    {
        TMP_Text t = gearChar switch
        {
            'P' => pText,
            'R' => rText,
            'N' => nText,
            'D' => dText,
            'B' => bText,
            _ => null
        };
        if (!t) yield break;

        Color before = t.color;

        for (int i = 0; i < blinkTimes; i++)
        {
            t.color = blinkColor;
            yield return new WaitForSecondsRealtime(blinkTime);
            t.color = before;
            yield return new WaitForSecondsRealtime(blinkTime);
        }

        _blinkCo = null;
    }
}
