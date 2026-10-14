// Windows 11 Explorer command extension. Build as an x64 native COM DLL.
// Explorer must only enumerate lightweight commands; FFmpeg never runs in Explorer.
#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <atomic>
#include <string>
#include <algorithm>
#include <cwctype>
#include <new>
#include <vector>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")

// Keep this GUID in sync with the full MSIX package manifest.
static constexpr CLSID CLSID_SlimClip = { 0x83e9cd33, 0xc9e0, 0x44de, { 0xb2, 0x65, 0xf1, 0x8f, 0x91, 0xa9, 0xa9, 0x29 } };
static HMODULE g_module = nullptr;
static std::atomic<long> g_objects{ 0 }, g_locks{ 0 };

static bool IsVideo(const std::wstring& path)
{
    const auto dot = path.find_last_of(L'.');
    if (dot == std::wstring::npos) return false;
    std::wstring ext = path.substr(dot);
    std::transform(ext.begin(), ext.end(), ext.begin(), [](wchar_t c) { return static_cast<wchar_t>(std::towlower(c)); });
    return ext == L".mp4" || ext == L".mkv" || ext == L".mov" || ext == L".avi" ||
           ext == L".webm" || ext == L".wmv" || ext == L".m4v" || ext == L".ts" ||
           ext == L".mts" || ext == L".m2ts" || ext == L".mpg" || ext == L".mpeg" || ext == L".3gp";
}

static bool AllVideo(IShellItemArray* items)
{
    if (!items) return false;
    DWORD count = 0;
    if (FAILED(items->GetCount(&count)) || !count) return false;
    for (DWORD i = 0; i < count; ++i)
    {
        IShellItem* item = nullptr;
        if (FAILED(items->GetItemAt(i, &item))) return false;
        PWSTR path = nullptr;
        HRESULT hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
        item->Release();
        bool valid = SUCCEEDED(hr) && path && IsVideo(path);
        CoTaskMemFree(path);
        if (!valid) return false;
    }
    return true;
}

class MenuCommand;
class CommandEnumerator final : public IEnumExplorerCommand
{
    std::atomic<ULONG> refs{ 1 };
    ULONG index = 0;
public:
    CommandEnumerator() { ++g_objects; }
    ~CommandEnumerator() { --g_objects; }
    STDMETHODIMP QueryInterface(REFIID id, void** out) override
    {
        if (!out) return E_POINTER;
        *out = nullptr;
        if (id == IID_IUnknown || id == IID_IEnumExplorerCommand) *out = static_cast<IEnumExplorerCommand*>(this);
        if (!*out) return E_NOINTERFACE;
        AddRef(); return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return ++refs; }
    STDMETHODIMP_(ULONG) Release() override { ULONG n = --refs; if (!n) delete this; return n; }
    STDMETHODIMP Next(ULONG requested, IExplorerCommand** commands, ULONG* fetched) override;
    STDMETHODIMP Skip(ULONG amount) override { index = std::min<ULONG>(5, index + amount); return index < 5 ? S_OK : S_FALSE; }
    STDMETHODIMP Reset() override { index = 0; return S_OK; }
    STDMETHODIMP Clone(IEnumExplorerCommand** out) override
    {
        if (!out) return E_POINTER;
        auto* clone = new (std::nothrow) CommandEnumerator;
        if (!clone) return E_OUTOFMEMORY;
        clone->index = index; *out = clone; return S_OK;
    }
};

class MenuCommand final : public IExplorerCommand
{
    std::atomic<ULONG> refs{ 1 };
    int kind; // -1 root, 0..4 actions
public:
    explicit MenuCommand(int k) : kind(k) { ++g_objects; }
    ~MenuCommand() { --g_objects; }
    STDMETHODIMP QueryInterface(REFIID id, void** out) override
    {
        if (!out) return E_POINTER;
        *out = nullptr;
        if (id == IID_IUnknown || id == IID_IExplorerCommand) *out = static_cast<IExplorerCommand*>(this);
        if (!*out) return E_NOINTERFACE;
        AddRef(); return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return ++refs; }
    STDMETHODIMP_(ULONG) Release() override { ULONG n = --refs; if (!n) delete this; return n; }
    STDMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
    {
        if (!title) return E_POINTER;
        constexpr const wchar_t* names[] = { L"压缩视频", L"低（小文件）", L"中（均衡）", L"高（高画质）", L"预估大小", L"设置" };
        return SHStrDupW(names[kind + 1], title);
    }
    STDMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override { if (!icon) return E_POINTER; *icon = nullptr; return E_NOTIMPL; }
    STDMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tip) override { if (!tip) return E_POINTER; *tip = nullptr; return E_NOTIMPL; }
    STDMETHODIMP GetCanonicalName(GUID* name) override
    {
        if (!name) return E_POINTER;
        // One stable identity per verb (including the flyout root).
        *name = CLSID_SlimClip;
        if (kind >= 0) name->Data4[7] ^= static_cast<BYTE>(kind + 1);
        return S_OK;
    }
    STDMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
    {
        if (!state) return E_POINTER;
        *state = AllVideo(items) ? ECS_ENABLED : ECS_HIDDEN;
        if (*state == ECS_ENABLED && kind == 3)
        {
            DWORD count = 0;
            if (FAILED(items->GetCount(&count)) || count != 1) *state = ECS_DISABLED;
        }
        return S_OK;
    }
    STDMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags) return E_POINTER;
        *flags = kind == -1 ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
        return S_OK;
    }
    STDMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        if (!commands) return E_POINTER;
        *commands = nullptr;
        if (kind != -1) return E_NOTIMPL;
        *commands = new (std::nothrow) CommandEnumerator;
        return *commands ? S_OK : E_OUTOFMEMORY;
    }
    STDMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        if (kind == -1 || !AllVideo(items)) return E_INVALIDARG;
        std::vector<wchar_t> dllPath(MAX_PATH);
        DWORD length = 0;
        for (;;)
        {
            length = GetModuleFileNameW(g_module, dllPath.data(), static_cast<DWORD>(dllPath.size()));
            if (!length) return HRESULT_FROM_WIN32(GetLastError());
            if (length < dllPath.size() - 1) break;
            if (dllPath.size() >= 32768) return HRESULT_FROM_WIN32(ERROR_FILENAME_EXCED_RANGE);
            dllPath.resize(dllPath.size() * 2);
        }
        std::wstring exe(dllPath.data(), length);
        auto slash = exe.find_last_of(L"\\/");
        if (slash == std::wstring::npos) return E_FAIL;
        exe.resize(slash + 1);
        exe += L"SlimClip.exe";
        const wchar_t* options[] = { L"--low", L"--medium", L"--high", L"--estimate", L"--settings" };
        DWORD count = 0; items->GetCount(&count);
        if (kind == 3 && count != 1) return E_INVALIDARG;
        if (kind == 4) count = 1;
        for (DWORD i = 0; i < count; ++i)
        {
            IShellItem* item = nullptr;
            if (FAILED(items->GetItemAt(i, &item))) continue;
            PWSTR path = nullptr;
            HRESULT hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
            item->Release();
            if (SUCCEEDED(hr) && path)
            {
                std::wstring parameters = options[kind];
                // ShellExecuteEx receives a single quoted filename after the option.
                // Filenames containing a literal quote are not permitted by Windows.
                if (kind != 4) { parameters += L" \""; parameters += path; parameters += L"\""; }
                SHELLEXECUTEINFOW info{ sizeof(info) };
                info.fMask = SEE_MASK_FLAG_NO_UI;
                info.lpVerb = L"open";
                info.lpFile = exe.c_str();
                info.lpParameters = parameters.c_str();
                info.nShow = SW_SHOWNORMAL;
                if (!ShellExecuteExW(&info))
                {
                    DWORD error = GetLastError();
                    CoTaskMemFree(path);
                    return HRESULT_FROM_WIN32(error);
                }
            }
            CoTaskMemFree(path);
        }
        return S_OK;
    }
};

STDMETHODIMP CommandEnumerator::Next(ULONG requested, IExplorerCommand** commands, ULONG* fetched)
{
    if (!commands || (requested != 1 && !fetched)) return E_POINTER;
    ULONG done = 0;
    while (done < requested && index < 5)
    {
        commands[done] = new (std::nothrow) MenuCommand(index);
        if (!commands[done])
        {
            for (ULONG i = 0; i < done; ++i) { commands[i]->Release(); commands[i] = nullptr; }
            if (fetched) *fetched = 0;
            return E_OUTOFMEMORY;
        }
        ++index;
        ++done;
    }
    if (fetched) *fetched = done;
    return done == requested ? S_OK : S_FALSE;
}

class CommandFactory final : public IClassFactory
{
    std::atomic<ULONG> refs{ 1 };
public:
    CommandFactory() { ++g_objects; }
    ~CommandFactory() { --g_objects; }
    STDMETHODIMP QueryInterface(REFIID id, void** out) override
    {
        if (!out) return E_POINTER;
        *out = nullptr;
        if (id == IID_IUnknown || id == IID_IClassFactory) *out = static_cast<IClassFactory*>(this);
        if (!*out) return E_NOINTERFACE;
        AddRef(); return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return ++refs; }
    STDMETHODIMP_(ULONG) Release() override { ULONG n = --refs; if (!n) delete this; return n; }
    STDMETHODIMP CreateInstance(IUnknown* outer, REFIID id, void** out) override
    {
        if (outer) return CLASS_E_NOAGGREGATION;
        auto* cmd = new (std::nothrow) MenuCommand(-1);
        if (!cmd) return E_OUTOFMEMORY;
        HRESULT hr = cmd->QueryInterface(id, out);
        cmd->Release(); return hr;
    }
    STDMETHODIMP LockServer(BOOL lock) override { if (lock) ++g_locks; else --g_locks; return S_OK; }
};

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) { g_module = module; DisableThreadLibraryCalls(module); }
    return TRUE;
}
// Distinct internal names avoid conflicting SDK combaseapi.h declarations.
// The .def file exports these under the COM-required Dll* entry-point names.
extern "C" HRESULT __stdcall SlimClipDllGetClassObject(REFCLSID clsid, REFIID id, void** out)
{
    if (clsid != CLSID_SlimClip) return CLASS_E_CLASSNOTAVAILABLE;
    auto* factory = new (std::nothrow) CommandFactory;
    if (!factory) return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(id, out);
    factory->Release(); return hr;
}
extern "C" HRESULT __stdcall SlimClipDllCanUnloadNow()
{
    return !g_objects && !g_locks ? S_OK : S_FALSE;
}
