using System.Runtime.CompilerServices;

// The autoplay harness lives in the editor assembly but drives the runtime's own
// configuration, so it reads the tier catalogue rather than restating it.
[assembly: InternalsVisibleTo("BudgetGameDev.Games.Brocoli.Editor")]
[assembly: InternalsVisibleTo("BudgetGameDev.Games.Brocoli.Tests")]

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
[assembly: InternalsVisibleTo("BudgetGameDev.Autoplay.Brocoli")]
[assembly: InternalsVisibleTo("BudgetGameDev.Autoplay.Brocoli.Editor")]
#endif
