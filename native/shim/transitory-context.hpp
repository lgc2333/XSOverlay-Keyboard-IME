#pragma once

#include <Windows.h>
#include <msctf.h>
#include <wrl.h>

#include <atomic>
#include <memory>

using Microsoft::WRL::ComPtr;

typedef void(__cdecl *IimePreEditFn)(int state, int selStart, int selEnd,
                                     const char *content, void *user);
typedef void(__cdecl *IimeCommitFn)(const char *text, void *user);
typedef void(__cdecl *IimeCandidatesFn)(int state, int selection,
                                        const char *const *items, int count,
                                        void *user);

class TransitoryContext final {
public:
  static TransitoryContext *Create(HWND hwnd);
  void Retire();
  void SetCallbacks(IimePreEditFn preedit, IimeCommitFn commit,
                    IimeCandidatesFn candidates, void *user);
  void SetActivated(bool activated);
  void SetPreEditRect(int x, int y, int width, int height);
  int ProcessKey(UINT message, WPARAM wParam, LPARAM lParam);

private:
  class Impl;
  class Frame;
  explicit TransitoryContext(HWND hwnd);
  ~TransitoryContext();
  void LeaveFrame();
  void PostDestruction();
  void DestroyOnOwner();
  static LRESULT CALLBACK RetireWindowProc(HWND, UINT, WPARAM, LPARAM);
  HWND retireWindow_{};
  DWORD ownerThread_{};
  std::atomic<bool> accepting_{true};
  std::atomic<int> frameDepth_{0};
  std::atomic<bool> destroyPosted_{false};
  std::unique_ptr<Impl> impl_;
  friend class Impl;
  friend struct TransitoryContextTest;
};
