using UnityEngine;

/// <summary>
/// Autoplay helper: auto-selects an upgrade whenever the <see cref="LevelUpScreen"/>
/// appears, so an unattended run never stalls at the paused level-up menu.
/// Runs in Update (which still ticks at <c>Time.timeScale == 0</c>) and uses
/// unscaled time for its debounce.
/// </summary>
public class LevelUpAutoResolver : MonoBehaviour
{
    private LevelUpScreen _screen;
    private float _cooldown;

    private void Update()
    {
        if (_cooldown > 0f)
        {
            _cooldown -= Time.unscaledDeltaTime;
        }

        if (_screen == null)
        {
            _screen = FindAnyObjectByType<LevelUpScreen>();
            if (_screen == null) return;
        }

        if (_cooldown <= 0f && _screen.IsShowing())
        {
            int choice = Random.Range(0, 3);
            _screen.AutoSelectUpgrade(choice);
            Debug.Log($"[Autoplay] Auto-selected level-up choice {choice}.");
            _cooldown = 0.25f;
        }
    }
}
