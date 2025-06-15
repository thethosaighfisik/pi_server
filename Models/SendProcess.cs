using System;
using System.Threading.Tasks;
using PiServer.Services;

namespace PiServer.Models
{
    public class SendProcess : IProcess
    {
        public string ChannelName { get; }
        public string Message { get; }
        public IProcess? NextProcess { get; } // Делаем nullable

        public SendProcess(string channelName, string message, IProcess? nextProcess = null)
        {
            ChannelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            NextProcess = nextProcess;
        }

        public async Task ExecuteAsync(EnvironmentManager environment)
        {
            var channel = environment.GetChannel(ChannelName);
            if (channel == null) throw new InvalidOperationException($"Channel {ChannelName} not found");

            await channel.SendAsync(Message);
            if (NextProcess != null)
            {
                await NextProcess.ExecuteAsync(environment);
            }
        }
    }
}