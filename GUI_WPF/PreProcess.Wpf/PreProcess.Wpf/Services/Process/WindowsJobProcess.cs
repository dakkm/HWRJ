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
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int Id { get; private set; }
        public StreamReader Output { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public StreamReader Error { get; private set; }
        private static readonly object LaunchLock = new object();

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static WindowsJobProcess Start(ProcessRunRequest request)
        {
            lock (LaunchLock) return Launch(request);
        }
        private static WindowsJobProcess Launch(ProcessRunRequest request)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var instance = new WindowsJobProcess();
            IntPtr outRead = IntPtr.Zero, outWrite = IntPtr.Zero, errRead = IntPtr.Zero, errWrite = IntPtr.Zero;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            IntPtr inRead = IntPtr.Zero, inWrite = IntPtr.Zero, environment = IntPtr.Zero;
            var info = new ProcessInformation();
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                instance.job = CreateJobObject(IntPtr.Zero, null);
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                Check(instance.job != IntPtr.Zero);
                var limits = new ExtendedLimits();
                limits.Basic.LimitFlags = 0x2000; // KILL_ON_JOB_CLOSE, no breakaway flags.
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                Check(SetInformationJobObject(instance.job, 9, ref limits, (uint)Marshal.SizeOf(limits)));
                Pipe(out outRead, out outWrite); Pipe(out errRead, out errWrite); Pipe(out inRead, out inWrite);
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                Check(SetHandleInformation(outRead, 1, 0)); Check(SetHandleInformation(errRead, 1, 0));
                Check(SetHandleInformation(inWrite, 1, 0));
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Flags = 0x100,
                    Input = inRead, Output = outWrite, Error = errWrite };
                var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables()) variables[(string)entry.Key] = (string)entry.Value;
                foreach (var entry in request.EnvironmentVariables) variables[entry.Key] = entry.Value;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                string block = String.Join("\0", variables.Select(x => x.Key + "=" + x.Value)) + "\0\0";
                environment = Marshal.StringToHGlobalUni(block);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var command = new StringBuilder(Quote(request.Executable) + " " + String.Join(" ", request.Arguments.Select(Quote)));
                Check(CreateProcess(request.Executable, command, IntPtr.Zero, IntPtr.Zero, true,
                    0x08000000 | 0x4 | 0x400, environment, request.WorkingDirectory, ref startup, out info));
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                instance.process = info.Process;
                instance.Id = (int)info.ProcessId;
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                Check(AssignProcessToJobObject(instance.job, instance.process));
                instance.Output = Reader(ref outRead); instance.Error = Reader(ref errRead);
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                Check(ResumeThread(info.Thread) != UInt32.MaxValue);
                return instance;
            }
            catch
            {
                // Assignment failure must fail closed; the root may still be suspended outside the job.
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (instance.process != IntPtr.Zero) { TerminateProcess(instance.process, 1); WaitForSingleObject(instance.process, 5000); }
                instance.Dispose();
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw;
            }
            finally
            {
                // 结束当前资源的使用，避免残留句柄或后台任务。
                Close(ref outRead); Close(ref outWrite); Close(ref errRead); Close(ref errWrite);
                Close(ref inRead); Close(ref inWrite); Close(ref info.Thread);
                if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            }
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public int Wait()
        {
            Check(WaitForSingleObject(process, UInt32.MaxValue) == 0);
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            uint code; Check(GetExitCodeProcess(process, out code)); return unchecked((int)code);
        }
        public void Terminate() { if (job != IntPtr.Zero) Check(TerminateJobObject(job, 1)); }
        // 结束当前资源的使用，避免残留句柄或后台任务。
        public void Dispose()
        {
            Close(ref job); // Also kills descendants after abnormal GUI shutdown / normal root exit.
            // 结束当前资源的使用，避免残留句柄或后台任务。
            Output?.Dispose(); Error?.Dispose(); Close(ref process);
        }
        // Windows CRT command-line quoting: do not invoke cmd.exe or concatenate unquoted user paths.
        public static string Quote(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0) throw new ArgumentException("Invalid process argument.");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var text = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (c == '\\') { slashes++; continue; }
                if (c == '"') text.Append('\\', slashes * 2 + 1).Append(c);
                // 当前置条件不成立时执行备用路径，保持处理结果完整。
                else text.Append('\\', slashes).Append(c);
                slashes = 0;
            }
            return text.Append('\\', slashes * 2).Append('"').ToString();
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static StreamReader Reader(ref IntPtr handle)
        {
            var safe = new SafeFileHandle(handle, true); handle = IntPtr.Zero;
            // 返回当前步骤生成的结果，并结束本次调用。
            return new StreamReader(new FileStream(safe, FileAccess.Read, 4096, false), new UTF8Encoding(false), true);
        }
        private static void Pipe(out IntPtr read, out IntPtr write)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Inherit = true };
            Check(CreatePipe(out read, out write, ref attributes, 0));
        }
        private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        // 结束当前资源的使用，避免残留句柄或后台任务。
        private static void Close(ref IntPtr handle) { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
        // 定义 StartupInfo 类型，集中封装与该领域对象相关的状态和行为。
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
        {
            public int Size; public string Reserved, Desktop, Title;
            // 保存该组件运行所需的配置或中间状态。
            public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
            public short Show, ReservedSize; public IntPtr ReservedPointer, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
        // 定义 BasicLimits 类型，集中封装与该领域对象相关的状态和行为。
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
        {
            public long ProcessTime, JobTime; public uint LimitFlags; public UIntPtr MinWorking, MaxWorking;
            // 保存该组件运行所需的配置或中间状态。
            public uint ActiveLimit; public UIntPtr Affinity; public uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
        // 定义 ExtendedLimits 类型，集中封装与该领域对象相关的状态和行为。
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
        { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int type, ref ExtendedLimits info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr read, out IntPtr write, ref SecurityAttributes attributes, uint size);
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string app, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation info);
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
