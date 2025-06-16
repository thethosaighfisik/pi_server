using System;
using System.Threading;
using System.Threading.Tasks;
using PiServer.Services;

namespace PiServer.Models
{
    public class ReceiveProcess : IProcess
    {
        internal readonly string _channelName;
        private readonly string _filter;
        internal readonly Func<string, IProcess> _continuation;
        private readonly CancellationToken _ct;

        public ReceiveProcess(
            string channelName, 
            string filter, 
            Func<string, IProcess> continuation,
            CancellationToken ct = default)
        {
            _channelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
            _ct = ct;
        }

        
        public async Task ExecuteAsync(EnvironmentManager environment)
        {
            if (environment is null)
                throw new ArgumentNullException(nameof(environment));

            var channel = environment.GetChannel(_channelName)
                ?? throw new InvalidOperationException($"Channel {_channelName} not found");

            while (!_ct.IsCancellationRequested)
            {
                try
                {
                    var message = await channel.ReceiveAsync(_ct).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(message) && MatchesFilter(message))
                    {
                        environment.LogMessage($"[{DateTime.Now:HH:mm:ss.fff}] RECEIVE from {_channelName}: {message}");

                        var nextProcess = _continuation(message);
                        await nextProcess.ExecuteAsync(environment).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Корректная обработка отмены операции
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Receive error: {ex.Message}");
                    await Task.Delay(1000, _ct).ConfigureAwait(false);
                }
            }
        }

        private bool MatchesFilter(string message)
        {
            return string.IsNullOrEmpty(_filter) || 
                  (message?.Contains(_filter, StringComparison.Ordinal) == true);
        }
    }
}