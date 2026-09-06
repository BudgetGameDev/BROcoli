#include <Windows.h>
#include "Security.h"
#include <filesystem>
#include <fstream>
#include <iostream>

int wmain(int argc, wchar_t** argv)
{
    if (argc != 2) return 2;
    std::filesystem::path payload(argv[1]);
    int failures{};
    auto expect = [&](const std::filesystem::path& path, bool streamline, bool expected) {
        std::string detail;
        bool actual = bgd::VerifyNvidiaLibrary(path.c_str(), streamline, detail);
        std::cout << path.filename().string() << ": " << detail << '\n';
        if (actual != expected) ++failures;
    };
    for (auto name : {L"sl.interposer.dll", L"sl.common.dll", L"sl.dlss.dll", L"sl.dlss_g.dll", L"sl.reflex.dll", L"sl.pcl.dll"})
        expect(payload / name, true, true);
    for (auto name : {L"nvngx_dlssg.dll", L"nvngx_dlss.dll"}) expect(payload / name, false, true);
    expect(payload / L"missing.dll", false, false);
    wchar_t system[32768]{};
    GetSystemDirectoryW(system, 32768);
    expect(std::filesystem::path(system) / L"kernel32.dll", false, false);
    auto tampered = std::filesystem::temp_directory_path() / (L"streamline-signature-test-" + std::to_wstring(GetCurrentProcessId()) + L".dll");
    std::filesystem::copy_file(payload / L"nvngx_dlssg.dll", tampered);
    {
        std::fstream file(tampered, std::ios::binary | std::ios::in | std::ios::out);
        file.seekg(4096); char value{}; file.read(&value, 1); value ^= 1;
        file.seekp(4096); file.write(&value, 1);
    }
    expect(tampered, false, false);
    std::filesystem::remove(tampered);
    return failures ? 1 : 0;
}
