using UnityEngine;

/// <summary>
/// Trauma-based camera shake system using Perlin noise.
/// Applies shake as an ADDITIVE offset so it works with camera follow scripts.
/// </summary>
public class CameraShake : MonoBehaviour
{
    private const float MaxShakeOffset = 0.4f;
    private const float MaxShakeRotation = 2f;
    private const float ShakeFrequency = 25f;
    private const float TraumaDecay = 3f;

    private float _trauma;
    private float _seed;
    private Vector3 _currentShakeOffset;
    private float _currentShakeRotation;

    private static CameraShake _instance;
    public static CameraShake Instance => _instance;

    private void Awake()
    {
        _instance = this;
        _seed = Random.value * 1000f;
        _trauma = 0f;
        _currentShakeOffset = Vector3.zero;
        _currentShakeRotation = 0f;
    }

    private void LateUpdate()
    {
        // Only process if there's trauma
        if (_trauma <= 0f && _currentShakeOffset.sqrMagnitude < 0.0001f)
            return;

        // Decay trauma
        _trauma = Mathf.Max(0f, _trauma - TraumaDecay * Time.deltaTime);

        if (_trauma > 0.001f)
        {
            float shake = _trauma * _trauma;
            float time = Time.time * ShakeFrequency;

            float offsetX = (Mathf.PerlinNoise(_seed, time) - 0.5f) * 2f * MaxShakeOffset * shake;
            float offsetY = (Mathf.PerlinNoise(_seed + 100f, time) - 0.5f) * 2f * MaxShakeOffset * shake;
            float rotation = (Mathf.PerlinNoise(_seed + 200f, time) - 0.5f) * 2f * MaxShakeRotation * shake;

            _currentShakeOffset = new Vector3(offsetX, offsetY, 0f);
            _currentShakeRotation = rotation;
        }
        else
        {
            _currentShakeOffset = Vector3.Lerp(_currentShakeOffset, Vector3.zero, 15f * Time.deltaTime);
            _currentShakeRotation = Mathf.Lerp(_currentShakeRotation, 0f, 15f * Time.deltaTime);
        }

        // Apply additive offset
        transform.localPosition += _currentShakeOffset;
        transform.localRotation *= Quaternion.Euler(0f, 0f, _currentShakeRotation);
    }

    public void TriggerShake(float intensity)
    {
        _trauma = Mathf.Min(1f, _trauma + Mathf.Clamp01(intensity));
    }

    public static void Shake(float intensity)
    {
        _instance?.TriggerShake(intensity);
    }
}
