using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using StreamlineDLSS;

/// <summary>
/// Legacy Execute path for DLSSUpscalePass (non-Render Graph).
/// Separated for code organization and 300 LOC limit.
/// </summary>
public static class DLSSUpscalePassLegacy
{
    [Obsolete("Use Render Graph path instead")]
    public static void Execute(
        ScriptableRenderPass pass,
        ScriptableRenderContext context,
        ref RenderingData renderingData,
        DLSSRenderFeature.DLSSSettings settings,
        ref RenderTexture dlssOutputRT,
        ref Matrix4x4 prevViewProjection,
        ref bool firstFrame,
        ref int frameCount)
    {
        frameCount++;
        var camera = renderingData.cameraData.camera;
        
        if (settings.debugLogging && (frameCount == 1 || frameCount % 300 == 0))
        {
            Debug.Log($"[DLSS] Execute frame {frameCount}, camera: {camera.name}, " +
                      $"screen: {Screen.width}x{Screen.height}, camPx: {camera.pixelWidth}x{camera.pixelHeight}");
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Early-out if DLSS is not supported - prevents crash on GetOptimalSettings
        if (!StreamlineDLSSPlugin.IsDLSSSupported())
        {
            if (settings.debugLogging && frameCount == 1)
                Debug.Log("[DLSS] DLSS not supported, skipping legacy pass");
            return;
        }
        
        var cmd = CommandBufferPool.Get("DLSS Upscale");
        
        // Create our own profiling sampler since the pass's is protected
        var dlssProfilingSampler = new ProfilingSampler("DLSS Upscale Legacy");
        using (new ProfilingScope(cmd, dlssProfilingSampler))
        {
            int outputWidth = Screen.width;
            int outputHeight = Screen.height;
            
            // Get render resolution from DLSS optimal settings
            int renderWidth, renderHeight;
            if (StreamlineDLSSPlugin.GetOptimalSettings(
                settings.dlssMode, 
                (uint)outputWidth, 
                (uint)outputHeight, 
                out DLSSSettings dlssSettings))
            {
                renderWidth = (int)dlssSettings.OptimalRenderWidth;
                renderHeight = (int)dlssSettings.OptimalRenderHeight;
                
                if (settings.debugLogging && frameCount == 1)
                {
                    Debug.Log($"[DLSS] Optimal settings: render {renderWidth}x{renderHeight}, " +
                              $"sharpness={dlssSettings.OptimalSharpness}");
                }
            }
            else
            {
                renderWidth = camera.pixelWidth;
                renderHeight = camera.pixelHeight;
                
                if (settings.debugLogging && frameCount == 1)
                    Debug.Log($"[DLSS] GetOptimalSettings failed, using fallback: {renderWidth}x{renderHeight}");
            }
            
            // Ensure output RT exists
            dlssOutputRT = DLSSOutputManager.EnsureOutputRT(dlssOutputRT, outputWidth, outputHeight,
                settings.colorBuffersHDR, settings.debugLogging);
            
            // 1. Set viewport
            StreamlineDLSSPlugin.SetViewport(0);
            
            // 2. Set camera constants
            SetConstants(camera, prevViewProjection, firstFrame, settings.debugLogging);
            
            // 3. Set DLSS options
            bool optionsSet = StreamlineDLSSPlugin.SetOptions(
                settings.dlssMode,
                (uint)outputWidth,
                (uint)outputHeight,
                settings.colorBuffersHDR
            );
            
            if (settings.debugLogging && frameCount == 1)
                Debug.Log($"[DLSS] SetOptions: mode={settings.dlssMode}, output={outputWidth}x{outputHeight}, result={optionsSet}");
            
            // 4. Tag all required resources
            bool resourcesTagged = DLSSResourceTagger.TagResourcesLegacy(
                renderingData.cameraData, dlssOutputRT,
                renderWidth, renderHeight, outputWidth, outputHeight,
                settings.colorBuffersHDR, settings.debugLogging, firstFrame);
            
            if (settings.debugLogging && frameCount == 1)
                Debug.Log($"[DLSS] TagResources result: {resourcesTagged}");
            
            // 5. Execute command buffer to ensure resources are ready
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 6. Issue DLSS evaluate via plugin event
            if (settings.debugLogging && frameCount == 1)
                Debug.Log("[DLSS] About to call IssueEvaluateEvent...");
            
            StreamlineDLSSPlugin.IssueEvaluateEvent(cmd);
            
            if (settings.debugLogging && frameCount == 1)
            {
                Debug.Log($"[DLSS] IssueEvaluateEvent called " +
                          $"(Render: {renderWidth}x{renderHeight} → Output: {outputWidth}x{outputHeight})");
            }
            
            // 7. Execute the plugin event
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 8. Blit DLSS output to camera target
            if (dlssOutputRT != null)
            {
                cmd.Blit(dlssOutputRT, BuiltinRenderTextureType.CameraTarget);
                
                if (settings.debugLogging && frameCount == 1)
                    Debug.Log($"[DLSS] Blitting output RT to camera target");
            }
            
            // Store for next frame
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            prevViewProjection = projMatrix * viewMatrix;
            firstFrame = false;
        }
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
#endif
    }
    
    private static void SetConstants(Camera camera, Matrix4x4 prevViewProjection, bool firstFrame, bool debugLogging)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        Matrix4x4 viewProj = projMatrix * viewMatrix;
        Matrix4x4 invViewProj = viewProj.inverse;
        
        Matrix4x4 clipToPrevClip = firstFrame ? Matrix4x4.identity : prevViewProjection * invViewProj;
        Matrix4x4 prevClipToClip = clipToPrevClip.inverse;
        
        Vector2 jitterOffset = Vector2.zero;
        Vector2 mvecScale = new Vector2(camera.pixelWidth, camera.pixelHeight);
        
        bool success = StreamlineDLSSPlugin.SetConstants(
            projMatrix,
            projMatrix.inverse,
            clipToPrevClip,
            prevClipToClip,
            jitterOffset,
            mvecScale,
            camera.nearClipPlane,
            camera.farClipPlane,
            camera.fieldOfView * Mathf.Deg2Rad,
            camera.aspect,
            depthInverted: SystemInfo.usesReversedZBuffer,
            cameraMotionIncluded: true,
            reset: firstFrame
        );
        
        if (debugLogging && !success)
            Debug.LogWarning("[DLSS] SetConstants failed");
#endif
    }
}
