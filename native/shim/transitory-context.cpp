#include "transitory-context.hpp"

#include <algorithm>
#include <limits>
#include <mutex>
#include <string>
#include <vector>

namespace {
constexpr UINT kRetireMessage = WM_APP + 0x4a;
constexpr ULONG kTextChunk = 2048;

HRESULT ToUtf8(const std::wstring &text, std::string &result) {
  result.clear();
  if (text.empty())
    return S_OK;
  if (text.size() > INT_MAX)
    return E_INVALIDARG;
  int count = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(),
                                  static_cast<int>(text.size()), nullptr, 0,
                                  nullptr, nullptr);
  if (!count)
    return HRESULT_FROM_WIN32(GetLastError());
  result.resize(count);
  if (WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(),
                          static_cast<int>(text.size()), result.data(), count,
                          nullptr, nullptr) != count)
    return HRESULT_FROM_WIN32(GetLastError());
  return S_OK;
}

ComPtr<IUnknown> Identity(IUnknown *object) {
  ComPtr<IUnknown> identity;
  if (object)
    object->QueryInterface(IID_PPV_ARGS(&identity));
  return identity;
}

HRESULT ReadRange(ITfRange *range, TfEditCookie cookie, std::wstring &result) {
  result.clear();
  if (!range)
    return S_OK;
  ComPtr<ITfRange> cursor;
  HRESULT hr = range->Clone(&cursor);
  if (FAILED(hr))
    return hr;
  for (;;) {
    WCHAR chunk[kTextChunk];
    ULONG count = 0;
    hr = cursor->GetText(cookie, 0, chunk, kTextChunk, &count);
    if (FAILED(hr))
      return hr;
    if (!count)
      return S_OK;
    result.append(chunk, count);
    LONG moved = 0;
    hr = cursor->ShiftStart(cookie, static_cast<LONG>(count), &moved, nullptr);
    if (FAILED(hr))
      return hr;
    if (moved != static_cast<LONG>(count))
      return E_UNEXPECTED;
  }
}

ComPtr<IUnknown> CompartmentObject(IUnknown *object, REFGUID key) {
  ComPtr<ITfCompartmentMgr> manager;
  if (!object || FAILED(object->QueryInterface(IID_PPV_ARGS(&manager))))
    return {};
  ComPtr<ITfCompartment> compartment;
  if (FAILED(manager->GetCompartment(key, &compartment)))
    return {};
  VARIANT value;
  VariantInit(&value);
  HRESULT hr = compartment->GetValue(&value);
  ComPtr<IUnknown> result;
  if (SUCCEEDED(hr) && value.vt == VT_UNKNOWN)
    result = Identity(value.punkVal);
  VariantClear(&value);
  return result;
}

// This COM object owns its data and never points into TransitoryContext.
class ContextOwner final
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          ITfContextOwner, ITfContextOwnerCompositionSink> {
public:
  explicit ContextOwner(HWND hwnd) : hwnd_(hwnd) {}
  void SetRect(int x, int y, int width, int height) {
    rect_ = {x, y, x + width, y + height};
  }
  HRESULT STDMETHODCALLTYPE GetACPFromPoint(const POINT *, DWORD,
                                            LONG *position) override {
    if (!position)
      return E_POINTER;
    *position = 0;
    return E_NOTIMPL;
  }
  HRESULT STDMETHODCALLTYPE GetTextExt(LONG, LONG, RECT *rect,
                                       BOOL *clipped) override {
    if (!rect || !clipped)
      return E_POINTER;
    *rect = rect_;
    *clipped = FALSE;
    return S_OK;
  }
  HRESULT STDMETHODCALLTYPE GetScreenExt(RECT *rect) override {
    if (!rect)
      return E_POINTER;
    *rect = rect_;
    return S_OK;
  }
  HRESULT STDMETHODCALLTYPE GetStatus(TF_STATUS *status) override {
    if (!status)
      return E_POINTER;
    *status = {0, 0};
    return S_OK;
  }
  HRESULT STDMETHODCALLTYPE GetWnd(HWND *hwnd) override {
    if (!hwnd)
      return E_POINTER;
    *hwnd = hwnd_;
    return S_OK;
  }
  HRESULT STDMETHODCALLTYPE GetAttribute(REFGUID, VARIANT *value) override {
    if (!value)
      return E_POINTER;
    VariantInit(value);
    return E_NOTIMPL;
  }
  HRESULT STDMETHODCALLTYPE OnStartComposition(ITfCompositionView *,
                                               BOOL *accepted) override {
    if (!accepted)
      return E_POINTER;
    *accepted = TRUE;
    return S_OK;
  }
  HRESULT STDMETHODCALLTYPE OnUpdateComposition(ITfCompositionView *,
                                                ITfRange *) override {
    return S_OK;
  }
  HRESULT STDMETHODCALLTYPE OnEndComposition(ITfCompositionView *) override {
    return S_OK;
  }

private:
  HWND hwnd_{};
  RECT rect_{0, 0, 1, 1};
};
} // namespace

class TransitoryContext::Frame final {
public:
  explicit Frame(TransitoryContext *owner) : owner_(owner) {
    owner_->frameDepth_.fetch_add(1);
  }
  ~Frame() { owner_->LeaveFrame(); }

private:
  TransitoryContext *owner_;
};

class TransitoryContext::Impl final {
public:
  Impl(TransitoryContext *owner, HWND hwnd) : owner_(owner), hwnd_(hwnd) {}

  class Sink final
      : public Microsoft::WRL::RuntimeClass<
            Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
            ITfTransitoryExtensionSink> {
  public:
    explicit Sink(Impl *impl) : impl_(impl) {}
    void Detach() { impl_ = nullptr; }
    HRESULT STDMETHODCALLTYPE OnTransitoryExtensionUpdated(
        ITfContext *context, TfEditCookie cookie, ITfRange *result,
        ITfRange *composition, BOOL *deleteResult) override {
      if (!deleteResult)
        return E_POINTER;
      *deleteResult = FALSE;
      Impl *impl = impl_;
      if (!impl)
        return S_OK;
      Frame frame(impl->owner_);
      if (!impl->owner_->accepting_)
        return S_OK;
      return impl->OnTransitory(context, cookie, result, composition,
                                deleteResult);
    }

  private:
    Impl *impl_;
  };

  class UiSink final
      : public Microsoft::WRL::RuntimeClass<
            Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
            ITfUIElementSink> {
  public:
    explicit UiSink(Impl *impl) : impl_(impl) {}
    void Detach() { impl_ = nullptr; }
    HRESULT STDMETHODCALLTYPE BeginUIElement(DWORD id, BOOL *show) override {
      if (!show)
        return E_POINTER;
      *show = TRUE;
      Impl *impl = impl_;
      if (!impl)
        return S_OK;
      Frame frame(impl->owner_);
      if (!impl->owner_->accepting_)
        return S_OK;
      ComPtr<ITfUIElement> element;
      ComPtr<ITfCandidateListUIElement> candidates;
      HRESULT hr = impl->uiManager_->GetUIElement(id, &element);
      if (FAILED(hr) || FAILED(element.As(&candidates)))
        return S_OK;
      // This STA has one document. Result delivery follows current composer
      // focus, not optional TIP document provenance (ADR 0057).
      impl->candidateId_ = id;
      HRESULT published = impl->PublishCandidates(candidates.Get(), 0);
      if (SUCCEEDED(published) && impl->owner_->accepting_)
        *show = FALSE;
      return published;
    }
    HRESULT STDMETHODCALLTYPE UpdateUIElement(DWORD id) override {
      Impl *impl = impl_;
      if (!impl)
        return S_OK;
      Frame frame(impl->owner_);
      if (!impl->owner_->accepting_ || id != impl->candidateId_)
        return S_OK;
      ComPtr<ITfUIElement> element;
      ComPtr<ITfCandidateListUIElement> candidates;
      HRESULT hr = impl->uiManager_->GetUIElement(id, &element);
      if (FAILED(hr))
        return hr;
      hr = element.As(&candidates);
      return FAILED(hr) ? hr : impl->PublishCandidates(candidates.Get(), 1);
    }
    HRESULT STDMETHODCALLTYPE EndUIElement(DWORD id) override {
      Impl *impl = impl_;
      if (!impl)
        return S_OK;
      Frame frame(impl->owner_);
      if (!impl->owner_->accepting_ || id != impl->candidateId_)
        return S_OK;
      impl->candidateId_ = TF_INVALID_UIELEMENTID;
      if (impl->candidates_)
        impl->candidates_(2, -1, nullptr, 0, impl->user_);
      return S_OK;
    }

  private:
    Impl *impl_;
  };

  bool Initialize() {
    if (!hwnd_ || FAILED(CoCreateInstance(CLSID_TF_ThreadMgr, nullptr,
                                          CLSCTX_INPROC_SERVER,
                                          IID_PPV_ARGS(&threadMgr_))))
      return false;
    sink_ = Microsoft::WRL::Make<Sink>(this);
    uiSink_ = Microsoft::WRL::Make<UiSink>(this);
    ownerCom_ = Microsoft::WRL::Make<ContextOwner>(hwnd_);
    if (!sink_ || !uiSink_ || !ownerCom_ ||
        FAILED(threadMgr_.As(&managerSource_)) ||
        FAILED(threadMgr_.As(&uiManager_)) ||
        FAILED(managerSource_->AdviseSink(IID_ITfUIElementSink, uiSink_.Get(),
                                          &uiCookie_)) ||
        FAILED(
            threadMgr_->ActivateEx(&clientId_, TF_TMAE_UIELEMENTENABLEDONLY)))
      return false;
    if (FAILED(threadMgr_->CreateDocumentMgr(&parent_)))
      return false;
    TfEditCookie cookie = 0;
    if (FAILED(parent_->CreateContext(
            clientId_, 0, static_cast<ITfContextOwner *>(ownerCom_.Get()),
            &parentContext_, &cookie)) ||
        FAILED(parent_->Push(parentContext_.Get())) ||
        FAILED(parent_.As(&parentSource_)) ||
        FAILED(parentSource_->AdviseSink(IID_ITfTransitoryExtensionSink,
                                         sink_.Get(), &sinkCookie_)))
      return false;
    ComPtr<ITfCompartmentMgr> compartments;
    if (FAILED(parent_.As(&compartments)) ||
        FAILED(compartments->GetCompartment(
            GUID_COMPARTMENT_TRANSITORYEXTENSION, &enabled_)))
      return false;
    VARIANT value;
    VariantInit(&value);
    value.vt = VT_I4;
    value.lVal = TF_TRANSITORYEXTENSION_FLOATING;
    if (FAILED(enabled_->SetValue(clientId_, &value)))
      return false;
    auto child = CompartmentObject(
        parent_.Get(), GUID_COMPARTMENT_TRANSITORYEXTENSION_DOCUMENTMANAGER);
    if (!child || FAILED(child.As(&childManager_)) ||
        FAILED(childManager_->GetTop(&childContext_)))
      return false;
    childIdentity_ = Identity(childContext_.Get());
    return childIdentity_ != nullptr;
  }

  ~Impl() {
    // Disconnect raw owners before any TSF operation can reenter an advised
    // sink.
    if (sink_)
      sink_->Detach();
    if (uiSink_)
      uiSink_->Detach();
    preedit_ = nullptr;
    commit_ = nullptr;
    candidates_ = nullptr;
    if (activated_)
      SetActivated(false);
    if (enabled_) {
      VARIANT value;
      VariantInit(&value);
      value.vt = VT_I4;
      value.lVal = TF_TRANSITORYEXTENSION_NONE;
      enabled_->SetValue(clientId_, &value);
    }
    if (parentSource_ && sinkCookie_ != TF_INVALID_COOKIE)
      parentSource_->UnadviseSink(sinkCookie_);
    if (managerSource_ && uiCookie_ != TF_INVALID_COOKIE)
      managerSource_->UnadviseSink(uiCookie_);
    if (parent_)
      parent_->Pop(TF_POPF_ALL);
    childIdentity_.Reset();
    childContext_.Reset();
    childManager_.Reset();
    parentContext_.Reset();
    parentSource_.Reset();
    enabled_.Reset();
    parent_.Reset();
    ownerCom_.Reset();
    sink_.Reset();
    uiSink_.Reset();
    uiManager_.Reset();
    managerSource_.Reset();
    if (threadMgr_ && clientId_ != TF_CLIENTID_NULL)
      threadMgr_->Deactivate();
    threadMgr_.Reset();
  }

  void SetCallbacks(IimePreEditFn preedit, IimeCommitFn commit,
                    IimeCandidatesFn candidates, void *user) {
    preedit_ = preedit;
    commit_ = commit;
    candidates_ = candidates;
    user_ = user;
  }

  void SetActivated(bool activated) {
    if (activated == activated_ || !threadMgr_ || !parent_)
      return;
    ComPtr<ITfDocumentMgr> previous;
    if (SUCCEEDED(threadMgr_->AssociateFocus(
            hwnd_, activated ? parent_.Get() : nullptr, &previous)))
      activated_ = activated;
  }

  int ProcessKey(UINT message, WPARAM key, LPARAM data) {
    ComPtr<ITfKeystrokeMgr> keystrokes;
    if (!threadMgr_ || FAILED(threadMgr_.As(&keystrokes)))
      return 0;
    bool down = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
    if (!down && message != WM_KEYUP && message != WM_SYSKEYUP)
      return 0;
    BOOL eaten = FALSE;
    HRESULT hr = down ? keystrokes->TestKeyDown(key, data, &eaten)
                      : keystrokes->TestKeyUp(key, data, &eaten);
    if (FAILED(hr) || !eaten || !owner_->accepting_)
      return 0;
    hr = down ? keystrokes->KeyDown(key, data, &eaten)
              : keystrokes->KeyUp(key, data, &eaten);
    return SUCCEEDED(hr) && eaten ? 1 : 0;
  }

  HRESULT PublishCandidates(ITfCandidateListUIElement *candidates, int state) {
    UINT count = 0, pageCount = 0, page = 0, selection = 0;
    HRESULT hr = candidates->GetCount(&count);
    if (FAILED(hr))
      return hr;
    hr = candidates->GetPageIndex(nullptr, 0, &pageCount);
    if (FAILED(hr))
      return hr;
    std::vector<UINT> starts(pageCount);
    UINT actualPages = 0;
    if (pageCount) {
      hr = candidates->GetPageIndex(starts.data(), pageCount, &actualPages);
      if (FAILED(hr))
        return hr;
      if (!actualPages || actualPages > pageCount)
        return E_UNEXPECTED;
      starts.resize(actualPages);
      hr = candidates->GetCurrentPage(&page);
      if (FAILED(hr))
        return hr;
      if (page >= starts.size())
        return E_UNEXPECTED;
    }
    UINT start = starts.empty() ? 0 : starts[page];
    UINT end =
        starts.empty() || page + 1 == starts.size() ? count : starts[page + 1];
    if (start > end || end > count || end - start > INT_MAX)
      return E_UNEXPECTED;
    hr = candidates->GetSelection(&selection);
    if (FAILED(hr))
      return hr;
    std::vector<std::string> strings;
    strings.reserve(end - start);
    for (UINT index = start; index < end; ++index) {
      BSTR value = nullptr;
      hr = candidates->GetString(index, &value);
      if (FAILED(hr)) {
        SysFreeString(value);
        return hr;
      }
      std::wstring text(value ? value : L"", SysStringLen(value));
      SysFreeString(value);
      std::string utf8;
      hr = ToUtf8(text, utf8);
      if (FAILED(hr))
        return hr;
      strings.push_back(std::move(utf8));
    }
    std::vector<const char *> items;
    for (const auto &value : strings)
      items.push_back(value.c_str());
    int relative = selection >= start && selection < end
                       ? static_cast<int>(selection - start)
                       : -1;
    if (owner_->accepting_ && candidates_)
      candidates_(state, relative, items.data(), static_cast<int>(items.size()),
                  user_);
    return S_OK;
  }

  HRESULT OnTransitory(ITfContext *context, TfEditCookie cookie,
                       ITfRange *result, ITfRange *composition,
                       BOOL *deleteResult) {
    if (!childIdentity_ || Identity(context).Get() != childIdentity_.Get())
      return S_OK;
    // The framework deletes only its supplied result range. Never authorize
    // deletion if a malformed/overlapping result could consume the live
    // composition (ADR 0059).
    if (result && composition) {
      LONG before = 0, after = 0;
      HRESULT hr =
          result->CompareEnd(cookie, composition, TF_ANCHOR_START, &before);
      if (FAILED(hr))
        return hr;
      hr = result->CompareStart(cookie, composition, TF_ANCHOR_END, &after);
      if (FAILED(hr))
        return hr;
      if (before > 0 && after < 0)
        return E_UNEXPECTED;
    }
    std::wstring resultText, compositionText;
    HRESULT hr = ReadRange(result, cookie, resultText);
    if (FAILED(hr))
      return hr;
    hr = ReadRange(composition, cookie, compositionText);
    if (FAILED(hr))
      return hr;
    std::string resultUtf8, compositionUtf8;
    hr = ToUtf8(resultText, resultUtf8);
    if (FAILED(hr))
      return hr;
    hr = ToUtf8(compositionText, compositionUtf8);
    if (FAILED(hr))
      return hr;
    int selectionStart = 0, selectionEnd = 0;
    if (composition) {
      ComPtr<ITfRangeACP> compositionAcp;
      LONG start = 0, length = 0;
      if (SUCCEEDED(
              composition->QueryInterface(IID_PPV_ARGS(&compositionAcp))) &&
          SUCCEEDED(compositionAcp->GetExtent(&start, &length))) {
        TF_SELECTION selection{};
        ULONG fetched = 0;
        if (SUCCEEDED(context->GetSelection(cookie, TF_DEFAULT_SELECTION, 1,
                                            &selection, &fetched)) &&
            fetched && selection.range) {
          ComPtr<ITfRangeACP> selectionAcp;
          LONG position = 0, selected = 0;
          if (SUCCEEDED(selection.range->QueryInterface(
                  IID_PPV_ARGS(&selectionAcp))) &&
              SUCCEEDED(selectionAcp->GetExtent(&position, &selected))) {
            selectionStart = std::clamp(position - start, 0L, length);
            selectionEnd =
                std::clamp(position + selected - start,
                           static_cast<LONG>(selectionStart), length);
          }
        }
        if (selection.range)
          selection.range->Release();
      }
      int state = hasComposition_ ? 1 : 0;
      hasComposition_ = true;
      if (preedit_)
        preedit_(state, selectionStart, selectionEnd, compositionUtf8.c_str(),
                 user_);
    } else if (hasComposition_) {
      hasComposition_ = false;
      if (preedit_)
        preedit_(2, 0, 0, nullptr, user_);
    }
    if (!owner_->accepting_)
      return S_OK;
    if (!resultUtf8.empty()) {
      if (!commit_)
        return S_OK;
      commit_(resultUtf8.c_str(), user_);
    }
    if (result)
      *deleteResult = TRUE;
    return S_OK;
  }

  TransitoryContext *owner_{};
  HWND hwnd_{};
  ComPtr<ITfThreadMgrEx> threadMgr_;
  ComPtr<ITfSource> managerSource_, parentSource_;
  ComPtr<ITfUIElementMgr> uiManager_;
  TfClientId clientId_{TF_CLIENTID_NULL};
  ComPtr<ITfDocumentMgr> parent_, childManager_;
  ComPtr<ITfContext> parentContext_, childContext_;
  ComPtr<IUnknown> childIdentity_;
  ComPtr<ContextOwner> ownerCom_;
  ComPtr<UiSink> uiSink_;
  ComPtr<ITfCompartment> enabled_;
  ComPtr<Sink> sink_;
  DWORD sinkCookie_{TF_INVALID_COOKIE}, uiCookie_{TF_INVALID_COOKIE};
  DWORD candidateId_{TF_INVALID_UIELEMENTID};
  bool activated_{}, hasComposition_{};
  IimePreEditFn preedit_{};
  IimeCommitFn commit_{};
  IimeCandidatesFn candidates_{};
  void *user_{};
};

TransitoryContext::TransitoryContext(HWND hwnd)
    : ownerThread_(GetCurrentThreadId()),
      impl_(std::make_unique<Impl>(this, hwnd)) {
  static std::once_flag once;
  std::call_once(once, [] {
    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = RetireWindowProc;
    windowClass.hInstance = GetModuleHandleW(nullptr);
    windowClass.lpszClassName = L"XSOverlayImeTransitoryRetire";
    RegisterClassW(&windowClass);
  });
  retireWindow_ =
      CreateWindowExW(0, L"XSOverlayImeTransitoryRetire", L"", 0, 0, 0, 0, 0,
                      HWND_MESSAGE, nullptr, GetModuleHandleW(nullptr), this);
}
TransitoryContext::~TransitoryContext() {
  accepting_ = false;
  impl_.reset();
  if (retireWindow_) {
    SetWindowLongPtrW(retireWindow_, GWLP_USERDATA, 0);
    DestroyWindow(retireWindow_);
  }
}
TransitoryContext *TransitoryContext::Create(HWND hwnd) {
  auto context = new TransitoryContext(hwnd);
  if (!context->retireWindow_ || !context->impl_->Initialize()) {
    delete context;
    return nullptr;
  }
  return context;
}
void TransitoryContext::SetCallbacks(IimePreEditFn preedit, IimeCommitFn commit,
                                     IimeCandidatesFn candidates, void *user) {
  Frame frame(this);
  if (accepting_)
    impl_->SetCallbacks(preedit, commit, candidates, user);
}
void TransitoryContext::SetActivated(bool activated) {
  Frame frame(this);
  if (accepting_)
    impl_->SetActivated(activated);
}
void TransitoryContext::SetPreEditRect(int x, int y, int width, int height) {
  Frame frame(this);
  if (accepting_)
    impl_->ownerCom_->SetRect(x, y, width, height);
}
int TransitoryContext::ProcessKey(UINT message, WPARAM key, LPARAM data) {
  Frame frame(this);
  return accepting_ ? impl_->ProcessKey(message, key, data) : 0;
}
void TransitoryContext::LeaveFrame() {
  if (frameDepth_.fetch_sub(1) == 1 && !accepting_)
    PostDestruction();
}
void TransitoryContext::PostDestruction() {
  if (!destroyPosted_.exchange(true) &&
      !PostMessageW(retireWindow_, kRetireMessage, 0, 0))
    destroyPosted_ = false;
}
void TransitoryContext::Retire() {
  {
    Frame frame(this);
    accepting_ = false;
  }
  // Application shutdown may stop pumping immediately after iime_destroy.
  // A quiescent owner can finish now; entered callbacks still defer
  // destruction.
  if (GetCurrentThreadId() == ownerThread_ && frameDepth_ == 0)
    DestroyOnOwner();
}
void TransitoryContext::DestroyOnOwner() {
  destroyPosted_ = false;
  if (GetCurrentThreadId() == ownerThread_ && !accepting_ && frameDepth_ == 0)
    delete this;
  // A reentrant dispatch leaves destruction pending; the last frame posts it
  // again.
}
LRESULT CALLBACK TransitoryContext::RetireWindowProc(HWND hwnd, UINT message,
                                                     WPARAM key, LPARAM data) {
  if (message == WM_NCCREATE) {
    auto create = reinterpret_cast<CREATESTRUCTW *>(data);
    SetWindowLongPtrW(hwnd, GWLP_USERDATA,
                      reinterpret_cast<LONG_PTR>(create->lpCreateParams));
  }
  if (message == kRetireMessage) {
    auto context = reinterpret_cast<TransitoryContext *>(
        GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (context)
      context->DestroyOnOwner();
    return 0;
  }
  return DefWindowProcW(hwnd, message, key, data);
}
