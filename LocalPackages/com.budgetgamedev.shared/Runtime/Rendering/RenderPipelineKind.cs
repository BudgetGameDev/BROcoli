namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// The render pipeline a build renders through. BROcoli ships two front ends over one
    /// game: Universal drives the web build, and High Definition drives the Windows build
    /// that adds ray tracing. Gameplay code never branches on this; only the rendering
    /// front ends and the assets they own do.
    /// </summary>
    public enum RenderPipelineKind
    {
        /// <summary>No scriptable pipeline is active, or the active one is unrecognized.</summary>
        Unknown = 0,

        /// <summary>Universal Render Pipeline: the web and low-end target.</summary>
        Universal = 1,

        /// <summary>High Definition Render Pipeline: the Windows high-end target.</summary>
        HighDefinition = 2,
    }
}
