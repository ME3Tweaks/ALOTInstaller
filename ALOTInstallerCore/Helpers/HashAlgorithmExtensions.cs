using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ALOTInstallerCore.Helpers
{
    public static class HashAlgorithmExtensions
    {
        public static async Task<string> ComputeHashAsync(
            this HashAlgorithm hashAlgorithm, Stream stream,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<long> progress = null,
            int bufferSize = 1024 * 1024 * 4) // 4MB buffer
        {
            byte[] readAheadBuffer, buffer;
            int readAheadBytesRead, bytesRead;
            long size, totalBytesRead = 0;
            size = stream.Length;
            readAheadBuffer = new byte[bufferSize];
            readAheadBytesRead = await stream.ReadAsync(readAheadBuffer, 0,
                readAheadBuffer.Length, cancellationToken);
            totalBytesRead += readAheadBytesRead;
            do
            {
                bytesRead = readAheadBytesRead;
                buffer = readAheadBuffer;
                readAheadBuffer = new byte[bufferSize];
                readAheadBytesRead = await stream.ReadAsync(readAheadBuffer, 0,
                    readAheadBuffer.Length, cancellationToken);
                totalBytesRead += readAheadBytesRead;

                if (readAheadBytesRead == 0)
                    hashAlgorithm.TransformFinalBlock(buffer, 0, bytesRead);
                else
                    hashAlgorithm.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                progress?.Invoke(totalBytesRead);
                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();
            } while (readAheadBytesRead != 0);
            return BitConverter.ToString(hashAlgorithm.Hash).Replace("-", string.Empty).ToLower();
        }
    }
}
