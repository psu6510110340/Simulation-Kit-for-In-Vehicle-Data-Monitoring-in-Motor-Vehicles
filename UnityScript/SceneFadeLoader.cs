using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneFadeManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string menuSceneName = "Menu";
    public string defaultTargetScene = "Project";

    [Header("Fade Settings")]
    public float fadeOutDuration = 0.8f;
    public float fadeInDuration = 0.8f;

    [Header("Input")]
    public KeyCode nextSceneKey = KeyCode.Space; // ใน Menu: ไป Project
    public KeyCode backToMenuKey = KeyCode.Escape; // ทุกซีน: กลับ Menu

    float _alpha = 1f;           // เริ่ม 1 เพื่อทำ Fade In ตอนเริ่มซีน
    bool _isTransitioning = false;

    void Awake()
    {
        // ✅ ทำให้สคริปนี้อยู่ข้ามซีนได้ โดยไม่ต้องสร้าง Object เพิ่ม
        DontDestroyOnLoad(gameObject);

        // กันซ้ำถ้ามีหลายตัวติดมาจากหลายซีน
        var all = FindObjectsByType<SceneFadeManager>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        // เริ่มด้วย Fade In
        StartCoroutine(FadeIn());
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ทุกครั้งที่โหลดซีนใหม่ ให้ Fade In
        StartCoroutine(FadeIn());
    }

    void Update()
    {
        if (_isTransitioning) return;

        // ESC: กลับ Menu จากซีนใด ๆ (ถ้าไม่ใช่ Menu)
        if (Input.GetKeyDown(backToMenuKey))
        {
            if (SceneManager.GetActiveScene().name != menuSceneName)
                StartCoroutine(FadeOutAndLoad(menuSceneName));
            return;
        }

        // Space: เฉพาะตอนอยู่ใน Menu ให้ไป Project (หรือ defaultTargetScene)
        if (Input.GetKeyDown(nextSceneKey))
        {
            if (SceneManager.GetActiveScene().name == menuSceneName)
                StartCoroutine(FadeOutAndLoad(defaultTargetScene));
        }
    }

    public void LoadSceneWithFade(string sceneName)
    {
        if (_isTransitioning) return;
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    IEnumerator FadeOutAndLoad(string sceneName)
    {
        _isTransitioning = true;

        // Fade Out (0 -> 1)
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            _alpha = Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        SceneManager.LoadScene(sceneName);

        // Fade In จะถูกเรียกใน OnSceneLoaded
        _isTransitioning = false;
    }

    IEnumerator FadeIn()
    {
        // Fade In (1 -> 0)
        float t = 0f;
        _alpha = 1f;

        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            _alpha = 1f - Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }

        _alpha = 0f;
    }

    void OnGUI()
    {
        if (_alpha <= 0f) return;

        GUI.color = new Color(0, 0, 0, _alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
