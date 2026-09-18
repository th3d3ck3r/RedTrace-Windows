using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RedTrace.Windows;

/// Native Windows 10 1809+ ConPTY transport. No Windows Terminal dependency.
public sealed class ConPtySession : IDisposable
{
    const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016, EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    IntPtr pseudo, inputWrite, outputRead, process, thread, attributeList; CancellationTokenSource? cancel;
    public event Action<byte[]>? Output; public bool IsRunning => process != IntPtr.Zero;
    [StructLayout(LayoutKind.Sequential)] struct COORD { public short X,Y; public COORD(short x,short y){X=x;Y=y;} }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct STARTUPINFO { public int cb; public string? lpReserved,lpDesktop,lpTitle; public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags; public short wShowWindow,cbReserved2; public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct PROCESS_INFORMATION { public IntPtr hProcess,hThread; public int dwProcessId,dwThreadId; }
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool CreatePipe(out IntPtr r,out IntPtr w,IntPtr a,int size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern int CreatePseudoConsole(COORD size,IntPtr input,IntPtr output,uint flags,out IntPtr pty);
    [DllImport("kernel32.dll")] static extern int ResizePseudoConsole(IntPtr pty,COORD size);
    [DllImport("kernel32.dll")] static extern void ClosePseudoConsole(IntPtr pty);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcess(string? app,string command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr env,string? cwd,ref STARTUPINFOEX startup,out PROCESS_INFORMATION info);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadFile(IntPtr h,byte[] b,int count,out int read,IntPtr over);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteFile(IntPtr h,byte[] b,int count,out int written,IntPtr over);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h); [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h,uint c);
    public void Start(string file,string args,int columns=100,int rows=30){Stop();IntPtr inputRead=IntPtr.Zero, outputWrite=IntPtr.Zero;try{if(!CreatePipe(out inputRead,out inputWrite,IntPtr.Zero,0)||!CreatePipe(out outputRead,out outputWrite,IntPtr.Zero,0))throw new System.ComponentModel.Win32Exception();if(CreatePseudoConsole(new COORD((short)Math.Max(2,columns),(short)Math.Max(1,rows)),inputRead,outputWrite,0,out pseudo)!=0)throw new System.ComponentModel.Win32Exception();CloseHandle(inputRead);inputRead=IntPtr.Zero;CloseHandle(outputWrite);outputWrite=IntPtr.Zero;IntPtr size=IntPtr.Zero;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);attributeList=Marshal.AllocHGlobal(size);if(!InitializeProcThreadAttributeList(attributeList,1,0,ref size))throw new System.ComponentModel.Win32Exception();if(!UpdateProcThreadAttribute(attributeList,0,(IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,pseudo,(IntPtr)IntPtr.Size,IntPtr.Zero,IntPtr.Zero))throw new System.ComponentModel.Win32Exception();var si=new STARTUPINFOEX{StartupInfo=new STARTUPINFO{cb=Marshal.SizeOf<STARTUPINFOEX>()},lpAttributeList=attributeList};if(!CreateProcess(null,$"\"{file}\" {args}",IntPtr.Zero,IntPtr.Zero,false,EXTENDED_STARTUPINFO_PRESENT,IntPtr.Zero,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),ref si,out var pi))throw new System.ComponentModel.Win32Exception();process=pi.hProcess;thread=pi.hThread;cancel=new();_ = Task.Run(ReadLoop);}catch{Stop();throw;}finally{if(inputRead!=IntPtr.Zero)CloseHandle(inputRead);if(outputWrite!=IntPtr.Zero)CloseHandle(outputWrite);}}
    async Task ReadLoop(){var b=new byte[8192];while(cancel is {IsCancellationRequested:false}&&outputRead!=IntPtr.Zero){if(!ReadFile(outputRead,b,b.Length,out var n,IntPtr.Zero)||n==0)break;Output?.Invoke(b[..n]);await Task.Yield();}}
    public void SendBytes(byte[] bytes){if(inputWrite!=IntPtr.Zero)WriteFile(inputWrite,bytes,bytes.Length,out _,IntPtr.Zero);} public void SendText(string value)=>SendBytes(System.Text.Encoding.UTF8.GetBytes(value)); public void Resize(int c,int r){if(pseudo!=IntPtr.Zero)ResizePseudoConsole(pseudo,new COORD((short)Math.Max(2,c),(short)Math.Max(1,r)));}
    public void Stop(){cancel?.Cancel();cancel?.Dispose();cancel=null;if(process!=IntPtr.Zero){TerminateProcess(process,0);CloseHandle(process);process=IntPtr.Zero;}if(thread!=IntPtr.Zero){CloseHandle(thread);thread=IntPtr.Zero;}if(pseudo!=IntPtr.Zero){ClosePseudoConsole(pseudo);pseudo=IntPtr.Zero;}if(inputWrite!=IntPtr.Zero){CloseHandle(inputWrite);inputWrite=IntPtr.Zero;}if(outputRead!=IntPtr.Zero){CloseHandle(outputRead);outputRead=IntPtr.Zero;}if(attributeList!=IntPtr.Zero){DeleteProcThreadAttributeList(attributeList);Marshal.FreeHGlobal(attributeList);attributeList=IntPtr.Zero;}} public void Dispose()=>Stop();
}
