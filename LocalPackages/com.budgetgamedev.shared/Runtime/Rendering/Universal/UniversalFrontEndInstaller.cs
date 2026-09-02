using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace BudgetGameDev.Shared.Rendering.Universal
{
    /// <summary>
    /// Hands this assembly's front ends to <see cref="RenderPipelineFrontEnd"/> as the runtime
    /// starts. Same reason as High Definition's: nothing references this assembly by name, so
    /// without the assembly-level <c>AlwaysLinkAssembly</c> and this call the web build would
    /// run with no grade and no lighting front end at all.
    /// </summary>
    public static class UniversalFrontEndInstaller
    {
        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            RenderPipelineFrontEnd.Register(new UniversalHdrGradeFrontEnd());
            RenderPipelineFrontEnd.Register(new UniversalLightingFrontEnd());
        }
    }
}
