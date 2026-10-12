// Standalone ABI/contract test. This does not simulate Fallout or prove in-game compatibility.
#include <Windows.h>
#include <cstring>
#include <iostream>
#include "common/ITypes.h"
#include "f4se/PluginAPI.h"
#include "f4se_common/f4se_version.h"
namespace {
F4SEMessagingInterface::EventCallback listener = nullptr;
bool registerResult = true;
bool Register(PluginHandle handle, const char* sender, F4SEMessagingInterface::EventCallback callback) {
    if (handle != 42 || !sender || std::strcmp(sender, "F4SE")) return false;
    if (registerResult) listener = callback;
    return registerResult;
}
F4SEMessagingInterface messaging { F4SEMessagingInterface::kInterfaceVersion, Register, nullptr, nullptr };
void* Query(UInt32 id) { return id == kInterface_Messaging ? &messaging : nullptr; }
void* Missing(UInt32) { return nullptr; }
PluginHandle Handle() { return 42; }
PluginHandle BadHandle() { return kPluginHandle_Invalid; }
bool Check(bool condition, const char* what) {
    if (!condition) std::cerr << "FAILED: " << what << '\n';
    return condition;
}
}
int wmain(int argc, wchar_t** argv) {
    if (argc != 2) return 2;
    auto library = LoadLibraryW(argv[1]);
    if (!library) { std::cerr << "LoadLibrary failed: " << GetLastError(); return 1; }
    auto query = reinterpret_cast<_F4SEPlugin_Query>(GetProcAddress(library, "F4SEPlugin_Query"));
    auto load = reinterpret_cast<_F4SEPlugin_Load>(GetProcAddress(library, "F4SEPlugin_Load"));
    if (!query || !load) { FreeLibrary(library); return 1; }
    bool ok = true;
    PluginInfo info{};
    F4SEInterface api { PACKED_F4SE_VERSION, @RUNTIME@, 0, 0, Query, Handle, nullptr };
    ok &= Check(query(&api, &info) && info.infoVersion == PluginInfo::kInfoVersion
        && info.name && std::strcmp(info.name, "@NAME@") == 0 && info.version == 1, "query metadata");
    ok &= Check(!query(nullptr, &info) && !query(&api, nullptr) && !load(nullptr), "null inputs");
    auto bad = api; bad.runtimeVersion ^= 0x10;
    ok &= Check(!query(&bad, &info) && !load(&bad), "wrong runtime");
    bad = api; bad.isEditor = 1;
    ok &= Check(!query(&bad, &info) && !load(&bad), "editor rejected");
    bad = api; bad.f4seVersion = 0;
    ok &= Check(!query(&bad, &info) && !load(&bad), "old extender rejected");
    bad = api; bad.QueryInterface = Missing;
    ok &= Check(!load(&bad), "missing messaging");
    bad = api; bad.GetPluginHandle = BadHandle;
    ok &= Check(!load(&bad), "invalid plugin handle");
    messaging.interfaceVersion = 0;
    ok &= Check(!load(&api), "old messaging API");
    messaging.interfaceVersion = F4SEMessagingInterface::kInterfaceVersion;
    registerResult = false;
    ok &= Check(!load(&api), "listener failure");
    registerResult = true;
    ok &= Check(load(&api) && listener, "load and listener registration");
    if (listener) {
        F4SEMessagingInterface::Message message { "F4SE", F4SEMessagingInterface::kMessage_GameLoaded, 0, nullptr };
        listener(nullptr); listener(&message);
    }
    FreeLibrary(library);
    if (ok) std::cout << "Plugin contract passed (mock host; in-game test still required).\n";
    return ok ? 0 : 1;
}
