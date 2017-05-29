using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;

namespace AlotAddOnGUI
{
    public class AnonymousPipes
    {
        private String clientPath;
        private AnonymousPipeServerStream outGoingServerPipe;
        private AnonymousPipeServerStream inComingServerPipe;
        private PipeStream clientIn;
        private PipeStream clientOut;
        private Process pipeClient;
        private String incomingHandle;
        private String outgoingHandle;
        private StreamWriter ssw;
        private StreamWriter csw;
        private bool serverMode;
        private bool running;
        private CallBack callback;
        private DisconnectEvent disconnectEvent;
        private String msgError;
        private String name;

        public delegate void CallBack(String msg);
        public delegate void DisconnectEvent();
        public String ermsg;

        public bool isConnected()
        {
            return running;
        }

        public String GetPipeName()
        {
            return name;
        }

        public AnonymousPipes(String pipeName)
        {
            this.name = pipeName;
        }

        private String StartPipeServer()
        {
            serverMode = true;
            outGoingServerPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            inComingServerPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);

            return outGoingServerPipe.GetClientHandleAsString() + ":::" + inComingServerPipe.GetClientHandleAsString();
        }

        public AnonymousPipes(String pipeName, String clientPath, String cmdLineArgs, CallBack callback, DisconnectEvent disconnectEvent)
        {
            String args;
            this.clientPath = clientPath;
            this.callback = callback;
            this.disconnectEvent = disconnectEvent;
            this.name = pipeName;
            this.running = true;

            serverMode = true;

            args = StartPipeServer() + " " + cmdLineArgs;

            try
            {
                pipeClient = new Process();
                #if !DEBUG
                pipeClient.StartInfo.CreateNoWindow = true;
                #endif
                pipeClient.StartInfo.FileName = clientPath;
                pipeClient.StartInfo.Arguments = args;
                pipeClient.StartInfo.UseShellExecute = false;
                pipeClient.Start();
            }
            catch (Exception ex)
            {
                ermsg = ex.Message;
                running = false;
                return;
            }

            outGoingServerPipe.DisposeLocalCopyOfClientHandle();
            inComingServerPipe.DisposeLocalCopyOfClientHandle();

            ssw = new StreamWriter(outGoingServerPipe);
            ssw.AutoFlush = true;
            ssw.WriteLine("SYNC");

            outGoingServerPipe.WaitForPipeDrain();

            new Thread(delegate ()
            {

                using (StreamReader isr = new StreamReader(inComingServerPipe))
                {
                    String tmp;
                    while (running && inComingServerPipe.IsConnected)
                    {
                        tmp = isr.ReadLine();
                        if (tmp != null) { callback(tmp); }
                    }
                }

                running = false;
                disconnectEvent();

            }).Start();
        }

        public bool SendText(String msg)
        {
            return SendText(msg, ref msgError);
        }

        public bool SendText(String msg, ref String errMsg)
        {
            if (serverMode)
            {
                try
                {
                    ssw.WriteLine(msg);
                    outGoingServerPipe.WaitForPipeDrain();
                    return true;
                }
                catch (Exception ex)
                {
                    errMsg = ex.Message;
                    return false;
                }
            }
            else
            {
                try
                {
                    csw.WriteLine(msg);
                    clientOut.WaitForPipeDrain();
                }
                catch (Exception) { }
                return true;
            }
        }

        public void ConnectToPipe(String clientHandles, CallBack callback, DisconnectEvent disconnectEvent)
        {
            String[] handles = System.Text.RegularExpressions.Regex.Split(clientHandles, ":::");
            this.incomingHandle = handles[0];
            this.outgoingHandle = handles[1];
            this.callback = callback;
            this.disconnectEvent = disconnectEvent;
            running = true;
            serverMode = false;

            new Thread(delegate ()
            {
                clientIn = new AnonymousPipeClientStream(PipeDirection.In, this.incomingHandle);
                clientOut = new AnonymousPipeClientStream(PipeDirection.Out, this.outgoingHandle);

                csw = new StreamWriter(clientOut);
                csw.AutoFlush = true;

                using (StreamReader sr = new StreamReader(clientIn))
                {
                    string temp;

                    do
                    {
                        temp = sr.ReadLine();
                    }
                    while (!temp.StartsWith("SYNC") && running);

                    while (running && clientIn.IsConnected)
                    {
                        temp = sr.ReadLine();
                        if (temp != null) { callback(temp); }
                    }

                    running = false;
                    disconnectEvent();
                }
            }).Start();
        }

        public void Close()
        {
            running = false;

            try
            {
                pipeClient.Close();
            }
            catch (Exception) { }

            try
            {
                outGoingServerPipe.Close();
            }
            catch (Exception) { }

            try
            {
                inComingServerPipe.Close();
            }
            catch (Exception) { }

            try
            {
                clientOut.Close();
            }
            catch (Exception) { }

            try
            {
                clientIn.Close();
            }
            catch (Exception) { }

            try
            {
                ssw.Close();
            }
            catch (Exception) { }

            try
            {
                csw.Close();
            }
            catch (Exception) { }
        }
    }
}
