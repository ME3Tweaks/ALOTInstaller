using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ALOTInstallerCore.Helpers
{
    public static class MEMIPCHandler
    {
        public static void RunMassEffectModderNoGuiIPC(string operationName, string exe, string args, object lockObject, Action<string, string> exceptionOccuredCallback, Action<int?> setExitCodeCallback = null, Action<string, string> ipcCallback = null)
        {
            Log.Information($@"Running Mass Effect Modder No GUI w/ IPC: {exe} {args}");
            var memProcess = new ConsoleApp(exe, args);
            bool hasExceptionOccured = false;
            memProcess.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                if (hasExceptionOccured)
                {
                    Log.Fatal(@"MassEffectModderNoGui.exe: " + str);
                }
                if (str.StartsWith(@"[IPC]", StringComparison.Ordinal))
                {
                    string command = str.Substring(5);
                    int endOfCommand = command.IndexOf(' ');
                    if (endOfCommand >= 0)
                    {
                        command = command.Substring(0, endOfCommand);
                    }

                    string param = str.Substring(endOfCommand + 5).Trim();
                    if (command == @"EXCEPTION_OCCURRED")
                    {
                        hasExceptionOccured = true;
                        exceptionOccuredCallback?.Invoke(operationName, param);
                        return; //don't process this command further, nothing handles it.
                    }

                    ipcCallback?.Invoke(command, param);
                }
                //Debug.WriteLine(args2.Line);
            };
            memProcess.Exited += (a, b) =>
            {
                setExitCodeCallback?.Invoke(memProcess.ExitCode);
                lock (lockObject)
                {
                    Monitor.Pulse(lockObject);
                }
            };
            memProcess.Run();
        }
    }
}
