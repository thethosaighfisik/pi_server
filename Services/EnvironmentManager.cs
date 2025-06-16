using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using PiServer.Models;


namespace PiServer.Services
{
    public class EnvironmentManager
    {
        private readonly ConcurrentDictionary<string, Channel> _channels = new();
        private readonly ConcurrentDictionary<string, ChannelStrategy> _strategies = new();

        public List<string> MessageLogs { get; } = new();



        private readonly Dictionary<string, Queue<string>> _channelMessages = new();

        
        // Добавляем nullable для события
        public event Action<string>? MessageLogged;



        // EnvironmentManager.cs


        public bool TryReceiveMessage(string channel, out string? message)
        {
            if (_channelMessages.TryGetValue(channel, out var queue) && queue.Count > 0)
            {
                message = queue.Dequeue();
                return true;
            }

            message = null;
            return false;
        }


        public void EnqueueMessage(string channel, string message)
        {
            if (!_channelMessages.ContainsKey(channel))
                _channelMessages[channel] = new Queue<string>();

            _channelMessages[channel].Enqueue(message);
        }

     
     

        public void LogMessage(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            MessageLogs.Add(entry);
            MessageLogged?.Invoke(entry);
        }



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

            MessageLogged?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] SEND to {channelName}: {message}");

            switch (strategy)
            {
                case ChannelStrategy.ActiveEnvironment:
                case ChannelStrategy.PassiveEnvironment:
                case ChannelStrategy.Hybrid:
                    await channel.SendAsync(message);
                    break;
            }
        }

        public Channel? GetChannel(string name)
        {
            return _channels.TryGetValue(name, out var channel) ? channel : null;
        }

        // Добавляем недостающие методы
        public void RegisterSend(Channel channel, string message)
        {
            MessageLogged?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] REGISTER SEND: {channel.Name} - {message}");
        }

        public void RegisterReceive(Channel channel, Func<string, Task> receiver)
        {
            MessageLogged?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] REGISTER RECEIVE: {channel.Name}");
        }

        public void MixInteraction(Channel channel, string message, Func<string, Task> receiver)
        {
            MessageLogged?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] MIX INTERACTION: {channel.Name} - {message}");
        }
    }

    public enum ChannelStrategy
    {
        ActiveEnvironment,
        PassiveEnvironment,
        Hybrid
    }
}