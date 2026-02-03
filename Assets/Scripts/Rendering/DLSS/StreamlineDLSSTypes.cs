using System.Runtime.InteropServices;

namespace StreamlineDLSS
{
    /// <summary>
    /// DLSS Quality/Performance modes
    /// </summary>
    public enum DLSSMode
    {
        Off = 0,
        MaxPerformance = 1,    // ~50% render scale - best performance
        Balanced = 2,          // ~58% render scale - balanced
        MaxQuality = 3,        // ~67% render scale - best quality
        UltraPerformance = 4,  // ~33% render scale - extreme performance (may reduce quality)
        UltraQuality = 5,      // ~77% render scale - highest quality
        DLAA = 6               // Native resolution AA - no upscaling, just anti-aliasing
    }
    
    /// <summary>
    /// Frame Generation modes
    /// </summary>
    public enum DLSSGMode
    {
        Off = 0,
        On = 1,
        Auto = 2   // Automatically enables based on GPU capability
    }
    
    /// <summary>
    /// Buffer types for DLSS resource tagging
    /// </summary>
    public enum BufferType : uint
    {
        Depth = 0,
        MotionVectors = 1,
        HUDLessColor = 2,
        ScalingInputColor = 3,
        ScalingOutputColor = 4,
        UIColorAndAlpha = 23
    }
    
    /// <summary>
    /// DLSS optimal render settings for a given output resolution and mode
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DLSSSettings
    {
        public uint OptimalRenderWidth;
        public uint OptimalRenderHeight;
        public uint MinRenderWidth;
        public uint MinRenderHeight;
        public uint MaxRenderWidth;
        public uint MaxRenderHeight;
        public float OptimalSharpness;
    }
    
    /// <summary>
    /// Frame Generation state information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DLSSGState
    {
        public ulong EstimatedVRAMUsage;
        public uint Status;
        public uint MinWidthOrHeight;
        public uint NumFramesActuallyPresented;
        public uint NumFramesToGenerateMax;
    }
    
    /// <summary>
    /// D3D12 resource state constants
    /// </summary>
    public static class D3D12ResourceStates
    {
        public const uint PixelShaderResource = 0x80;
        public const uint DepthRead = 0x20;
        public const uint RenderTarget = 0x4;
        public const uint UnorderedAccess = 0x8;
    }
    
    /// <summary>
    /// DXGI format constants
    /// </summary>
    public static class DXGIFormats
    {
        public const uint R16G16B16A16_FLOAT = 10;  // HDR
        public const uint R8G8B8A8_UNORM = 28;      // SDR
        public const uint R16G16_FLOAT = 34;        // Motion vectors
        public const uint D32_FLOAT = 40;           // Depth
    }
}
