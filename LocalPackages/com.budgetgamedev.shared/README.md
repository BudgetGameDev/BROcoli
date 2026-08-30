# Budget GameDev Shared

Runtime services every Budget GameDev title reuses, with no knowledge of any
particular game.

- **Input** — unified keyboard/gamepad/touch handling and an on-screen virtual
  controller, including the generated `TouchAction` action map.
- **Pooling** — a generic `ObjectPool<T>` for reuse instead of Instantiate/Destroy.
- **Platform** — PWA install/fullscreen helpers, iOS Safari WebGL tuning, and
  landscape/focus handling, with their WebGL `.jslib` bridges.
- **UI** — canvas bootstrap, menu theming, procedural UI audio, bars and a
  loading screen.
- **Audio** — mixer-backed master/ambience/SFX volume settings.

## Per-game configuration

This package names no game. Anything game-specific is injected:

- `GameAudioSettings.Configure(mixerResourcePath, menuSceneName)` points the
  shared mixer code at the running game's assets. The hub calls this.
- `IPauseController` is how the pause button and WebGL focus-loss handling reach
  a game's pause screen. Games implement it; `PauseControllerLocator.Find()`
  resolves it without a compile-time reference.

## Dependencies

Declared in `package.json` and resolved by the Package Manager: Input System,
URP, uGUI, and the audio/UI modules.
