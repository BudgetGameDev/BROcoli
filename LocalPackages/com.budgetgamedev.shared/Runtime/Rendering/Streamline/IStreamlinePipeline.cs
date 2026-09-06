using System;
using UnityEngine;

namespace BudgetGameDev.Shared.Rendering
{
    internal interface IStreamlinePipeline : IDisposable
    {
        bool IsActive { get; }
        bool CanCapture { get; }
        bool ResolutionConfiguredBeforeUpscaler { get; }
        bool SupportsCamera(Camera camera);
        Vector2 GetJitter(Camera camera, Vector2 requested);
        void Attach(GameObject host);
        void Configure(bool superResolution);
        void ConfigureCamera(Camera camera, bool superResolution);
    }
}
