using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BudgetGameDev.Autoplay.Brocoli.Editor")]
[assembly: InternalsVisibleTo("BudgetGameDev.Games.Brocoli.Tests")]

// The adapter is injected at runtime; no production assembly references it.
[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]
