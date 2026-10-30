using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WechatDuokai.Core
{
    internal static class NetworkStreamReader
    {
        internal static async Task<int> ReadAsync(Stream input, byte[] buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            // Some .NET Framework network streams do not interrupt a pending ReadAsync
            // when its token is cancelled. Closing the stream aborts that socket read.
            using (token.Register(state =>
                   {
                       try { ((Stream)state).Dispose(); }
                       catch (Exception) { /* Cancellation must not throw on a timer thread. */ }
                   }, input))
            {
                try { return await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false); }
                catch (Exception exception) when (token.IsCancellationRequested &&
                    (exception is IOException || exception is ObjectDisposedException ||
                     exception is OperationCanceledException))
                {
                    throw new OperationCanceledException(token);
                }
            }
        }
    }
}
