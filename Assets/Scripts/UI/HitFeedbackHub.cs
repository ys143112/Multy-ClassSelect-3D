using UnityEngine;
using System.Collections;

public class HitFeedbackHub : MonoBehaviour
{
    public static HitFeedbackHub Instance { get; private set; }

    [Header("Camera Shake")]
    public Camera cam;
    public float shakeStrength = 0.05f;
    public float shakeTime = 0.08f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (cam == null)
            cam = Camera.main;
    }

    // 🔥 맞았을 때 호출
    public void PlayGotHit(float intensity01, Vector3 hitWorldPos)
    {
        if (cam == null) return;

        StartCoroutine(CoShake(intensity01));
    }

    IEnumerator CoShake(float intensity01)
    {
        Vector3 basePos = cam.transform.localPosition;

        float elapsed = 0f;
        float strength = shakeStrength * Mathf.Lerp(0.6f, 1.4f, intensity01);

        while (elapsed < shakeTime)
        {
            cam.transform.localPosition =
                basePos + Random.insideUnitSphere * strength;

            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.transform.localPosition = basePos;
    }
}
