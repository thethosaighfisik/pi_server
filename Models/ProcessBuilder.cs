using System;
using System.Collections.Generic;
using System.Linq;
using PiServer.Services;


namespace PiServer.Models
{
    public class ProcessBuilder
    {
        private IProcess _currentProcess;
        private readonly EnvironmentManager _env;

        public ProcessBuilder(EnvironmentManager env)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _currentProcess = new InactiveProcess();
        }

        public IProcess GetCurrentProcess() => _currentProcess;

        

        public ProcessBuilder AddSend(string channel, string message, IProcess nextProcess = null)
        {
            if (string.IsNullOrEmpty(channel)) throw new ArgumentException("Channel cannot be null or empty");
            if (message == null) throw new ArgumentNullException(nameof(message));

            _currentProcess = new SendProcess(channel, message, nextProcess ?? _currentProcess);
            return this;
        }

    

        public ProcessBuilder AddReceive(string channel, string filter, Action<string> handler)
        {
            if (string.IsNullOrEmpty(channel)) throw new ArgumentException("Channel cannot be null or empty");

            var currentBeforeReceive = _currentProcess;

            _currentProcess = new ReceiveProcess(
                channelName: channel,
                filter: filter ?? "",
                continuation: msg =>
                {
                    handler?.Invoke(msg ?? string.Empty);
                    return currentBeforeReceive;
                });
            return this;
        }

        public ProcessBuilder AddReceive(string channel, string filter, Func<string, IProcess> continuation)
        {
            if (string.IsNullOrEmpty(channel)) throw new ArgumentException("Channel cannot be null or empty");

            _currentProcess = new ReceiveProcess(
                channelName: channel,
                filter: filter ?? "",
                continuation: continuation);
            return this;
        }

        public ProcessBuilder AddParallel(params IProcess[] processes)
        {
            var validProcesses = processes?.Where(p => p != null).ToArray() ?? Array.Empty<IProcess>();
            _currentProcess = validProcesses.Length > 0
                ? new ParallelProcess(validProcesses.Concat(new[] { _currentProcess }).ToArray())
                : _currentProcess;
            return this;
        }

        public ProcessBuilder AddInactive()
        {
            _currentProcess = new InactiveProcess();
            return this;
        }

        public async Task ExecuteAsync()
        {
            if (_currentProcess == null)
                throw new InvalidOperationException("Process chain is empty");
            
            await _currentProcess.ExecuteAsync(_env);
        }


        // В класс ProcessBuilder добавьте:
        public ProcessBuilder AddReplication(Func<IProcess> processFactory)
        {
            _currentProcess = new ReplicationProcess(processFactory);
            return this;
        }

        public ProcessBuilder AddNewChannel(string name, ChannelStrategy strategy)
        {
            _currentProcess = new NewChannelProcess(name, strategy);
            return this;
        }




        public string GetProcessDiagram()
        {
            var visited = new HashSet<IProcess>();
            return BuildDiagram(_currentProcess, 0, visited);

            static string BuildDiagram(IProcess process, int indent, HashSet<IProcess> visited)
            {
                if (process == null) return new string(' ', indent * 2) + "null";
                if (visited.Contains(process)) return new string(' ', indent * 2) + "... (recursion)";

                visited.Add(process);

                var indentStr = new string(' ', indent * 2);

                try
                {
                    return process switch
                    {
                        SendProcess sp => $"{indentStr}Send({sp.ChannelName}, {sp.Message})\n" +
                                    BuildDiagram(sp.NextProcess, indent + 1, visited),
                        ReceiveProcess rp => $"{indentStr}Receive({rp._channelName})\n" +
                                        BuildDiagram(rp._continuation("sample"), indent + 1, visited),
                        ParallelProcess pp => $"{indentStr}Parallel[\n" +
                                        string.Join("\n", pp.GetProcesses().Select(p => BuildDiagram(p, indent + 1, visited))) +
                                        $"\n{indentStr}]",
                        _ => $"{indentStr}{process.GetType().Name}"
                    };
                }
                finally
                {
                    visited.Remove(process);
                }
            }
        }
    }
}