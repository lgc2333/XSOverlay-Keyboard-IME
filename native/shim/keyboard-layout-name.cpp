// Keyboard layout display names. LGPL-2.1, like shim.cpp. See ADR 0044.
#include <Windows.h>
#include <cwchar>
#include <cstdlib>

#pragma comment(lib, "advapi32.lib")

// HKLs with an Fxxx device word refer to the registry's Layout Id. Other device
// words are the layout identifier itself. Reading names never activates a layout.
extern "C" __declspec(dllexport) int __cdecl iime_get_keyboard_layout_name(HKL layout, wchar_t* name, int capacity)
{
    if (!name || capacity <= 0) return 0;
    name[0] = L'\0';
    HKEY layouts = nullptr;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SYSTEM\\CurrentControlSet\\Control\\Keyboard Layouts",
        0, KEY_READ, &layouts) != ERROR_SUCCESS) return 0;

    const auto value = reinterpret_cast<ULONG_PTR>(layout);
    const WORD device = HIWORD(value);
    wchar_t keyName[KL_NAMELENGTH] = {};
    if ((device & 0xf000) != 0xf000)
        swprintf_s(keyName, L"%08x", device ? device : LOWORD(value));
    else
    {
        for (DWORD index = 0;; ++index)
        {
            DWORD length = KL_NAMELENGTH;
            if (RegEnumKeyExW(layouts, index, keyName, &length, nullptr, nullptr, nullptr, nullptr) != ERROR_SUCCESS)
            {
                keyName[0] = L'\0';
                break;
            }
            if ((wcstoul(keyName, nullptr, 16) & 0xffff) != LOWORD(value)) continue;
            wchar_t id[16] = {};
            DWORD size = sizeof(id);
            if (RegGetValueW(layouts, keyName, L"Layout Id", RRF_RT_REG_SZ, nullptr, id, &size) == ERROR_SUCCESS &&
                wcstoul(id, nullptr, 16) == (device & 0x0fff)) break;
        }
    }

    HKEY key = nullptr;
    if (keyName[0] && RegOpenKeyExW(layouts, keyName, 0, KEY_READ, &key) == ERROR_SUCCESS)
    {
        DWORD bytes = capacity * sizeof(wchar_t);
        if (RegLoadMUIStringW(key, L"Layout Display Name", name, bytes, nullptr, 0, nullptr) != ERROR_SUCCESS)
            RegGetValueW(key, nullptr, L"Layout Text", RRF_RT_REG_SZ | RRF_ZEROONFAILURE, nullptr, name, &bytes);
        RegCloseKey(key);
    }
    RegCloseKey(layouts);
    return static_cast<int>(wcslen(name));
}
