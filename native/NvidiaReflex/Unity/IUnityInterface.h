// Unity Native Plugin API copyright © 2015 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity-dependent projects--see
// Unity Companion License (http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made
// available strictly on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
// Please review the license for details on these and other terms and conditions.

#pragma once

// Unity native plugin API
// Compatible with C99

#if defined(__CYGWIN32__)
    #define UNITY_INTERFACE_API __stdcall
    #define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#elif defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(_WIN64) || defined(WINAPI_FAMILY)
    #define UNITY_INTERFACE_API __stdcall
    #define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#elif defined(__MACH__) || defined(__ANDROID__) || defined(__linux__) || defined(LUMIN)
    #define UNITY_INTERFACE_API
    #define UNITY_INTERFACE_EXPORT __attribute__ ((visibility ("default")))
#else
    #define UNITY_INTERFACE_API
    #define UNITY_INTERFACE_EXPORT
#endif

// Unity Interface GUID
struct UnityInterfaceGUID
{
#ifdef __cplusplus
    UnityInterfaceGUID(unsigned long long high, unsigned long long low)
        : m_GUIDHigh(high)
        , m_GUIDLow(low)
    {
    }

    UnityInterfaceGUID(const UnityInterfaceGUID& other)
    {
        m_GUIDHigh = other.m_GUIDHigh;
        m_GUIDLow = other.m_GUIDLow;
    }

    UnityInterfaceGUID& operator=(const UnityInterfaceGUID& other)
    {
        m_GUIDHigh = other.m_GUIDHigh;
        m_GUIDLow = other.m_GUIDLow;
        return *this;
    }

    bool Equals(const UnityInterfaceGUID& other) const { return m_GUIDHigh == other.m_GUIDHigh && m_GUIDLow == other.m_GUIDLow; }
    bool LessThan(const UnityInterfaceGUID& other) const { return m_GUIDHigh < other.m_GUIDHigh || (m_GUIDHigh == other.m_GUIDHigh && m_GUIDLow < other.m_GUIDLow); }
#endif
    unsigned long long m_GUIDHigh;
    unsigned long long m_GUIDLow;
};

#ifdef __cplusplus
inline bool operator==(const UnityInterfaceGUID& left, const UnityInterfaceGUID& right) { return left.Equals(right); }
inline bool operator!=(const UnityInterfaceGUID& left, const UnityInterfaceGUID& right) { return !left.Equals(right); }
inline bool operator<(const UnityInterfaceGUID& left, const UnityInterfaceGUID& right) { return left.LessThan(right); }
inline bool operator>(const UnityInterfaceGUID& left, const UnityInterfaceGUID& right) { return right.LessThan(left); }
inline bool operator>=(const UnityInterfaceGUID& left, const UnityInterfaceGUID& right) { return !operator<(left, right); }
inline bool operator<=(const UnityInterfaceGUID& left, const UnityInterfaceGUID& right) { return !operator>(left, right); }
#else
typedef struct UnityInterfaceGUID UnityInterfaceGUID;
#endif

#ifdef __cplusplus
    #define UNITY_DECLARE_INTERFACE(NAME) \
    struct NAME : IUnityInterface

    template<typename TYPE>
    inline const UnityInterfaceGUID GetUnityInterfaceGUID();

    #define UNITY_REGISTER_INTERFACE_GUID(HASHH, HASHL, TYPE) \
    template<> \
    inline const UnityInterfaceGUID GetUnityInterfaceGUID<TYPE>() \
    { \
        return UnityInterfaceGUID(HASHH, HASHL); \
    }

    #define UNITY_REGISTER_INTERFACE_GUID_IN_NAMESPACE(HASHH, HASHL, TYPE, NAMESPACE) \
    const UnityInterfaceGUID TYPE##_GUID(HASHH, HASHL); \
    template<> \
    inline const UnityInterfaceGUID GetUnityInterfaceGUID<NAMESPACE::TYPE>() \
    { \
        return UnityInterfaceGUID(HASHH, HASHL); \
    }

    #define UNITY_GET_INTERFACE_GUID(TYPE) GetUnityInterfaceGUID<TYPE>()

#else
    #define UNITY_DECLARE_INTERFACE(NAME) \
    typedef struct NAME NAME; \
    struct NAME

    #define UNITY_REGISTER_INTERFACE_GUID(HASHH, HASHL, TYPE) \
    const UnityInterfaceGUID TYPE##_GUID = {HASHH, HASHL};

    #define UNITY_REGISTER_INTERFACE_GUID_IN_NAMESPACE(HASHH, HASHL, TYPE, NAMESPACE)

    #define UNITY_GET_INTERFACE_GUID(TYPE) TYPE##_GUID
#endif

#define UNITY_GET_INTERFACE(INTERFACES, TYPE) (TYPE*)INTERFACES->GetInterfaceSplit(UNITY_GET_INTERFACE_GUID(TYPE).m_GUIDHigh, UNITY_GET_INTERFACE_GUID(TYPE).m_GUIDLow);

#ifdef __cplusplus
struct IUnityInterface
{
};
#else
typedef void IUnityInterface;
#endif

typedef struct IUnityInterfaces
{
    IUnityInterface* (UNITY_INTERFACE_API * GetInterface)(UnityInterfaceGUID guid);
    void(UNITY_INTERFACE_API * RegisterInterface)(UnityInterfaceGUID guid, IUnityInterface* ptr);
    IUnityInterface* (UNITY_INTERFACE_API * GetInterfaceSplit)(unsigned long long guidHigh, unsigned long long guidLow);
    void(UNITY_INTERFACE_API * RegisterInterfaceSplit)(unsigned long long guidHigh, unsigned long long guidLow, IUnityInterface* ptr);

#ifdef __cplusplus
    template<typename INTERFACE>
    INTERFACE* Get()
    {
        return static_cast<INTERFACE*>(GetInterface(GetUnityInterfaceGUID<INTERFACE>()));
    }

    template<typename INTERFACE>
    void Register(IUnityInterface* ptr)
    {
        RegisterInterface(GetUnityInterfaceGUID<INTERFACE>(), ptr);
    }
#endif
} IUnityInterfaces;

#ifdef __cplusplus
extern "C"
{
#endif

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces);
void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload();

#ifdef __cplusplus
}
#endif

struct RenderSurfaceBase;
typedef struct RenderSurfaceBase* UnityRenderBuffer;
typedef unsigned int UnityTextureID;
