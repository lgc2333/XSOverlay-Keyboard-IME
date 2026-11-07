// Native profile notifications use the same managed-delegate boundary as the
// composition callbacks. LGPL-2.1, like shim.cpp. See ADR 0024 and ADR 0045.
#include <Windows.h>
#include <msctf.h>

using ProfileChangedFn = void(__cdecl*)(void* user);

class ProfileNotifications final : public ITfInputProcessorProfileActivationSink
{
public:
    ProfileNotifications(ProfileChangedFn changed, void* user) : changed_(changed), user_(user) {}

    HRESULT Open()
    {
        ITfThreadMgr* manager = nullptr;
        HMODULE module = GetModuleHandleW(L"msctf.dll");
        using GetThreadManagerFn = HRESULT(WINAPI*)(ITfThreadMgr**);
        auto getThreadManager = module
            ? reinterpret_cast<GetThreadManagerFn>(GetProcAddress(module, "TF_GetThreadMgr"))
            : nullptr;
        if (!getThreadManager) return E_NOINTERFACE;
        HRESULT result = getThreadManager(&manager);
        if (FAILED(result) || !manager) return FAILED(result) ? result : E_NOINTERFACE;
        result = manager->QueryInterface(IID_PPV_ARGS(&source_));
        manager->Release();
        if (FAILED(result)) return result;
        return source_->AdviseSink(__uuidof(ITfInputProcessorProfileActivationSink), this, &cookie_);
    }

    void Close()
    {
        changed_ = nullptr;
        if (source_)
        {
            if (cookie_ != TF_INVALID_COOKIE) source_->UnadviseSink(cookie_);
            source_->Release();
            source_ = nullptr;
        }
        Release();
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** object) override
    {
        if (!object) return E_POINTER;
        *object = nullptr;
        if (iid != IID_IUnknown && iid != __uuidof(ITfInputProcessorProfileActivationSink)) return E_NOINTERFACE;
        *object = static_cast<ITfInputProcessorProfileActivationSink*>(this);
        AddRef();
        return S_OK;
    }

    ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&references_); }
    ULONG STDMETHODCALLTYPE Release() override
    {
        ULONG remaining = InterlockedDecrement(&references_);
        if (!remaining) delete this;
        return remaining;
    }

    HRESULT STDMETHODCALLTYPE OnActivated(DWORD type, LANGID, REFCLSID, REFGUID category,
        REFGUID, HKL, DWORD) override
    {
        if (changed_ && (type == TF_PROFILETYPE_KEYBOARDLAYOUT ||
            (type == TF_PROFILETYPE_INPUTPROCESSOR && category == GUID_TFCAT_TIP_KEYBOARD)))
            changed_(user_);
        return S_OK;
    }

private:
    LONG references_ = 1;
    ITfSource* source_ = nullptr;
    DWORD cookie_ = TF_INVALID_COOKIE;
    ProfileChangedFn changed_;
    void* user_;
};

extern "C" __declspec(dllexport) void* __cdecl iime_watch_profiles(ProfileChangedFn changed, void* user, HRESULT* result)
{
    auto* watch = new ProfileNotifications(changed, user);
    *result = watch->Open();
    if (FAILED(*result))
    {
        watch->Close();
        return nullptr;
    }
    return watch;
}

extern "C" __declspec(dllexport) void __cdecl iime_unwatch_profiles(void* watch)
{
    if (watch) static_cast<ProfileNotifications*>(watch)->Close();
}
