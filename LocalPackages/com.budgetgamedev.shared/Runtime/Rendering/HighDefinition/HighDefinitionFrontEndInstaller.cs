using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    /// <summary>
    /// Hands this assembly's front ends to <see cref="RenderPipelineFrontEnd"/> as the runtime
    /// starts.
    ///
    /// Nothing in the game references this assembly by name -- that is what keeps the pipeline
    /// out of gameplay code -- so a player build would otherwise strip it, and a stripped
    /// assembly cannot be found by looking through the loaded ones. The assembly-level
    /// <c>AlwaysLinkAssembly</c> above is what keeps it in the player, and this is what makes it
    /// speak up once it is there.
    ///
    /// Registering costs nothing on the pipeline that is not rendering: the front end is a
    /// handful of fields until something asks it to attach.
    /// </summary>
    public static class HighDefinitionFrontEndInstaller
    {
        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            RenderPipelineFrontEnd.Register(new HighDefinitionHdrGradeFrontEnd());
            RenderPipelineFrontEnd.Register(new HighDefinitionLightingFrontEnd());
        }
    }
}
