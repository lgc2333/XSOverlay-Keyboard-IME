// Verified private Shell ABI; see docs/handoffs/archived-notes/input-mode-shell.md. LGPL-2.1, like shim.cpp.
#include <Windows.h>
#include <bcrypt.h>
#include <objbase.h>

#include <array>
#include <cstdint>

#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "ole32.lib")

namespace
{
const GUID ClassId = {0xb9bc2a50, 0x43c3, 0x41aa, {0xa0, 0x86, 0x5d, 0xb1, 0x4e, 0x18, 0x4b, 0xae}};
const GUID ControlId = {0xb9bc2a50, 0x43c3, 0x41aa, {0xa0, 0x82, 0x5d, 0xb1, 0x4e, 0x18, 0x4b, 0xae}};
const GUID CallbackId = {0xb9bc2a50, 0x43c3, 0x41aa, {0xa0, 0x83, 0x5d, 0xb1, 0x4e, 0x18, 0x4b, 0xae}};

struct ModeItem
{
    WCHAR* tooltip;
    HICON icon;
    BOOL disabled;
    BOOL hidden;
    WCHAR* glyph;
    void* reserved;
};
static_assert(sizeof(void*) == 8 && sizeof(ModeItem) == 40, "Verified x64 Shell layout");

struct ModeCallback : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE OnUpdateProfile(const void*) = 0;
    virtual HRESULT STDMETHODCALLTYPE OnUpdateTsfFloatingFlags(ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE OnProfileCountChange(UINT, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE OnShowHide(BOOL, BOOL, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE OnImeModeItemUpdate(const ModeItem*) = 0;
    virtual HRESULT STDMETHODCALLTYPE OnModalitySelected(int) = 0;
    virtual HRESULT STDMETHODCALLTYPE OnContextFlagsChange(ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE OnTouchKeyboardManualInvoke() = 0;
};

// No calls through an unverified slot, even when COM registration points elsewhere.
bool VerifyModule(HMODULE module)
{
    WCHAR path[MAX_PATH];
    DWORD length = GetModuleFileNameW(module, path, MAX_PATH);
    if (!length || length >= MAX_PATH) return false;
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE,
        nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return false;
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    std::array<BYTE, 32> actual{};
    constexpr std::array<BYTE, 32> expected = {
        0xe9,0xbf,0x1d,0x4f,0xb9,0xcd,0x9e,0x8c,0x4d,0xf6,0x75,0xba,0x83,0x1a,0xfa,0x62,
        0xcb,0x48,0x71,0x47,0xf9,0x5c,0xc2,0x63,0x8f,0x28,0x20,0xc6,0x65,0xde,0x57,0xc8};
    bool valid = BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) >= 0
        && BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) >= 0;
    BYTE buffer[16384];
    DWORD read = 0;
    while (valid)
    {
        if (!ReadFile(file, buffer, sizeof(buffer), &read, nullptr)) { valid = false; break; }
        if (!read) break;
        valid = BCryptHashData(hash, buffer, read, 0) >= 0;
    }
    if (valid) valid = BCryptFinishHash(hash, actual.data(), static_cast<ULONG>(actual.size()), 0) >= 0
        && actual == expected;
    if (hash) BCryptDestroyHash(hash);
    if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0);
    CloseHandle(file);
    return valid;
}

template<class Function> Function Slot(IUnknown* control, size_t slot)
{
    return reinterpret_cast<Function>((*reinterpret_cast<void***>(control))[slot]);
}
using InitFn = HRESULT(STDMETHODCALLTYPE*)(IUnknown*, int);
using SetCallbackFn = HRESULT(STDMETHODCALLTYPE*)(IUnknown*, ModeCallback*);
using ReadFn = HRESULT(STDMETHODCALLTYPE*)(IUnknown*, ModeItem*);
using ChangedFn = void(__cdecl*)(void*);

class ShellModeButton final : public ModeCallback
{
public:
    ShellModeButton(HWND host, ChangedFn changed, void* user) : host_(host), changed_(changed), user_(user) {}
    HRESULT Open()
    {
        if (!OwnsFocus()) return E_ACCESSDENIED;
        module_ = LoadLibraryExW(L"InputSwitch.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (!module_ || !VerifyModule(module_)) return HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH);
        HRESULT result = CoCreateInstance(ClassId, nullptr, CLSCTX_INPROC_SERVER, ControlId,
            reinterpret_cast<void**>(&control_));
        if (FAILED(result)) return result;
        const auto base = reinterpret_cast<uintptr_t>(module_);
        auto table = *reinterpret_cast<uintptr_t**>(control_);
        if (table[3] != base + 0x27200 || table[4] != base + 0x221a0 ||
            table[15] != base + 0x19490)
            return HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH);
        result = Slot<InitFn>(control_, 3)(control_, 0);
        if (FAILED(result)) return result;
        initialized_ = true;
        return Slot<SetCallbackFn>(control_, 4)(control_, this);
    }
    void Close()
    {
        changed_ = nullptr;
        if (control_)
        {
            if (initialized_) Slot<SetCallbackFn>(control_, 4)(control_, nullptr);
            control_->Release();
            control_ = nullptr;
        }
        if (module_) { FreeLibrary(module_); module_ = nullptr; }
        Release();
    }
    HRESULT Read(ModeItem* item)
    {
        *item = {};
        return OwnsFocus() && OwnsCache() ? Slot<ReadFn>(control_, 15)(control_, item) : E_ACCESSDENIED;
    }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** value) override
    {
        if (!value) return E_POINTER;
        *value = nullptr;
        if (iid != IID_IUnknown && iid != CallbackId) return E_NOINTERFACE;
        *value = static_cast<ModeCallback*>(this);
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&references_); }
    ULONG STDMETHODCALLTYPE Release() override
    {
        ULONG left = InterlockedDecrement(&references_);
        if (!left) delete this;
        return left;
    }
    HRESULT STDMETHODCALLTYPE OnImeModeItemUpdate(const ModeItem*) override
    {
        // Callback fields are borrowed, may be null, and can belong to another window.
        if (changed_ && OwnsFocus() && OwnsCache()) changed_(user_);
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE OnUpdateProfile(const void*) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE OnUpdateTsfFloatingFlags(ULONG) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE OnProfileCountChange(UINT, BOOL) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE OnShowHide(BOOL, BOOL, BOOL) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE OnModalitySelected(int) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE OnContextFlagsChange(ULONG) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE OnTouchKeyboardManualInvoke() override { return S_OK; }
private:
    bool OwnsFocus() const { return GetForegroundWindow() == host_ && GetFocus() == host_; }
    bool OwnsCache() const
    {
        // This owner tag and its offsets are covered by the exact module hash gate.
        if (!initialized_ || !control_) return false;
        auto* handler = *reinterpret_cast<const BYTE* const*>(reinterpret_cast<const BYTE*>(control_) + 0x30);
        return handler && *reinterpret_cast<const DWORD*>(handler + 0x68) == GetCurrentThreadId()
            && *reinterpret_cast<const HWND*>(handler + 0x70) == host_;
    }
    LONG references_ = 1;
    HWND host_;
    ChangedFn changed_;
    void* user_;
    HMODULE module_ = nullptr;
    IUnknown* control_ = nullptr;
    bool initialized_ = false;
};
}

extern "C" __declspec(dllexport) void* __cdecl iime_shell_mode_open(HWND host, ChangedFn changed,
    void* user, HRESULT* result)
{
    auto* button = new ShellModeButton(host, changed, user);
    *result = button->Open();
    if (FAILED(*result)) { button->Close(); return nullptr; }
    return button;
}
extern "C" __declspec(dllexport) void __cdecl iime_shell_mode_close(void* button)
{
    if (button) static_cast<ShellModeButton*>(button)->Close();
}
extern "C" __declspec(dllexport) HRESULT __cdecl iime_shell_mode_read(void* button, ModeItem* item)
{
    return static_cast<ShellModeButton*>(button)->Read(item);
}
extern "C" __declspec(dllexport) void __cdecl iime_shell_mode_free(ModeItem* item)
{
    CoTaskMemFree(item->tooltip);
    if (item->icon) DestroyIcon(item->icon);
    CoTaskMemFree(item->glyph);
    *item = {};
}
