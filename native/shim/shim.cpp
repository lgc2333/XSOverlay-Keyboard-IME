// The C ABI through which the plugin reaches IngameIME. The C++ types stay on this
// side of the boundary. Every export is cdecl, every string is UTF-8, and a handle is
// an opaque void*.
//
// This file is part of the same single library as IngameIME itself and is licensed
// under the GNU Lesser General Public License 2.1, like the rest of that library. See
// ADR 0023 and THIRD-PARTY-NOTICES.md.

#include <IngameIME.hpp>
#include <Windows.h>

#include <string>
#include <vector>

using namespace IngameIME;

typedef void(__cdecl* IimePreEditFn)(int state, int selStart, int selEnd, const char* content, void* user);
typedef void(__cdecl* IimeCommitFn)(const char* text, void* user);
typedef void(__cdecl* IimeCandidatesFn)(int state, int selection, const char* const* items, int count, void* user);

#define IIME_API extern "C" __declspec(dllexport)

struct ShimContext
{
    InputContext*   ctx        = nullptr;
    void*           user       = nullptr;
    IimePreEditFn   preedit    = nullptr;
    IimeCommitFn    commit     = nullptr;
    IimeCandidatesFn candidates = nullptr;
};

// Always the TSF backend in UI-less mode: the plugin draws the composing string
// and the candidates itself, so the IME must not draw them. See ADR 0022.
IIME_API void* __cdecl iime_create(void* hwnd)
{
    auto* shim = new ShimContext();
    shim->ctx  = CreateInputContextWin32(static_cast<HWND>(hwnd), API::TextServiceFramework, true);
    if (!shim->ctx)
    {
        delete shim;
        return nullptr;
    }

    shim->ctx->IngameIME::PreEditCallbackHolder::setCallback(
        [shim](const CompositionState state, const PreEditContext* ctx)
        {
            if (!shim->preedit) return;
            if (ctx)
                shim->preedit(static_cast<int>(state), ctx->selStart, ctx->selEnd, ctx->content.c_str(), shim->user);
            else
                shim->preedit(static_cast<int>(state), -1, -1, nullptr, shim->user);
        });

    shim->ctx->IngameIME::CommitCallbackHolder::setCallback(
        [shim](const std::string text)
        {
            if (shim->commit) shim->commit(text.c_str(), shim->user);
        });

    shim->ctx->IngameIME::CandidateListCallbackHolder::setCallback(
        [shim](const CandidateListState state, const CandidateListContext* ctx)
        {
            if (!shim->candidates) return;
            if (!ctx)
            {
                shim->candidates(static_cast<int>(state), -1, nullptr, 0, shim->user);
                return;
            }

            std::vector<const char*> items;
            items.reserve(ctx->candidates.size());
            for (auto& candidate : ctx->candidates) items.push_back(candidate.c_str());
            shim->candidates(static_cast<int>(state), ctx->selection, items.data(),
                             static_cast<int>(items.size()), shim->user);
        });

    return shim;
}

// Stops the IME from composing into a destroyed window, so it runs before the
// window goes away.
IIME_API void __cdecl iime_destroy(void* handle)
{
    auto* shim = static_cast<ShimContext*>(handle);
    if (!shim) return;
    delete shim->ctx;
    delete shim;
}

IIME_API void __cdecl iime_set_callbacks(void* handle, IimePreEditFn preedit, IimeCommitFn commit,
                                         IimeCandidatesFn candidates, void* user)
{
    auto* shim = static_cast<ShimContext*>(handle);
    if (!shim) return;
    shim->preedit    = preedit;
    shim->commit     = commit;
    shim->candidates = candidates;
    shim->user       = user;
}

// The context starts out attached to an empty document, so the IME composes
// nothing until activation. The plugin never asks for the input mode: what a key
// does is the IME's answer, reported through the callbacks. See ADR 0026.
IIME_API void __cdecl iime_set_activated(void* handle, int activated)
{
    auto* shim = static_cast<ShimContext*>(handle);
    if (shim) shim->ctx->setActivated(activated != 0);
}

// Where the IME would place its own windows, in window coordinates. The host
// window sits off screen, so the placement only has to be well formed.
IIME_API void __cdecl iime_set_preedit_rect(void* handle, int x, int y, int width, int height)
{
    auto* shim = static_cast<ShimContext*>(handle);
    if (!shim) return;
    shim->ctx->setPreEditRect(PreEditRect{ x, y, width, height });
}

IIME_API const char* __cdecl iime_version(void)
{
    return "IngameIME (shared)";
}
