using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LLM2Everything.App.Services;

public static class ExplorerFolderPicker
{
    public static IReadOnlyList<string> PickFolders(Window? owner, string title)
    {
        var dialog = (IFileOpenDialog)(object)new FileOpenDialog();
        dialog.GetOptions(out var options);
        dialog.SetOptions(options
            | FileOpenOptions.PickFolders
            | FileOpenOptions.ForceFileSystem
            | FileOpenOptions.AllowMultiSelect
            | FileOpenOptions.PathMustExist
            | FileOpenOptions.NoChangeDir);
        dialog.SetTitle(title);

        var hwnd = owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
        var result = dialog.Show(hwnd);
        if (result == HResultCancelled)
            return [];
        Marshal.ThrowExceptionForHR(result);

        dialog.GetResults(out var items);
        items.GetCount(out var count);
        var folders = new List<string>((int)count);
        for (uint i = 0; i < count; i++)
        {
            items.GetItemAt(i, out var item);
            item.GetDisplayName(ShellItemDisplayName.FileSystemPath, out var pathPtr);
            try
            {
                var path = Marshal.PtrToStringUni(pathPtr);
                if (!string.IsNullOrWhiteSpace(path))
                    folders.Add(path);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
        }
        return folders;
    }

    private const int HResultCancelled = unchecked((int)0x800704C7);
}

[ComImport]
[Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
internal sealed class FileOpenDialog;

[ComImport]
[Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    [PreserveSig]
    int Show(IntPtr parent);
    void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
    void SetFileTypeIndex(uint iFileType);
    void GetFileTypeIndex(out uint piFileType);
    void Advise(IntPtr pfde, out uint pdwCookie);
    void Unadvise(uint dwCookie);
    void SetOptions(FileOpenOptions fos);
    void GetOptions(out FileOpenOptions pfos);
    void SetDefaultFolder(IShellItem psi);
    void SetFolder(IShellItem psi);
    void GetFolder(out IShellItem ppsi);
    void GetCurrentSelection(out IShellItem ppsi);
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
    void GetResult(out IShellItem ppsi);
    void AddPlace(IShellItem psi, int fdap);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
    void Close(int hr);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter(IntPtr pFilter);
    void GetResults(out IShellItemArray ppenum);
    void GetSelectedItems(out IShellItemArray ppsai);
}

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(ShellItemDisplayName sigdnName, out IntPtr ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    void BindToHandler(IntPtr pbc, ref Guid rbhid, ref Guid riid, out IntPtr ppvOut);
    void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
    void GetPropertyDescriptionList(ref Guid keyType, ref Guid riid, out IntPtr ppv);
    void GetAttributes(int attribFlags, uint sfgaoMask, out uint psfgaoAttribs);
    void GetCount(out uint pdwNumItems);
    void GetItemAt(uint dwIndex, out IShellItem ppsi);
    void EnumItems(out IntPtr ppenumShellItems);
}

[Flags]
internal enum FileOpenOptions : uint
{
    NoChangeDir = 0x00000008,
    PickFolders = 0x00000020,
    ForceFileSystem = 0x00000040,
    AllowMultiSelect = 0x00000200,
    PathMustExist = 0x00000800
}

internal enum ShellItemDisplayName : uint
{
    FileSystemPath = 0x80058000
}
