using System.Collections.Concurrent;
using System.Threading.Tasks;
using PiServer.Models;

namespace PiServer.Services
{
    public class EnvironmentManager
    {
        private readonly ConcurrentDictionary<string, Channel> _channels = new();
        private readonly ConcurrentDictionary<string, ChannelStrategy> _strategies = new();

        public Channel GetOrCreateChannel(string name, ChannelStrategy strategy = ChannelStrategy.PassiveEnvironment)
        {
            var channel = _channels.GetOrAdd(name, new Channel(name));
            _strategies.TryAdd(name, strategy);
            return channel;
        }

        public ChannelStrategy GetStrategy(string channelName)
        {
            return _strategies.TryGetValue(channelName, out var strategy)
            ? strategy
            : ChannelStrategy.PassiveEnvironment;
        }

        public async Task TransferAsync(string channelName, string message)
        {
            var strategy = GetStrategy(channelName);
            var channel = GetOrCreateChannel(channelName);

            switch (strategy)
            {
                case ChannelStrategy.ActiveEnvironment:
                    await channel.SendAsync(message);
                    break;

                case ChannelStrategy.PassiveEnvironment:
                    await channel.SendAsync(message);
                    break;

                case ChannelStrategy.Hybrid:
                    await channel.SendAsync(message);
                    break;
            }
        }

        public Channel? GetChannel(string name)
        {
            _channels.TryGetValue(name, out var channel);
            return channel;
        }
        
        public void RegisterSend(Channel channel, string message)
        {
            // TODO: реализация или заглушка
        }

        public void RegisterReceive(Channel channel, Func<string, Task> receiver)
        {
            // TODO: реализация или заглушка
        }

        public void MixInteraction(Channel channel, string message, Func<string, Task> receiver)
        {
            // TODO: реализация или заглушка
        }


    }

    public enum ChannelStrategy
    {
        ActiveEnvironment,
        PassiveEnvironment,
        Hybrid
    }
}