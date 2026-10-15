// Native, registration-free smoke test for the built x64 Explorer COM DLL.
// No real filesystem items, registry changes, Explorer process or Invoke calls.
#include <windows.h>
#include <shobjidl.h>
#include <string>
#include <iostream>
#include <cstring>
#include <cwchar>

#ifndef _WIN64
#error SlimClip COM self-test requires an x64 MSVC environment
#endif

static constexpr CLSID kClsid = { 0x83e9cd33, 0xc9e0, 0x44de, { 0xb2, 0x65, 0xf1, 0x8f, 0x91, 0xa9, 0xa9, 0x29 } };
static int failures = 0;

static void Check(bool ok, const char* what)
{
    if (!ok) { std::cerr << "SlimClip COM self-test FAILED: " << what << '\n'; ++failures; }
}

class FakeItem final : public IShellItem
{
    ULONG refs = 1;
    const wchar_t* path;
public:
    explicit FakeItem(const wchar_t* value) : path(value) {}
    STDMETHODIMP QueryInterface(REFIID iid, void** out) override
    {
        if (!out) return E_POINTER;
        *out = nullptr;
        if (iid != IID_IUnknown && iid != IID_IShellItem) return E_NOINTERFACE;
        *out = static_cast<IShellItem*>(this);
        AddRef();
        return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return ++refs; }
    STDMETHODIMP_(ULONG) Release() override { return --refs; } // Owned by the test stack.
    STDMETHODIMP BindToHandler(IBindCtx*, REFGUID, REFIID, void**) override { return E_NOTIMPL; }
    STDMETHODIMP GetParent(IShellItem**) override { return E_NOTIMPL; }
    STDMETHODIMP GetDisplayName(SIGDN name, PWSTR* out) override
    {
        if (!out) return E_POINTER;
        *out = nullptr;
        if (name != SIGDN_FILESYSPATH) return E_INVALIDARG;
        const size_t bytes = (std::wcslen(path) + 1) * sizeof(wchar_t);
        *out = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
        if (!*out) return E_OUTOFMEMORY;
        std::memcpy(*out, path, bytes);
        return S_OK;
    }
    STDMETHODIMP GetAttributes(SFGAOF, SFGAOF*) override { return E_NOTIMPL; }
    STDMETHODIMP Compare(IShellItem*, SICHINTF, int*) override { return E_NOTIMPL; }
};

class FakeArray final : public IShellItemArray
{
    ULONG refs = 1;
    IShellItem* const* items;
    DWORD count;
public:
    FakeArray(IShellItem* const* values, DWORD size) : items(values), count(size) {}
    STDMETHODIMP QueryInterface(REFIID iid, void** out) override
    {
        if (!out) return E_POINTER;
        *out = nullptr;
        if (iid != IID_IUnknown && iid != IID_IShellItemArray) return E_NOINTERFACE;
        *out = static_cast<IShellItemArray*>(this);
        AddRef();
        return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return ++refs; }
    STDMETHODIMP_(ULONG) Release() override { return --refs; } // Owned by the test stack.
    STDMETHODIMP BindToHandler(IBindCtx*, REFGUID, REFIID, void**) override { return E_NOTIMPL; }
    STDMETHODIMP GetPropertyStore(GETPROPERTYSTOREFLAGS, REFIID, void**) override { return E_NOTIMPL; }
    STDMETHODIMP GetPropertyDescriptionList(REFPROPERTYKEY, REFIID, void**) override { return E_NOTIMPL; }
    STDMETHODIMP GetAttributes(SIATTRIBFLAGS, SFGAOF, SFGAOF*) override { return E_NOTIMPL; }
    STDMETHODIMP GetCount(DWORD* out) override
    {
        if (!out) return E_POINTER;
        *out = count;
        return S_OK;
    }
    STDMETHODIMP GetItemAt(DWORD index, IShellItem** out) override
    {
        if (!out) return E_POINTER;
        *out = nullptr;
        if (index >= count) return E_INVALIDARG;
        *out = items[index];
        (*out)->AddRef();
        return S_OK;
    }
    STDMETHODIMP EnumItems(IEnumShellItems**) override { return E_NOTIMPL; }
};

static void TestCommand(IExplorerCommand* command, const wchar_t* title,
                        IShellItemArray* video, IShellItemArray* nonvideo,
                        IShellItemArray* multiple, IShellItemArray* mixed, bool estimate)
{
    PWSTR actual = nullptr;
    HRESULT hr = command->GetTitle(video, &actual);
    Check(SUCCEEDED(hr) && actual && title == std::wstring(actual), "Chinese menu title");
    CoTaskMemFree(actual);
    EXPCMDSTATE state = ECS_HIDDEN;
    hr = command->GetState(video, FALSE, &state);
    Check(hr == S_OK && state == ECS_ENABLED, "single video must be enabled");
    hr = command->GetState(nonvideo, FALSE, &state);
    Check(hr == S_OK && state == ECS_HIDDEN, "non-video must be hidden");
    hr = command->GetState(mixed, FALSE, &state);
    Check(hr == S_OK && state == ECS_HIDDEN, "mixed video and non-video must be hidden");
    hr = command->GetState(multiple, FALSE, &state);
    Check(hr == S_OK && state == (estimate ? ECS_DISABLED : ECS_ENABLED),
          estimate ? "multiple videos must disable estimate" : "multiple videos must remain visible");
}

int wmain(int argc, wchar_t** argv)
{
    if (argc != 2) { std::cerr << "Usage: SlimClipCommandSelfTest.exe <built dll>\n"; return 2; }
    HMODULE dll = LoadLibraryW(argv[1]);
    if (!dll) { std::cerr << "SlimClip COM self-test FAILED: LoadLibraryW error " << GetLastError() << '\n'; return 1; }
    using GetClassObject = HRESULT (STDAPICALLTYPE*)(REFCLSID, REFIID, void**);
    using CanUnloadNow = HRESULT (STDAPICALLTYPE*)();
    auto getClassObject = reinterpret_cast<GetClassObject>(GetProcAddress(dll, "DllGetClassObject"));
    auto canUnloadNow = reinterpret_cast<CanUnloadNow>(GetProcAddress(dll, "DllCanUnloadNow"));
    Check(getClassObject != nullptr && canUnloadNow != nullptr, "required COM DLL exports");
    if (getClassObject && canUnloadNow)
    {
        IClassFactory* factory = nullptr;
        HRESULT hr = getClassObject(kClsid, IID_IClassFactory, reinterpret_cast<void**>(&factory));
        Check(hr == S_OK && factory != nullptr, "DllGetClassObject / IClassFactory");
        if (factory)
        {
            IExplorerCommand* root = nullptr;
            hr = factory->CreateInstance(nullptr, IID_IExplorerCommand, reinterpret_cast<void**>(&root));
            Check(hr == S_OK && root != nullptr, "IClassFactory::CreateInstance");
            if (root)
            {
                FakeItem movie(L"C:\\smoke\\video.MP4");
                FakeItem another(L"C:\\smoke\\second.mkv");
                FakeItem document(L"C:\\smoke\\document.txt");
                IShellItem* one[] = { &movie };
                IShellItem* bad[] = { &document };
                IShellItem* two[] = { &movie, &another };
                IShellItem* both[] = { &movie, &document };
                FakeArray video(one, 1), nonvideo(bad, 1), multiple(two, 2), mixed(both, 2);
                TestCommand(root, L"压缩视频", &video, &nonvideo, &multiple, &mixed, false);
                EXPCMDFLAGS flags = ECF_DEFAULT;
                hr = root->GetFlags(&flags);
                Check(hr == S_OK && (flags & ECF_HASSUBCOMMANDS) != 0, "root flyout flag");
                IEnumExplorerCommand* children = nullptr;
                hr = root->EnumSubCommands(&children);
                Check(hr == S_OK && children != nullptr, "root enumerates children");
                if (children)
                {
                    const wchar_t* expected[] = { L"低（小文件）", L"中（均衡）", L"高（高画质）", L"预估大小", L"设置" };
                    for (int i = 0; i < 5; ++i)
                    {
                        IExplorerCommand* child = nullptr;
                        ULONG fetched = 0;
                        hr = children->Next(1, &child, &fetched);
                        Check(hr == S_OK && fetched == 1 && child != nullptr, "one child per enumerator step");
                        if (child)
                        {
                            TestCommand(child, expected[i], &video, &nonvideo, &multiple, &mixed, i == 3);
                            child->Release();
                        }
                    }
                    IExplorerCommand* extra = nullptr;
                    ULONG fetched = 123;
                    hr = children->Next(1, &extra, &fetched);
                    Check(hr == S_FALSE && fetched == 0 && extra == nullptr, "exactly five child commands");
                    if (extra) extra->Release();
                    Check(children->Reset() == S_OK, "reset child enumerator");
                    Check(children->Skip(5) == S_OK, "skipping exactly five commands succeeds");
                    IExplorerCommand* afterSkip = nullptr;
                    ULONG afterFetched = 99;
                    hr = children->Next(1, &afterSkip, &afterFetched);
                    Check(hr == S_FALSE && afterFetched == 0 && afterSkip == nullptr, "exact skip reaches end");
                    if (afterSkip) afterSkip->Release();
                    Check(children->Reset() == S_OK, "reset after exact skip");
                    Check(children->Skip(4) == S_OK, "skip to final child succeeds");
                    IExplorerCommand* last = nullptr;
                    ULONG lastFetched = 0;
                    hr = children->Next(1, &last, &lastFetched);
                    Check(hr == S_OK && lastFetched == 1 && last != nullptr, "final child remains after skip");
                    if (last) last->Release();
                    Check(children->Reset() == S_OK, "reset before oversized skip");
                    Check(children->Skip(~static_cast<ULONG>(0)) == S_FALSE, "oversized skip does not overflow");
                    children->Release();
                }
                root->Release();
            }
            factory->Release();
        }
        Check(canUnloadNow() == S_OK, "all COM objects released");
    }
    FreeLibrary(dll);
    if (failures) { std::cerr << "SlimClip COM self-test: " << failures << " failure(s)\n"; return 1; }
    std::cout << "SlimClip COM self-test passed: root, five children, visibility, estimate selection, and Skip boundaries\n";
    return 0;
}
