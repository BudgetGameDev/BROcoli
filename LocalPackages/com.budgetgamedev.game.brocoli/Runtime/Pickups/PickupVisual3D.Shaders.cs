using System;
using BudgetGameDev.Games.Brocoli.Rendering;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class PickupVisual3D
    {
        /// <summary>
        /// The lit shader pickups are built from. It is BROcoli's own dual-target surface
        /// graph, so the same pickup renders through whichever pipeline the build ships.
        /// <c>Sprites/Default</c> is the last resort: it is an engine builtin that resolves
        /// under both pipelines, so a pickup still appears rather than rendering as magenta.
        /// </summary>
        internal static Shader FindPickupShader(
            Func<string, Shader> load,
            Func<string, Shader> find
        )
        {
            Shader shader = BrocoliShaders.ResolveUncached(BrocoliShaders.Surface, load, find);
            return shader != null ? shader : find("Sprites/Default");
        }
    }
}
