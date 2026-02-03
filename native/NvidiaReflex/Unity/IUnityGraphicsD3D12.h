// Unity Native Plugin API copyright © 2015 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity-dependent projects--see
// Unity Companion License (http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made
// available strictly on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
// Please review the license for details on these and other terms and conditions.

#pragma once
#include "IUnityInterface.h"

#ifndef __cplusplus
#include <stdbool.h>
#endif

// Forward declarations for D3D12 types
struct ID3D12Device;
struct ID3D12CommandQueue;
struct ID3D12Fence;
struct ID3D12Resource;
struct ID3D12GraphicsCommandList;
struct IDXGISwapChain;

typedef struct UnityGraphicsD3D12ResourceState UnityGraphicsD3D12ResourceState;
struct UnityGraphicsD3D12ResourceState
{
    ID3D12Resource* resource;
    int expected; // D3D12_RESOURCE_STATES
    int current;
};

struct UnityGraphicsD3D12RecordingState
{
    ID3D12GraphicsCommandList* commandList;
};

enum UnityD3D12GraphicsQueueAccess
{
    kUnityD3D12GraphicsQueueAccess_DontCare,
    kUnityD3D12GraphicsQueueAccess_Allow,
};

enum UnityD3D12EventConfigFlagBits
{
    kUnityD3D12EventConfigFlag_EnsurePreviousFrameSubmission = (1 << 0),
    kUnityD3D12EventConfigFlag_FlushCommandBuffers = (1 << 1),
    kUnityD3D12EventConfigFlag_SyncWorkerThreads = (1 << 2),
    kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState = (1 << 3),
};

struct UnityD3D12PluginEventConfig
{
    UnityD3D12GraphicsQueueAccess graphicsQueueAccess;
    unsigned int flags;
    bool ensureActiveRenderTextureIsBound;
};

typedef struct UnityGraphicsD3D12PhysicalVideoMemoryControlValues UnityGraphicsD3D12PhysicalVideoMemoryControlValues;
struct UnityGraphicsD3D12PhysicalVideoMemoryControlValues
{
    unsigned long long reservation;
    unsigned long long systemMemoryThreshold;
    unsigned long long residencyHysteresisThreshold;
    float nonEvictableRelativeThreshold;
};

// Should only be used on the rendering/submission thread.
// v7 is the latest and preferred version for D3D12 integration
UNITY_DECLARE_INTERFACE(IUnityGraphicsD3D12v7)
{
    ID3D12Device* (UNITY_INTERFACE_API * GetDevice)();
    IDXGISwapChain* (UNITY_INTERFACE_API * GetSwapChain)();
    unsigned int(UNITY_INTERFACE_API * GetSyncInterval)();
    unsigned int(UNITY_INTERFACE_API * GetPresentFlags)();
    ID3D12Fence* (UNITY_INTERFACE_API * GetFrameFence)();
    unsigned long long(UNITY_INTERFACE_API * GetNextFrameFenceValue)();
    unsigned long long(UNITY_INTERFACE_API * ExecuteCommandList)(ID3D12GraphicsCommandList* commandList, int stateCount, UnityGraphicsD3D12ResourceState* states);
    void(UNITY_INTERFACE_API * SetPhysicalVideoMemoryControlValues)(const UnityGraphicsD3D12PhysicalVideoMemoryControlValues* memInfo);
    ID3D12CommandQueue* (UNITY_INTERFACE_API * GetCommandQueue)();
    ID3D12Resource* (UNITY_INTERFACE_API * TextureFromRenderBuffer)(UnityRenderBuffer rb);
    ID3D12Resource* (UNITY_INTERFACE_API * TextureFromNativeTexture)(UnityTextureID texture);
    void(UNITY_INTERFACE_API * ConfigureEvent)(int eventID, const UnityD3D12PluginEventConfig* pluginEventConfig);
    bool(UNITY_INTERFACE_API * CommandRecordingState)(UnityGraphicsD3D12RecordingState* outCommandRecordingState);
};
UNITY_REGISTER_INTERFACE_GUID(0x4624B0DA41B64AACULL, 0x915AABCB9BC3F0D3ULL, IUnityGraphicsD3D12v7)

// v6 interface
UNITY_DECLARE_INTERFACE(IUnityGraphicsD3D12v6)
{
    ID3D12Device* (UNITY_INTERFACE_API * GetDevice)();
    ID3D12Fence* (UNITY_INTERFACE_API * GetFrameFence)();
    unsigned long long(UNITY_INTERFACE_API * GetNextFrameFenceValue)();
    unsigned long long(UNITY_INTERFACE_API * ExecuteCommandList)(ID3D12GraphicsCommandList* commandList, int stateCount, UnityGraphicsD3D12ResourceState* states);
    void(UNITY_INTERFACE_API * SetPhysicalVideoMemoryControlValues)(const UnityGraphicsD3D12PhysicalVideoMemoryControlValues* memInfo);
    ID3D12CommandQueue* (UNITY_INTERFACE_API * GetCommandQueue)();
    ID3D12Resource* (UNITY_INTERFACE_API * TextureFromRenderBuffer)(UnityRenderBuffer rb);
    ID3D12Resource* (UNITY_INTERFACE_API * TextureFromNativeTexture)(UnityTextureID texture);
    void(UNITY_INTERFACE_API * ConfigureEvent)(int eventID, const UnityD3D12PluginEventConfig* pluginEventConfig);
    bool(UNITY_INTERFACE_API * CommandRecordingState)(UnityGraphicsD3D12RecordingState* outCommandRecordingState);
};
UNITY_REGISTER_INTERFACE_GUID(0xA396DCE58CAC4D78ULL, 0xAFDD9B281F20B840ULL, IUnityGraphicsD3D12v6)

// v5 interface
UNITY_DECLARE_INTERFACE(IUnityGraphicsD3D12v5)
{
    ID3D12Device* (UNITY_INTERFACE_API * GetDevice)();
    ID3D12Fence* (UNITY_INTERFACE_API * GetFrameFence)();
    unsigned long long(UNITY_INTERFACE_API * GetNextFrameFenceValue)();
    unsigned long long(UNITY_INTERFACE_API * ExecuteCommandList)(ID3D12GraphicsCommandList* commandList, int stateCount, UnityGraphicsD3D12ResourceState* states);
    void(UNITY_INTERFACE_API * SetPhysicalVideoMemoryControlValues)(const UnityGraphicsD3D12PhysicalVideoMemoryControlValues* memInfo);
    ID3D12CommandQueue* (UNITY_INTERFACE_API * GetCommandQueue)();
    ID3D12Resource* (UNITY_INTERFACE_API * TextureFromRenderBuffer)(UnityRenderBuffer rb);
};
UNITY_REGISTER_INTERFACE_GUID(0xF5C8D8A37D37BC42ULL, 0xB02DFE93B5064A27ULL, IUnityGraphicsD3D12v5)
