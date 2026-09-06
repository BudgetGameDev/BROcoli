#include <Windows.h>
#include <wintrust.h>
#include <softpub.h>
#include <cstdio>
#include "Security.h"
#include "sl_security.h"

namespace bgd
{
bool VerifyNvidiaLibrary(const wchar_t* path, bool streamline, std::string& detail)
{
    // NGX binaries have ordinary NVIDIA Authenticode signatures. Only sl.* uses
    // Streamline's additional embedded signature/public key (sl_security.h).
    WINTRUST_FILE_INFO file{};
    file.cbStruct = sizeof(file);
    file.pcwszFilePath = path;
    WINTRUST_DATA trust{};
    trust.cbStruct = sizeof(trust);
    trust.dwUIChoice = WTD_UI_NONE;
    trust.fdwRevocationChecks = WTD_REVOKE_NONE;
    trust.dwUnionChoice = WTD_CHOICE_FILE;
    trust.pFile = &file;
    trust.dwStateAction = WTD_STATEACTION_VERIFY;
    GUID policy = WINTRUST_ACTION_GENERIC_VERIFY_V2;
    LONG result = WinVerifyTrust(nullptr, &policy, &trust);
    bool valid = result == ERROR_SUCCESS;
    wchar_t organization[256]{};
    if (valid)
    {
        auto data = WTHelperProvDataFromStateData(trust.hWVTStateData);
        auto signer = data ? WTHelperGetProvSignerFromChain(data, 0, FALSE, 0) : nullptr;
        auto cert = signer ? WTHelperGetProvCertFromChain(signer, 0) : nullptr;
        if (cert)
            CertGetNameStringW(cert->pCert, CERT_NAME_ATTR_TYPE, 0,
                const_cast<char*>(szOID_ORGANIZATION_NAME), organization, 256);
        valid = wcscmp(organization, L"NVIDIA Corporation") == 0;
    }
    char code[32]{};
    snprintf(code, sizeof(code), "0x%08lX", static_cast<unsigned long>(result));
    detail = std::string("Authenticode=") + code + "; NVIDIA publisher=" + (valid ? "verified" : "rejected");
    trust.dwStateAction = WTD_STATEACTION_CLOSE;
    WinVerifyTrust(nullptr, &policy, &trust);
    if (valid && streamline)
    {
        valid = sl::security::verifyEmbeddedSignature(path);
        detail += std::string("; Streamline secondary signature=") + (valid ? "verified" : "rejected");
    }
    return valid;
}
}
