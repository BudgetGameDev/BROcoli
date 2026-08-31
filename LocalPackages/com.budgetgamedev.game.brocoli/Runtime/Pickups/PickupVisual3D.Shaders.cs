using System;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class PickupVisual3D
    {
        internal static Shader FindPickupShader(Func<string, Shader> find)
        {
            Shader shader = find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = find("Sprites/Default");
            return shader;
        }
    }
}
