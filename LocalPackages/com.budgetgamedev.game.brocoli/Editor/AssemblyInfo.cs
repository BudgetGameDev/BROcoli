using System.Runtime.CompilerServices;

// The autoplay runner assembles the player's arguments and reads its summary back,
// and both are worth testing without widening them into public API.
[assembly: InternalsVisibleTo("BudgetGameDev.Games.Brocoli.Tests")]
