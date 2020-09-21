using System;
using System.IO;
using System.Net.Http;
using System.Text;
using ALOTInstallerCore.Helpers;
using ME3ExplorerCore.Compression;
using Serilog;

namespace ALOTInstallerCore.ModManager.ME3Tweaks
{
    public static class LogUploader
    {
        /// <summary>
        /// Uploads a log to the specified endpoint, using the lzma upload method. The receiver must accept and return a link to the diagnostic, or an error reason which will be returned to the caller. This method is synchronous and should not be run on a UI thread
        /// </summary>
        /// <param name="logtext"></param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public static string UploadLog(string logtext, string endpoint)
        {
            //void updateStatusCallback(string status)
            //{
            //    CollectionStatusMessage = status;
            //}

            //void updateProgressCallback(int progress)
            //{
            //    nbw.ReportProgress(0, progress / 100.0);
            //}

            //void updateTaskbarProgressStateCallback(TaskbarItemProgressState state)
            //{
            //    nbw.ReportProgress(-1, state);
            //}
            //StringBuilder logUploadText = new StringBuilder();
            //if (SelectedDiagnosticTarget != null && SelectedDiagnosticTarget.Game > Mod.MEGame.Unknown)
            //{
            //    Debug.WriteLine(@"Selected game target: " + SelectedDiagnosticTarget.TargetPath);
            //    logUploadText.Append("[MODE]diagnostics\n"); //do not localize
            //    logUploadText.Append(LogCollector.PerformDiagnostic(SelectedDiagnosticTarget, TextureCheck, updateStatusCallback, updateProgressCallback, updateTaskbarProgressStateCallback));
            //    logUploadText.Append("\n"); //do not localize
            //}

            //if (SelectedLog != null && SelectedLog.Selectable)
            //{
            //    Debug.WriteLine(@"Selected log: " + SelectedLog.filepath);
            //    logUploadText.Append("[MODE]logs\n"); //do not localize
            //    logUploadText.AppendLine(LogCollector.CollectLogs(SelectedLog.filepath));
            //    logUploadText.Append("\n"); //do not localize
            //}

            //var logtext = logUploadText.ToString();
            //if (logtext != null)
            //{
            var lzmalog = LZMA.CompressToLZMAFile(Encoding.UTF8.GetBytes(logtext));
            var lzmamd5 = Utilities.CalculateMD5(new MemoryStream(lzmalog));
            try
            {
                // examples of converting both Stream and byte [] to HttpContent objects
                // representing input type file
                HttpContent bytesContent = new ByteArrayContent(lzmalog);

                // Submit the form using HttpClient and 
                // create form data as Multipart (enctype="multipart/form-data")

                using var client = new HttpClient();
                using var formData = new MultipartFormDataContent();
                // <input type="file" name="file2" />
                formData.Add(new StringContent(Utilities.GetAppVersion().ToString()), "toolversion");
                formData.Add(new StringContent(Utilities.GetHostingProcessname()), "tool");
                formData.Add(new StringContent(lzmamd5), "lzmamd5");
                formData.Add(bytesContent, "lzmafile", "lzmafile.lzma");
                // Invoke the request to the server

                // equivalent to pressing the submit button on
                // a form with attributes (action="{url}" method="post")
                var response = client.PostAsync(endpoint, formData).Result;

                // ensure the request was a success
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                var resultStream = response.Content.ReadAsStreamAsync().Result;
                var responseString = new StreamReader(resultStream).ReadToEnd();
















                //var request = WebRequest.CreateHttp(endpoint);
                //request.Method = "POST";
                //request.ContentType = "application/x-www-form-urlencoded";
                //var logB64 = Convert.ToBase64String(lzmalog);
                //string reqText = $"LogData={logB64}&ToolVersion={Utilities.GetAppVersion()}&ToolName={Utilities.GetHostingProcessname()}&LogDataMD5={lzmamd5}";
                //File.WriteAllText(@"C:\users\mgamerz\desktop\post.txt", reqText);
                //request.ContentLength = reqText.Length;
                //using (StreamWriter stOut = new StreamWriter(request.GetRequestStream()))
                //{
                //    stOut.Write(reqText);
                //    stOut.Close();
                //}

                //var response = request.GetResponse();
                //using var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.ASCII);
                //string responseString = reader.ReadToEnd();

                Uri uriResult;
                bool result = Uri.TryCreate(responseString, UriKind.Absolute, out uriResult)
                              && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result)
                {
                    //should be valid URL.
                    //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_Upload));
                    //e.Result = responseString;
                    Log.Information(@"[AICORE] Result from server for log upload: " + responseString);
                    return responseString;
                }
                Log.Error(@"[AICORE] Error uploading log. The server responded with: " + responseString);
                return $"The server rejected the upload: {responseString}";
            }
            catch (Exception ex)
            {
                // ex.Message contains rich details, inclulding the URL, verb, response status,
                // and request and response bodies (if available)
                Log.Error($@"[AICORE] Handled error uploading log:");
                ex.WriteToLog("[AICORE] ");
                return $"Error uploading log: {ex.Message}";
            }
        }
    }
}
