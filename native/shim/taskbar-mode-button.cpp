// SPDX-License-Identifier: LGPL-2.1-only
#include <Windows.h>
#include <UIAutomation.h>
#include <shlwapi.h>
#include <wrl/client.h>
#include <cwctype>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace
{
using AllowedFn = int(__cdecl*)(void*);

std::wstring Resource(const wchar_t* key)
{
    wchar_t windows[MAX_PATH]{};
    if (!GetWindowsDirectoryW(windows, MAX_PATH)) return {};
    for (auto package : {L"MicrosoftWindows.Client.Core", L"MicrosoftWindows.Client.CBS"})
    {
        std::wstring path = std::wstring(L"@{") + windows + L"\\SystemApps\\" + package
            + L"_cw5n1h2txyewy\\resources.pri?ms-resource://" + package + L"/SystemTray/Resources/" + key + L"}";
        wchar_t value[512]{};
        if (SUCCEEDED(SHLoadIndirectString(path.c_str(), value, 512, nullptr)) && *value) return value;
    }
    return {};
}

std::wstring Normalize(std::wstring value)
{
    for (auto& character : value) if (iswspace(character)) character = L' ';
    return value;
}

HRESULT Text(IUIAutomationElement* element, PROPERTYID property, std::wstring& text)
{
    VARIANT value;
    VariantInit(&value);
    HRESULT hr = element->GetCurrentPropertyValue(property, &value);
    if (SUCCEEDED(hr) && value.vt == VT_BSTR && value.bstrVal) text = value.bstrVal;
    else if (SUCCEEDED(hr)) hr = E_UNEXPECTED;
    VariantClear(&value);
    return hr;
}

bool OwnsFocus(HWND host, DWORD thread, HKL layout, AllowedFn allowed, void* user)
{
    GUITHREADINFO info{sizeof(info)};
    return allowed(user) && IsWindow(host) && GetForegroundWindow() == host
        && GetGUIThreadInfo(thread, &info) && info.hwndFocus == host
        && GetKeyboardLayout(thread) == layout;
}

HRESULT Configure(IUIAutomation2* automation)
{
    HRESULT hr = automation->put_AutoSetFocus(FALSE);
    if (FAILED(hr)) return hr;
    BOOL automatic = TRUE;
    if (FAILED(hr = automation->get_AutoSetFocus(&automatic))) return hr;
    if (automatic) return E_ACCESSDENIED;
    // Provider request limits, not a whole-operation deadline. See ADR 0051.
    if (FAILED(hr = automation->put_ConnectionTimeout(2000))) return hr;
    if (FAILED(hr = automation->put_TransactionTimeout(20000))) return hr;
    DWORD connection = 0, transaction = 0;
    if (FAILED(hr = automation->get_ConnectionTimeout(&connection))) return hr;
    if (FAILED(hr = automation->get_TransactionTimeout(&transaction))) return hr;
    return connection == 2000 && transaction == 20000 ? S_OK : E_UNEXPECTED;
}

HRESULT Run(HWND host, bool click, AllowedFn allowed, AllowedFn authorize, void* user, int* available)
{
    DWORD thread = GetWindowThreadProcessId(host, nullptr);
    HKL layout = GetKeyboardLayout(thread);
    if (!thread || !OwnsFocus(host, thread, layout, allowed, user)) return E_ABORT;
    ComPtr<IUIAutomation2> automation;
    HRESULT hr = CoCreateInstance(CLSID_CUIAutomation8, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&automation));
    if (FAILED(hr) || FAILED(hr = Configure(automation.Get()))) return hr;

    HWND tray = FindWindowW(L"Shell_TrayWnd", nullptr);
    std::wstring modeName = Resource(L"AutomationName_ImeButton");
    std::wstring languageHelp = Normalize(Resource(L"LanguageIndicatorHelpString"));
    if (!tray || modeName.empty() || languageHelp.empty()) { *available = 0; return S_FALSE; }

    // Resource keys and taskbar identities follow the Windows taskbar lookup described in ADR 0051.
    std::vector<HWND> roots;
    EnumChildWindows(tray, [](HWND child, LPARAM data)->BOOL {
        wchar_t name[100]{};
        GetClassNameW(child, name, 100);
        if (wcscmp(name, L"Windows.UI.Composition.DesktopWindowContentBridge") == 0)
            reinterpret_cast<std::vector<HWND>*>(data)->push_back(child);
        return TRUE;
    }, reinterpret_cast<LPARAM>(&roots));
    roots.push_back(tray);

    VARIANT id;
    VariantInit(&id);
    id.vt = VT_BSTR;
    id.bstrVal = SysAllocString(L"SystemTrayIcon");
    if (!id.bstrVal) return E_OUTOFMEMORY;
    ComPtr<IUIAutomationCondition> condition;
    hr = automation->CreatePropertyCondition(UIA_AutomationIdPropertyId, id, &condition);
    VariantClear(&id);
    if (FAILED(hr)) return hr;

    ComPtr<IUIAutomationElement> target;
    for (HWND rootWindow : roots)
    {
        if (!OwnsFocus(host, thread, layout, allowed, user)) return E_ABORT;
        ComPtr<IUIAutomationElement> root;
        if (FAILED(hr = automation->ElementFromHandle(rootWindow, &root))) return hr;
        ComPtr<IUIAutomationElementArray> elements;
        if (FAILED(hr = root->FindAll(TreeScope_Descendants, condition.Get(), &elements))) return hr;
        int count = 0;
        if (FAILED(hr = elements->get_Length(&count))) return hr;
        for (int index = 0; index < count; ++index)
        {
            ComPtr<IUIAutomationElement> element;
            if (FAILED(hr = elements->GetElement(index, &element))) return hr;
            std::wstring name, kind;
            if (FAILED(hr = Text(element.Get(), UIA_NamePropertyId, name))) return hr;
            if (FAILED(hr = Text(element.Get(), UIA_ClassNamePropertyId, kind))) return hr;
            bool mode = name.compare(0, modeName.size(), modeName) == 0
                && (name.size() == modeName.size() || iswspace(name[modeName.size()]));
            bool language = Normalize(name).find(languageHelp) != std::wstring::npos;
            if (kind != L"SystemTray.NormalButton" || !mode || language) continue;
            if (target)
            {
                BOOL same = FALSE;
                if (FAILED(hr = automation->CompareElements(target.Get(), element.Get(), &same))) return hr;
                if (!same) { *available = 0; return S_FALSE; }
            }
            else target = element;
        }
    }
    if (!target) { *available = 0; return S_FALSE; }
    BOOL enabled = FALSE, offscreen = TRUE;
    if (FAILED(hr = target->get_CurrentIsEnabled(&enabled))) return hr;
    if (FAILED(hr = target->get_CurrentIsOffscreen(&offscreen))) return hr;
    if (!enabled || offscreen) { *available = 0; return S_FALSE; }
    ComPtr<IUIAutomationInvokePattern> invoke;
    if (FAILED(hr = target->GetCurrentPatternAs(UIA_InvokePatternId, IID_PPV_ARGS(&invoke)))) return hr;
    *available = 1;
    // The STA checks the complete TSF profile and composer generation after lookup.
    if (click && !authorize(user)) return E_ABORT;
    if (!OwnsFocus(host, thread, layout, allowed, user)) return E_ABORT;
    return click ? invoke->Invoke() : S_OK;
}
}

extern "C" __declspec(dllexport) HRESULT __cdecl iime_taskbar_mode_run(HWND host, int click,
    AllowedFn allowed, AllowedFn authorize, void* user, int* available)
{
    if (!allowed || !authorize || !available) return E_POINTER;
    *available = -1;
    HRESULT initialized = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(initialized)) return initialized;
    HRESULT result;
    try { result = Run(host, click != 0, allowed, authorize, user, available); }
    catch (...) { result = E_FAIL; }
    CoUninitialize();
    return result;
}
