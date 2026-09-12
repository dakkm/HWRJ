using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PreProcess.Wpf.Services.Process
{
    // Suspend BEFORE assigning the job, so even a fast child cannot escape containment.
    internal sealed class WindowsJobProcess : IDisposable
    {
        private IntPtr job, process;
        public int Id { get; private set; }
        public StreamReader Output { get; private set; }
        public StreamReader Error { get; private set; }
        private static readonly object LaunchLock = new object();

        public static WindowsJobProcess Start(ProcessRunRequest request)
        {
            lock (LaunchLock) return Launch(request);
        }
        private static WindowsJobProcess Launch(ProcessRunRequest request)
        {
            var instance = new WindowsJobProcess();
            IntPtr outRead = IntPtr.Zero, outWrite = IntPtr.Zero, errRead = IntPtr.Zero, errWrite = IntPtr.Zero;
            IntPtr inRead = IntPtr.Zero, inWrite = IntPtr.Zero, environment = IntPtr.Zero;
            var info = new ProcessInformation();
            try
            {
                instance.job = CreateJobObject(IntPtr.Zero, null);
                Check(instance.job != IntPtr.Zero);
                var limits = new ExtendedLimits();
                limits.Basic.LimitFlags = 0x2000; // KILL_ON_JOB_CLOSE, no breakaway flags.
                Check(SetInformationJobObject(instance.job, 9, ref limits, (uint)Marshal.SizeOf(limits)));
                Pipe(out outRead, out outWrite); Pipe(out errRead, out errWrite); Pipe(out inRead, out inWrite);
                Check(SetHandleInformation(outRead, 1, 0)); Check(SetHandleInformation(errRead, 1, 0));
                Check(SetHandleInformation(inWrite, 1, 0));
                var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Flags = 0x100,
                    Input = inRead, Output = outWrite, Error = errWrite };
                var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables()) variables[(string)entry.Key] = (string)entry.Value;
                foreach (var entry in request.EnvironmentVariables) variables[entry.Key] = entry.Value;
                string block = String.Join("\0", variables.Select(x => x.Key + "=" + x.Value)) + "\0\0";
                environment = Marshal.StringToHGlobalUni(block);
                var command = new StringBuilder(Quote(request.Executable) + " " + String.Join(" ", request.Arguments.Select(Quote)));
                Check(CreateProcess(request.Executable, command, IntPtr.Zero, IntPtr.Zero, true,
                    0x08000000 | 0x4 | 0x400, environment, request.WorkingDirectory, ref startup, out info));
                instance.process = info.Process;
                instance.Id = (int)info.ProcessId;
                Check(AssignProcessToJobObject(instance.job, instance.process));
                instance.Output = Reader(ref outRead); instance.Error = Reader(ref errRead);
                Check(ResumeThread(info.Thread) != UInt32.MaxValue);
                return instance;
            }
            catch
            {
                // Assignment failure must fail closed; the root may still be suspended outside the job.
                if (instance.process != IntPtr.Zero) { TerminateProcess(instance.process, 1); WaitForSingleObject(instance.process, 5000); }
                instance.Dispose();
                throw;
            }
            finally
            {
                Close(ref outRead); Close(ref outWrite); Close(ref errRead); Close(ref errWrite);
                Close(ref inRead); Close(ref inWrite); Close(ref info.Thread);
                if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            }
        }
        public int Wait()
        {
            Check(WaitForSingleObject(process, UInt32.MaxValue) == 0);
            uint code; Check(GetExitCodeProcess(process, out code)); return unchecked((int)code);
        }
        public void Terminate() { if (job != IntPtr.Zero) Check(TerminateJobObject(job, 1)); }
        public void Dispose()
        {
            Close(ref job); // Also kills descendants after abnormal GUI shutdown / normal root exit.
            Output?.Dispose(); Error?.Dispose(); Close(ref process);
        }
        // Windows CRT command-line quoting: do not invoke cmd.exe or concatenate unquoted user paths.
        public static string Quote(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0) throw new ArgumentException("Invalid process argument.");
            var text = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') text.Append('\\', slashes * 2 + 1).Append(c);
                else text.Append('\\', slashes).Append(c);
                slashes = 0;
            }
            return text.Append('\\', slashes * 2).Append('"').ToString();
        }
        private static StreamReader Reader(ref IntPtr handle)
        {
            var safe = new SafeFileHandle(handle, true); handle = IntPtr.Zero;
            return new StreamReader(new FileStream(safe, FileAccess.Read, 4096, false), new UTF8Encoding(false), true);
        }
        private static void Pipe(out IntPtr read, out IntPtr write)
        {
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Inherit = true };
            Check(CreatePipe(out read, out write, ref attributes, 0));
        }
        private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        private static void Close(ref IntPtr handle) { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
        {
            public int Size; public string Reserved, Desktop, Title;
            public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
            public short Show, ReservedSize; public IntPtr ReservedPointer, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
        {
            public long ProcessTime, JobTime; public uint LimitFlags; public UIntPtr MinWorking, MaxWorking;
            public uint ActiveLimit; public UIntPtr Affinity; public uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
        { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int type, ref ExtendedLimits info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr read, out IntPtr write, ref SecurityAttributes attributes, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string app, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation info);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
