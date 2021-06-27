using System;
using System.IO;
using System.Net.Http;
using System.Text;
using ALOTInstallerCore.Helpers;
using LegendaryExplorerCore.Compression;
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
        public static (bool uploaded, string result) UploadLog(string logtext, string endpoint)
        {
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
                    return (false, $"Error uploading log: Response code {response.StatusCode.ToString()}");
                }
                var resultStream = response.Content.ReadAsStreamAsync().Result;
                var responseString = new StreamReader(resultStream).ReadToEnd();

                Uri uriResult;
                bool result = Uri.TryCreate(responseString, UriKind.Absolute, out uriResult)
                              && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result)
                {
                    //should be valid URL.
                    //diagnosticsWorker.ReportProgress(0, new ThreadCommand(SET_DIAGTASK_ICON_GREEN, Image_Upload));
                    //e.Result = responseString;
                    Log.Information(@"[AICORE] Result from server for log upload: " + responseString);
                    return (true, responseString);
                }
                Log.Error(@"[AICORE] Error uploading log. The server responded with: " + responseString);
                return (false, $"The server rejected the upload: {responseString}");
            }
            catch (Exception ex)
            {
                // ex.Message contains rich details, including the URL, verb, response status,
                // and request and response bodies (if available)
                Log.Error($@"[AICORE] Handled error uploading log:");
                ex.WriteToLog("[AICORE] ");
                return (false, $"Error uploading log: {ex.Message}");
            }
        }
    }
}
