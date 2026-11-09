#include "../shim/transitory-context.cpp"

#include <cstdio>

// The framework supplies these ranges and consumes deleteResult in production.
// This fixture exercises the real sink under an actual TSF edit cookie.
struct TransitoryContextTest {
  static inline std::string committed;
  static inline HWND retiredWindow{};
  static inline bool retainedDuringCallback{};

  static void __cdecl Commit(const char *text, void *) { committed = text; }
  static void __cdecl RetireFromCallback(int, int, int, const char *,
                                         void *user) {
    auto context = static_cast<TransitoryContext *>(user);
    retiredWindow = context->retireWindow_;
    context->Retire();
    PostMessageW(retiredWindow, kRetireMessage, 0, 0);
    MSG message;
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
      DispatchMessageW(&message);
    retainedDuringCallback =
        IsWindow(retiredWindow) && context->frameDepth_ > 0;
  }

  class Edit final
      : public Microsoft::WRL::RuntimeClass<
            Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
            ITfEditSession> {
  public:
    explicit Edit(TransitoryContext *context) : context_(context) {}
    HRESULT STDMETHODCALLTYPE DoEditSession(TfEditCookie cookie) override {
      auto impl = context_->impl_.get();
      ComPtr<ITfRange> text, result, composition;
      HRESULT hr = impl->childContext_->GetStart(cookie, &text);
      if (FAILED(hr))
        return hr;
      hr = text->SetText(cookie, 0, L"\x63d0\x4ea4\x65b0\x7ec4\x5b57", 5);
      if (FAILED(hr))
        return hr;
      hr = text->Clone(&result);
      if (FAILED(hr))
        return hr;
      hr = text->Clone(&composition);
      if (FAILED(hr))
        return hr;
      LONG moved = 0;
      hr = result->ShiftEnd(cookie, -3, &moved, nullptr);
      if (FAILED(hr) || moved != -3)
        return E_FAIL;
      hr = composition->ShiftStart(cookie, 2, &moved, nullptr);
      if (FAILED(hr) || moved != 2)
        return E_FAIL;
      BOOL remove = FALSE;
      // An overlapping result must never authorize deleting live composition.
      hr = impl->sink_->OnTransitoryExtensionUpdated(
          impl->childContext_.Get(), cookie, text.Get(), composition.Get(),
          &remove);
      if (SUCCEEDED(hr) || remove || !committed.empty())
        return E_FAIL;
      hr = impl->sink_->OnTransitoryExtensionUpdated(
          impl->childContext_.Get(), cookie, result.Get(), composition.Get(),
          &remove);
      if (FAILED(hr) || !remove || committed != "\xe6\x8f\x90\xe4\xba\xa4")
        return E_FAIL;
      hr = result->SetText(cookie, 0, L"", 0);
      if (FAILED(hr))
        return hr;
      std::wstring remaining;
      hr = ReadRange(composition.Get(), cookie, remaining);
      return SUCCEEDED(hr) && remaining == L"\x65b0\x7ec4\x5b57" ? S_OK
                                                                 : E_FAIL;
    }

  private:
    TransitoryContext *context_;
  };

  static bool Run(HWND window) {
    auto context = TransitoryContext::Create(window);
    if (!context)
      return false;
    context->SetCallbacks(nullptr, Commit, nullptr, nullptr);
    auto edit = Microsoft::WRL::Make<Edit>(context);
    HRESULT session = E_FAIL;
    HRESULT request = context->impl_->childContext_->RequestEditSession(
        context->impl_->clientId_, edit.Get(), TF_ES_SYNC | TF_ES_READWRITE,
        &session);
    bool success = SUCCEEDED(request) && SUCCEEDED(session);
    edit.Reset();
    context->SetCallbacks(RetireFromCallback, Commit, nullptr, context);
    BOOL remove = FALSE;
    context->impl_->hasComposition_ = true;
    HRESULT retired = context->impl_->sink_->OnTransitoryExtensionUpdated(
        context->impl_->childContext_.Get(), 0, nullptr, nullptr, &remove);
    success = success && SUCCEEDED(retired) && retainedDuringCallback;
    MSG message;
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
      DispatchMessageW(&message);
    success = success && !IsWindow(retiredWindow);
    // Shutdown cannot depend on the message pump running again.
    context = TransitoryContext::Create(window);
    if (!context)
      return false;
    HWND immediate = context->retireWindow_;
    context->Retire();
    return success && !IsWindow(immediate);
  }
};

int main() {
  if (FAILED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED)))
    return 1;
  HWND window =
      CreateWindowExW(0, L"STATIC", L"transitory-test", WS_POPUP, 0, 0, 1, 1,
                      nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
  bool success = window && TransitoryContextTest::Run(window);
  if (window)
    DestroyWindow(window);
  CoUninitialize();
  std::puts(success ? "PASS: result isolation and callback-safe retirement"
                    : "FAIL: transitory context");
  return success ? 0 : 1;
}
