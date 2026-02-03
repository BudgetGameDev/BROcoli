// StreamlineCommon.cpp
// Shared state and logging for the Streamline plugin
//
// This file contains global state and logging functions used by all modules.

#include "StreamlineCommon.h"
#include <cstdarg>
#include <ctime>
#include <cstring>

// ============================================================================
// Global State Definitions
// ============================================================================

bool g_initialized = false;
bool g_reflexSupported = false;
bool g_pclSupported = false;
bool g_dlssSupported = false;
bool g_dlssgSupported = false;

sl::ReflexMode g_currentMode = sl::ReflexMode::eOff;
sl::DLSSMode g_dlssMode = sl::DLSSMode::eOff;
sl::DLSSGMode g_dlssgMode = sl::DLSSGMode::eOff;
uint32_t g_numFramesToGenerate = 1;
uint64_t g_frameId = 0;

std::mutex g_mutex;

LogCallback g_logCallback = nullptr;
FILE* g_logFile = nullptr;

sl::ViewportHandle g_dlssViewport = {0};

// ============================================================================
// File Logging
// ============================================================================

static char g_logFilePath[MAX_PATH] = {0};

void InitLogFile()
{
    if (g_logFile) return;  // Already initialized
    
    // Get the directory of the executable
    char exePath[MAX_PATH];
    GetModuleFileNameA(NULL, exePath, MAX_PATH);
    
    // Find the last backslash and replace filename with log filename
    char* lastSlash = strrchr(exePath, '\\');
    if (lastSlash)
    {
        *(lastSlash + 1) = '\0';
        snprintf(g_logFilePath, MAX_PATH, "%sGfxPluginStreamline.log", exePath);
    }
    else
    {
        snprintf(g_logFilePath, MAX_PATH, "GfxPluginStreamline.log");
    }
    
    // Open log file (overwrite previous)
    g_logFile = fopen(g_logFilePath, "w");
    if (g_logFile)
    {
        // Write header
        time_t now = time(NULL);
        struct tm* t = localtime(&now);
        fprintf(g_logFile, "=== GfxPluginStreamline Log ===\n");
        fprintf(g_logFile, "Started: %04d-%02d-%02d %02d:%02d:%02d\n",
                t->tm_year + 1900, t->tm_mon + 1, t->tm_mday,
                t->tm_hour, t->tm_min, t->tm_sec);
        fprintf(g_logFile, "Log file: %s\n", g_logFilePath);
        fprintf(g_logFile, "================================\n\n");
        fflush(g_logFile);
    }
}

void CloseLogFile()
{
    if (g_logFile)
    {
        fprintf(g_logFile, "\n=== Log Closed ===\n");
        fclose(g_logFile);
        g_logFile = nullptr;
    }
}

// ============================================================================
// Logging
// ============================================================================

void LogMessage(const char* format, ...)
{
    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);
    
    // Get timestamp
    time_t now = time(NULL);
    struct tm* t = localtime(&now);
    char timestamp[32];
    snprintf(timestamp, sizeof(timestamp), "[%02d:%02d:%02d] ",
             t->tm_hour, t->tm_min, t->tm_sec);
    
    // Write to log file
    if (g_logFile)
    {
        fprintf(g_logFile, "%s%s\n", timestamp, buffer);
        fflush(g_logFile);  // Flush immediately so we don't lose logs on crash
    }
    
    // C# callback
    if (g_logCallback)
    {
        g_logCallback(buffer);
    }
    
    // Debug output
    OutputDebugStringA("[GfxPluginStreamline] ");
    OutputDebugStringA(buffer);
    OutputDebugStringA("\n");
}

// ============================================================================
// Exported Logging API
// ============================================================================

EXPORT void SLReflex_SetLogCallback(LogCallback callback)
{
    g_logCallback = callback;
}
