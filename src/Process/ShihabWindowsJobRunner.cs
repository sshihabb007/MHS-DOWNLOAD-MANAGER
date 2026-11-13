using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ShihabMehediIDM
{
    // Win32 Job Object Subprocess Runner (Kill-On-Close + Deadlock-Free Pipes)
    public static class ShihabWindowsJobRunner
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private static readonly IntPtr _sharedKillOnCloseJob = CreateKillOnCloseJob();

        private static IntPtr CreateKillOnCloseJob()
        {
            try
            {
                IntPtr job = CreateJobObjectW(IntPtr.Zero, null);
                if (job == IntPtr.Zero) return IntPtr.Zero;
                JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                int len = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr ptr = Marshal.AllocHGlobal(len);
                try
                {
                    Marshal.StructureToPtr(info, ptr, false);
                    SetInformationJobObject(job, 9, ptr, (uint)len);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
                return job;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        public static void AttachProcessToKillOnCloseJob(Process proc)
        {
            if (proc == null || _sharedKillOnCloseJob == IntPtr.Zero) return;
            try { AssignProcessToJobObject(_sharedKillOnCloseJob, proc.Handle); } catch { }
        }

        public static int RunManagedSubprocess(
            string exePath,
            string arguments,
            string workingDirectory,
            int timeoutMs,
            Action<string> onStdoutLine,
            CancellationToken cancelToken,
            out string stdoutAll,
            out string stderrAll)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                stdoutAll = "";
                stderrAll = "Subprocess binary does not exist.";
                return -1;
            }

            StringBuilder outSb = new StringBuilder();
            StringBuilder errSb = new StringBuilder();
            object sbLock = new object();

            ProcessStartInfo psi = new ProcessStartInfo(exePath, arguments ?? "");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                psi.WorkingDirectory = workingDirectory;
            }

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (sbLock) { if (outSb.Length < 16 * 1024 * 1024) outSb.AppendLine(e.Data); }
                    if (onStdoutLine != null) { try { onStdoutLine(e.Data); } catch { } }
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (sbLock) { if (errSb.Length < 2 * 1024 * 1024) errSb.AppendLine(e.Data); }
                };

                p.Start();
                AttachProcessToKillOnCloseJob(p);
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                using (cancelToken.Register(delegate { try { if (!p.HasExited) p.Kill(); } catch { } }))
                {
                    bool exited = timeoutMs > 0 ? p.WaitForExit(timeoutMs) : p.WaitForExit(int.MaxValue);
                    if (!exited)
                    {
                        try { p.Kill(); } catch { }
                        try { p.WaitForExit(1000); } catch { }
                        lock (sbLock)
                        {
                            stdoutAll = outSb.ToString();
                            stderrAll = "Subprocess timed out: " + errSb.ToString();
                        }
                        return -1;
                    }
                    p.WaitForExit();
                }

                lock (sbLock)
                {
                    stdoutAll = outSb.ToString();
                    stderrAll = errSb.ToString();
                }
                return p.ExitCode;
            }
        }
    }
}

