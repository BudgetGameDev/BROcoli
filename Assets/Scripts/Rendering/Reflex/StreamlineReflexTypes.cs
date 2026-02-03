using System.Runtime.InteropServices;

namespace StreamlineReflex
{
    /// <summary>
    /// Reflex latency mode settings
    /// </summary>
    public enum ReflexMode
    {
        Off = 0,
        LowLatency = 1,           // Low Latency Mode
        LowLatencyWithBoost = 2   // Low Latency + Boost (increases GPU clocks when CPU-bound)
    }
    
    /// <summary>
    /// PCL Markers for latency measurement
    /// </summary>
    public enum PCLMarker
    {
        SimulationStart = 0,
        SimulationEnd = 1,
        RenderSubmitStart = 2,
        RenderSubmitEnd = 3,
        PresentStart = 4,
        PresentEnd = 5,
        TriggerFlash = 7,
        PCLatencyPing = 8
    }
    
    /// <summary>
    /// Latency statistics from Reflex
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LatencyStats
    {
        public float SimulationMs;
        public float RenderSubmitMs;
        public float PresentMs;
        public float DriverMs;
        public float OsRenderQueueMs;
        public float GpuRenderMs;
        public float TotalLatencyMs;
    }
}
