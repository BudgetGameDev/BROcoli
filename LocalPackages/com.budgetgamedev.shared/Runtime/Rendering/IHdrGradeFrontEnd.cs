using System;
using UnityEngine;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// One render pipeline's way of realizing an <see cref="HdrGradeRequest"/>. Exactly one
    /// implementation is linked into a build, and it registers itself with
    /// <see cref="RenderPipelineFrontEnd"/> when its pipeline turns out to be the active one.
    /// </summary>
    public interface IHdrGradeFrontEnd
    {
        /// <summary>The pipeline this front end grades for.</summary>
        RenderPipelineKind Pipeline { get; }

        /// <summary>
        /// Builds the grade's volume on <paramref name="host"/>. Called once, before the first
        /// <see cref="Apply"/>.
        /// </summary>
        void Attach(GameObject host);

        /// <summary>Pushes <paramref name="request"/> onto the volume built by <see cref="Attach"/>.</summary>
        void Apply(in HdrGradeRequest request);

        /// <summary>
        /// Tears the grade's volume down. Destruction is injected because the driver is torn
        /// down from both play mode and edit mode, which need different destroy calls.
        /// </summary>
        void Detach(
            bool isPlaying,
            Action<UnityEngine.Object> destroyDeferred,
            Action<UnityEngine.Object> destroyImmediate
        );
    }
}
