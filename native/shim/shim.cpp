#include "transitory-context.hpp"

#define IIME_API extern "C" __declspec(dllexport)

IIME_API void *__cdecl iime_create(void *hwnd) {
  return TransitoryContext::Create(static_cast<HWND>(hwnd));
}
IIME_API void __cdecl iime_destroy(void *handle) {
  if (handle)
    static_cast<TransitoryContext *>(handle)->Retire();
}
IIME_API void __cdecl iime_set_callbacks(void *handle, IimePreEditFn preedit,
                                         IimeCommitFn commit,
                                         IimeCandidatesFn candidates,
                                         void *user) {
  if (handle)
    static_cast<TransitoryContext *>(handle)->SetCallbacks(preedit, commit,
                                                           candidates, user);
}
IIME_API void __cdecl iime_set_activated(void *handle, int activated) {
  if (handle)
    static_cast<TransitoryContext *>(handle)->SetActivated(activated != 0);
}
IIME_API void __cdecl iime_set_preedit_rect(void *handle, int x, int y,
                                            int width, int height) {
  if (handle)
    static_cast<TransitoryContext *>(handle)->SetPreEditRect(x, y, width,
                                                             height);
}
IIME_API int __cdecl iime_process_key(void *handle, UINT message, WPARAM key,
                                      LPARAM data) {
  return handle ? static_cast<TransitoryContext *>(handle)->ProcessKey(
                      message, key, data)
                : 0;
}
IIME_API const char *__cdecl iime_version(void) {
  return "IngameIME (transitory TSF)";
}
