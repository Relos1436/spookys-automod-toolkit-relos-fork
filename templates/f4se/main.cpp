#include <Windows.h>
#include "common/ITypes.h"
#include "f4se/PluginAPI.h"
#include "f4se_common/f4se_version.h"

// This template deliberately uses only the public extender API: no engine offsets or hooks.
static_assert(CURRENT_RELEASE_RUNTIME == @RUNTIME@, "SDK does not match this project's target runtime.");
static_assert(sizeof(void*) == 8 && sizeof(UInt32) == 4);
namespace {
bool Compatible(const F4SEInterface* api) {
    return api && !api->isEditor && api->runtimeVersion == @RUNTIME@
        && api->f4seVersion >= PACKED_F4SE_VERSION;
}
void OnMessage(F4SEMessagingInterface::Message* message) {
    if (message && message->type == F4SEMessagingInterface::kMessage_GameLoaded) {
        OutputDebugStringA("[@NAME@] Game loaded.\n");
        // Add game-ready initialization here. Engine access needs a matching game SDK/framework.
    }
}
}
extern "C" __declspec(dllexport) bool F4SEPlugin_Query(const F4SEInterface* api, PluginInfo* info) {
    if (!info) return false;
    info->infoVersion = PluginInfo::kInfoVersion;
    info->name = "@NAME@";
    info->version = 1;
    return Compatible(api);
}
extern "C" __declspec(dllexport) bool F4SEPlugin_Load(const F4SEInterface* api) {
    if (!Compatible(api) || !api->QueryInterface || !api->GetPluginHandle) return false;
    auto* messaging = static_cast<F4SEMessagingInterface*>(api->QueryInterface(kInterface_Messaging));
    if (!messaging || messaging->interfaceVersion < F4SEMessagingInterface::kInterfaceVersion
        || !messaging->RegisterListener) return false;
    auto handle = api->GetPluginHandle();
    if (handle == kPluginHandle_Invalid) return false;
    if (!messaging->RegisterListener(handle, "F4SE", OnMessage)) return false;
    OutputDebugStringA("[@NAME@] Plugin loaded.\n");
    return true;
}
