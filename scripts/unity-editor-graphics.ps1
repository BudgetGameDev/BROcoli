# Graphics API selection shared by the Windows unity-open entry points.

function Test-UnityDirect3D12Available {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        return $false
    }

    try {
        if (-not ("UnityOpen.Direct3D12Probe" -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace UnityOpen
{
    public static class Direct3D12Probe
    {
        [DllImport("d3d12.dll", ExactSpelling = true)]
        private static extern int D3D12CreateDevice(
            IntPtr adapter, uint minimumFeatureLevel, ref Guid deviceId, IntPtr device);

        public static bool IsAvailable()
        {
            var deviceId = new Guid("189819f1-1db6-4b57-be54-1821339b85f7");
            // Feature level 11_0. A null output tests support without creating a device.
            // The successful probe returns S_FALSE (1), not S_OK (0).
            return D3D12CreateDevice(IntPtr.Zero, 0xb000, ref deviceId, IntPtr.Zero) >= 0;
        }
    }
}
'@
        }
        return [UnityOpen.Direct3D12Probe]::IsAvailable()
    } catch {
        # Missing runtime, unsupported drivers, or a blocked probe must not prevent launch.
        Write-Verbose "DX12 support could not be established: $_"
        return $false
    }
}

function Get-UnityEditorArgumentString {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [ValidateSet("Auto", "Default", "Direct3D11", "Direct3D12")]
        [string]$GraphicsApi = "Auto"
    )

    switch ($GraphicsApi) {
        "Direct3D11" { return "-automated -force-d3d11" }
        "Direct3D12" { return "-automated -force-d3d12" }
        "Auto" {
            if (Test-UnityDirect3D12Available) {
                return "-automated -force-d3d12"
            }
        }
    }
    return "-automated"
}
