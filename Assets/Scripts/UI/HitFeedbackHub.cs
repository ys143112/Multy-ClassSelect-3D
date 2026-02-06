using System.Collections;
using UnityEngine;

public class HitFeedbackHub : MonoBehaviour
{
    public static HitFeedbackHub Instance { get; private set; }

    [Header("Refs")]
    public Camera targetCamera;                 // 비우면 Camera.main
    public RectTransform crosshairRect;         // 크로스헤어 Image의 RectTransform (있으면 튕김)

    [Header("HitStop")]
    public float hitStopTimeScale = 0.05f;      // 0.05~0.2 추천
    public float hitStopDuration = 0.05f;       // 초(언스케일드)

    [Header("Camera Shake")]
    public float shakeDuration = 0.08f;         // 초(언스케일드)
    public float shakeStrength = 0.06f;         // 카메라 흔들림 강도
    public float shakeFrequency = 45f;          // 흔들림 속도

    [Header("Crosshair Punch")]
    public float punchDuration = 0.08f;         // 초(언스케일드)
    public float punchScale = 1.18f;            // 1.1~1.25 추천

    Vector3 camBaseLocalPos;
    Coroutine shakeCo;
    Coroutine punchCo;

    // 히트스톱 중복 방지
    float stopUntilUnscaled;
    float prevTimeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (targetCamera == null)
            targetCamera = Camera.main;

        CacheCameraBasePos();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void CacheCameraBasePos()
    {
        if (targetCamera != null)
            camBaseLocalPos = targetCamera.transform.localPosition;
    }

    public void PlayHitFeedback(float intensity01 = 1f)
    {
        // 카메라가 씬 전환 등으로 바뀔 수 있으니 매번 보정
        if (targetCamera == null) targetCamera = Camera.main;
        CacheCameraBasePos();

        // HitStop (로컬 클라 전용, 멀티에서도 안전)
        TriggerHitStop();

        // Camera Shake
        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(CoShake(Mathf.Clamp01(intensity01)));

        // Crosshair punch
        if (crosshairRect != null)
        {
            if (punchCo != null) StopCoroutine(punchCo);
            punchCo = StartCoroutine(CoPunch());
        }
    }

    void TriggerHitStop()
    {
        // 여러 번 맞아도 stopUntil만 연장
        stopUntilUnscaled = Mathf.Max(stopUntilUnscaled, Time.unscaledTime + hitStopDuration);

        // 이미 히트스톱 상태면 리턴
        if (Time.timeScale <= hitStopTimeScale + 0.0001f) return;

        prevTimeScale = Time.timeScale;
        Time.timeScale = hitStopTimeScale;

        StartCoroutine(CoRestoreTimeScale());
    }

    IEnumerator CoRestoreTimeScale()
    {
        // stopUntil이 갱신될 수 있으니 계속 대기
        while (Time.unscaledTime < stopUntilUnscaled)
            yield return null;

        Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;
    }

    IEnumerator CoShake(float intensity01)
    {
        if (targetCamera == null) yield break;

        float dur = shakeDuration;
        float strength = shakeStrength * Mathf.Lerp(0.6f, 1.4f, intensity01);

        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < dur)
        {
            float t = (Time.unscaledTime - start) * shakeFrequency;
            float x = (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.1f, t) - 0.5f) * 2f;

            targetCamera.transform.localPosition = camBaseLocalPos + new Vector3(x, y, 0f) * strength;
            yield return null;
        }

        targetCamera.transform.localPosition = camBaseLocalPos;
    }

    IEnumerator CoPunch()
    {
        Vector2 baseSize = crosshairRect.sizeDelta;
        Vector2 targetSize = baseSize * punchScale;

        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < punchDuration)
        {
            float t = (Time.unscaledTime - start) / punchDuration;
            // 빠르게 커졌다가 돌아오는 느낌
            float k = 1f - Mathf.Pow(1f - t, 3f);

            crosshairRect.sizeDelta = Vector2.Lerp(targetSize, baseSize, k);
            yield return null;
        }

        crosshairRect.sizeDelta = baseSize;
    }
}
