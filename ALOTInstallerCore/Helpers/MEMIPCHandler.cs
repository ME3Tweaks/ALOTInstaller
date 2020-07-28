using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using CliWrap;
using CliWrap.EventStream;

namespace ALOTInstallerCore.Helpers
{
    public static class MEMIPCHandler
    {
        public static async void RunMEMIPC(string arguments, Action<int> applicationStarted, Action<string, string> ipcCallback, Action<string> applicationStdErr, Action<int> applicationExited, CancellationToken cancellationToken = default)
        {
            var cmd = Cli.Wrap(Locations.MEMPath()).WithArguments(arguments);
            Debug.Write($"Launching process: {Locations.MEMPath()} {arguments}");
            await foreach (var cmdEvent in cmd.ListenAsync(cancellationToken))
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        applicationStarted?.Invoke(started.ProcessId);
                        break;
                    case StandardOutputCommandEvent stdOut:
                        if (stdOut.Text.StartsWith(@"[IPC]"))
                        {
                            var ipc = breakdownIPC(stdOut.Text);
                            ipcCallback?.Invoke(ipc.command, ipc.param);
                        }
                        else
                        {
                            Debug.WriteLine(stdOut.Text);
                        }

                        break;
                    case StandardErrorCommandEvent stdErr:
                        applicationStdErr?.Invoke(stdErr.Text);
                        break;
                    case ExitedCommandEvent exited:
                        applicationExited?.Invoke(exited.ExitCode);
                        break;
                }
            }
        }

        /// <summary>
        /// Converts MEM IPC output to command, param for handling. This method assumes string starts with [IPC] always.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static (string command, string param) breakdownIPC(string str)
        {
            string command = str.Substring(5);
            int endOfCommand = command.IndexOf(' ');
            if (endOfCommand >= 0)
            {
                command = command.Substring(0, endOfCommand);
            }

            string param = str.Substring(endOfCommand + 5).Trim();
            return (command, param);
        }

        //public static void RunMassEffectModderNoGuiIPC(string operationName, string exe, string args, object lockObject, Action<string, string> exceptionOccuredCallback, Action<int?> setExitCodeCallback = null, Action<string, string> ipcCallback = null)
        //{
        //    Log.Information($@"Running Mass Effect Modder No GUI w/ IPC: {exe} {args}");
        //    var memProcess = new ConsoleApp(exe, args);
        //    bool hasExceptionOccured = false;
        //    memProcess.ConsoleOutput += (o, args2) =>
        //    {
        //        string str = args2.Line;
        //        if (hasExceptionOccured)
        //        {
        //            Log.Fatal(@"MassEffectModderNoGui.exe: " + str);
        //        }
        //        if (str.StartsWith(@"[IPC]", StringComparison.Ordinal))
        //        {
        //            string command = str.Substring(5);
        //            int endOfCommand = command.IndexOf(' ');
        //            if (endOfCommand >= 0)
        //            {
        //                command = command.Substring(0, endOfCommand);
        //            }

        //            string param = str.Substring(endOfCommand + 5).Trim();
        //            if (command == @"EXCEPTION_OCCURRED")
        //            {
        //                hasExceptionOccured = true;
        //                exceptionOccuredCallback?.Invoke(operationName, param);
        //                return; //don't process this command further, nothing handles it.
        //            }

        //            ipcCallback?.Invoke(command, param);
        //        }
        //        //Debug.WriteLine(args2.Line);
        //    };
        //    memProcess.Exited += (a, b) =>
        //    {
        //        setExitCodeCallback?.Invoke(memProcess.ExitCode);
        //        lock (lockObject)
        //        {
        //            Monitor.Pulse(lockObject);
        //        }
        //    };
        //    memProcess.Run();
        //}
    }
}
