using System.Collections;
using UnityEngine;

/// <summary>
/// Stops the active player visuals and tips only the broccoli model onto the
/// ground, leaving it at full size for the results transition.
/// </summary>
public sealed class PlayerDeathVisual : MonoBehaviour
{
    private Transform fallPivot;
    private Quaternion standingRotation;
    private Vector3 standingPosition;

    public void Prepare()
    {
        ShuffleWalkVisual hop = GetComponentInChildren<ShuffleWalkVisual>(true);
        Face2DMovementDirection facing = GetComponentInChildren<Face2DMovementDirection>(true);
        PitchFromInputRelativeToDownPose pitch =
            GetComponentInChildren<PitchFromInputRelativeToDownPose>(true);

        if (hop != null)
            hop.enabled = false;
        if (facing != null)
            facing.enabled = false;
        if (pitch != null)
        {
            pitch.enabled = false;
            fallPivot = pitch.transform;
        }
        else
        {
            fallPivot = transform;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;

        SanitizerSpray spray = GetComponentInChildren<SanitizerSpray>(true);
        if (spray != null)
            spray.enabled = false;
        ProceduralSprayAudio sprayAudio = GetComponentInChildren<ProceduralSprayAudio>(true);
        sprayAudio?.StopSpray();

        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (SprayWeaponVisual3D weapon in GetComponentsInChildren<SprayWeaponVisual3D>(true))
            weapon.SetVisible(false);

        standingRotation = fallPivot.localRotation;
        standingPosition = fallPivot.localPosition;
    }

    public IEnumerator FallAndSettle(float fallDuration, float settleDuration)
    {
        if (fallPivot == null)
            Prepare();

        float duration = Mathf.Max(0.2f, fallDuration);
        Quaternion lyingRotation = standingRotation * Quaternion.Euler(-90f, 0f, 0f);
        Vector3 lyingPosition = standingPosition + Vector3.down * 0.06f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            fallPivot.localRotation = Quaternion.Slerp(standingRotation, lyingRotation, eased);
            fallPivot.localPosition = Vector3.Lerp(standingPosition, lyingPosition, eased);
            yield return null;
        }

        fallPivot.localRotation = lyingRotation;
        fallPivot.localPosition = lyingPosition;

        if (settleDuration > 0f)
            yield return new WaitForSecondsRealtime(settleDuration);
    }
}
