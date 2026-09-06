#include "Bridge.h"
#include <vector>
#include <cstring>

namespace bgd
{
struct ImportPatch { ULONG_PTR* slot; ULONG_PTR original; ULONG_PTR replacement; };
static std::vector<ImportPatch> patches;

// Redirect only UnityPlayer's own graphics entry points. Streamline continues to
// call the real system DLLs, and other native plugins retain their normal imports.
static FARPROC Replacement(const char* name)
{
    if (!name || reinterpret_cast<uintptr_t>(name) <= 0xffff) return nullptr;
    const char* functions[] = {"CreateDXGIFactory", "CreateDXGIFactory1", "CreateDXGIFactory2",
        "DXGIGetDebugInterface1", "D3D12CreateDevice"};
    for (const char* function : functions)
        if (!strcmp(name, function)) return GetProcAddress(api.module, name);
    return nullptr;
}
static FARPROC WINAPI GraphicsGetProcAddress(HMODULE module, LPCSTR name)
{
    if (module == GetModuleHandleW(L"dxgi.dll") || module == GetModuleHandleW(L"d3d12.dll"))
        if (auto replacement = Replacement(name)) return replacement;
    return GetProcAddress(module, name);
}
static bool Patch(ULONG_PTR* slot, ULONG_PTR replacement)
{
    DWORD protect{};
    if (!VirtualProtect(slot, sizeof(*slot), PAGE_READWRITE, &protect)) return false;
    patches.push_back({slot, *slot, replacement});
    InterlockedExchangePointer(reinterpret_cast<void* volatile*>(slot), reinterpret_cast<void*>(replacement));
    DWORD unused{};
    VirtualProtect(slot, sizeof(*slot), protect, &unused);
    return true;
}
bool InstallImports()
{
    auto base = reinterpret_cast<uint8_t*>(GetModuleHandleW(L"UnityPlayer.dll"));
    if (!base) return false;
    auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) return false;
    auto nt = reinterpret_cast<IMAGE_NT_HEADERS64*>(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE) return false;
    auto directory = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (!directory.VirtualAddress) return false;
    auto imports = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(base + directory.VirtualAddress);
    bool redirectedResolver = false;
    for (; imports->Name; ++imports)
    {
        if (!imports->OriginalFirstThunk) continue;
        const char* library = reinterpret_cast<const char*>(base + imports->Name);
        bool graphicsLibrary = !_stricmp(library, "dxgi.dll") || !_stricmp(library, "d3d12.dll");
        auto names = reinterpret_cast<IMAGE_THUNK_DATA64*>(base + imports->OriginalFirstThunk);
        auto addresses = reinterpret_cast<IMAGE_THUNK_DATA64*>(base + imports->FirstThunk);
        for (; names->u1.AddressOfData; ++names, ++addresses)
        {
            if (IMAGE_SNAP_BY_ORDINAL64(names->u1.Ordinal)) continue;
            auto entry = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(base + names->u1.AddressOfData);
            const char* name = entry->Name;
            if (!strcmp(name, "GetProcAddress"))
                redirectedResolver |= Patch(&addresses->u1.Function, reinterpret_cast<ULONG_PTR>(GraphicsGetProcAddress));
            else if (graphicsLibrary)
                if (auto replacement = Replacement(name))
                    Patch(&addresses->u1.Function, reinterpret_cast<ULONG_PTR>(replacement));
        }
    }
    // Unity loads the graphics runtime dynamically; require that path to work.
    if (!redirectedResolver) RestoreImports();
    return redirectedResolver;
}
void RestoreImports()
{
    for (const auto& patch : patches)
    {
        DWORD protect{};
        if (*patch.slot == patch.replacement && VirtualProtect(patch.slot, sizeof(ULONG_PTR), PAGE_READWRITE, &protect))
        {
            *patch.slot = patch.original;
            DWORD unused{}; VirtualProtect(patch.slot, sizeof(ULONG_PTR), protect, &unused);
        }
    }
    patches.clear();
}
}
