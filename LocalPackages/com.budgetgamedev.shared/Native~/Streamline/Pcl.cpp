#include "Bridge.h"

namespace bgd
{
static HWND pclWindow{};
static WNDPROC previousProcedure{};
static uint32_t pingMessage{};
static sl::FrameToken* simulationFrame{};

void SetSimulationFrame(sl::FrameToken* frame) { simulationFrame = frame; }

static LRESULT CALLBACK WindowProcedure(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
{
    WNDPROC previous{};
    {
        std::lock_guard lock(mutex);
        previous = previousProcedure;
        if (pclSupported && simulationFrame)
        {
            if (message == pingMessage && pingMessage)
                Marker(sl::PCLMarker::ePCLatencyPing, simulationFrame);
            if (message == WM_LBUTTONDOWN)
                Marker(sl::PCLMarker::eTriggerFlash, simulationFrame);
        }
    }
    return previous ? CallWindowProcW(previous, window, message, wparam, lparam)
                    : DefWindowProcW(window, message, wparam, lparam);
}

void BindPclWindow(HWND window)
{
    if (!pclSupported || !window || window == pclWindow) return;
    RestorePclWindow();
    sl::PCLState state;
    if (!Check(api.pclState(state))) return;
    pingMessage = state.statsWindowMessage;
    SetLastError(0);
    previousProcedure = reinterpret_cast<WNDPROC>(SetWindowLongPtrW(window, GWLP_WNDPROC,
        reinterpret_cast<LONG_PTR>(WindowProcedure)));
    if (previousProcedure) { pclWindow = window; diagnostics.pclWindowBound = 1; }
    else Log("PCL window callback could not be installed.");
}

void RestorePclWindow()
{
    if (pclWindow && IsWindow(pclWindow)
        && GetWindowLongPtrW(pclWindow, GWLP_WNDPROC) == reinterpret_cast<LONG_PTR>(WindowProcedure))
        SetWindowLongPtrW(pclWindow, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(previousProcedure));
    diagnostics.pclWindowBound = 0;
    pclWindow = nullptr;
    previousProcedure = nullptr;
    simulationFrame = nullptr;
}
}
