using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiServer.Services;
using PiServer.Controllers;

namespace PiServer.Models
{
    public class ProcessBuilder
    {
        private IProcess _currentProcess;
        private readonly EnvironmentManager _env;
        private readonly StringBuilder _executionLog = new();
        
        

        public ProcessBuilder(EnvironmentManager env)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _currentProcess = new InactiveProcess();

            _env.MessageLogged += message => _executionLog.AppendLine(message);
        }

        
        
        public IProcess GetCurrentProcess() => _currentProcess;


        public static IProcess BuildFromDto(ProcessDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            switch (dto.Type.ToLower())
            {
                case "send":
                    return new SendProcess(
                        channelName: dto.Channel,
                        message: dto.Message,
                        nextProcess: dto.Continuation != null ? BuildFromDto(dto.Continuation) : new InactiveProcess());

                case "receive":
                    return new ReceiveProcess(
                        channelName: dto.Channel,
                        filter: dto.Filter,
                        continuation: _ => dto.Continuation != null ? BuildFromDto(dto.Continuation) : new InactiveProcess());

                case "parallel":
                    var processes = dto.Processes?.Select(BuildFromDto).ToArray() ?? Array.Empty<IProcess>();
                    return new ParallelProcess(processes);

                case "inactive":
                    return new InactiveProcess();

                case "replication":
                    return new ReplicationProcess(() => BuildFromDto(dto.Continuation ?? new ProcessDto { Type = "inactive" }));

                default:
                    throw new InvalidOperationException($"Unknown process type: {dto.Type}");
            }
        }

        // public ProcessBuilder AddSend(string channel, string message, IProcess? nextProcess = null)
        // {
        //     if (string.IsNullOrEmpty(channel)) throw new ArgumentException("Channel cannot be null or empty");
        //     if (message == null) throw new ArgumentNullException(nameof(message));

        //     _currentProcess = new SendProcess(channel, message, nextProcess ?? _currentProcess);
        //     return this;
        // }


        public void AddSend(string channel, string message, IProcess? nextProcess = null)
        {
            var send = new SendProcess(channel, message, nextProcess ?? new InactiveProcess());

            if (_currentProcess is InactiveProcess)
                _currentProcess = send;
            else
                _currentProcess = new ParallelProcess(new[] { _currentProcess, send });
        }


        public ProcessBuilder AddReceive(string channel, string? filter, Action<string> handler)
        {
            if (string.IsNullOrEmpty(channel)) throw new ArgumentException("Channel cannot be null or empty");

            var currentBeforeReceive = _currentProcess;

            _currentProcess = new ReceiveProcess(
                channelName: channel,
                filter: filter ?? "",
                continuation: msg =>
                {
                    handler?.Invoke(msg ?? string.Empty);
                    _executionLog.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] RECV from {channel}: {msg}");
                    return currentBeforeReceive;
                });
            return this;
        }

        public ProcessBuilder AddReceive(string channel, string? filter, Func<string, IProcess> continuation)
        {
            if (string.IsNullOrEmpty(channel)) throw new ArgumentException("Channel cannot be null or empty");

            _currentProcess = new ReceiveProcess(
                channelName: channel,
                filter: filter ?? "",
                continuation: msg =>
                {
                    _executionLog.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] RECV from {channel}: {msg}");
                    return continuation(msg);
                });
            return this;
        }

        public ProcessBuilder AddParallel(params IProcess[]? processes)
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

        // Добавляем недостающие методы
        public ProcessBuilder AddReplication(Func<IProcess> processFactory)
        {
            if (processFactory == null) throw new ArgumentNullException(nameof(processFactory));
            _currentProcess = new ReplicationProcess(processFactory);
            return this;
        }

        public ProcessBuilder AddNewChannel(string name, ChannelStrategy strategy)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Channel name cannot be null or empty");
            _env.GetOrCreateChannel(name, strategy);
            return this;
        }

        public async Task ExecuteAsync()
        {
            if (_currentProcess == null)
                throw new InvalidOperationException("Process chain is empty");
            
            _executionLog.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] EXECUTION STARTED");
            await _currentProcess.ExecuteAsync(_env);
            _executionLog.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] EXECUTION COMPLETED");
        }

        public string GetExecutionLog()
        {
            return _executionLog.ToString();
        }

        public string GetProcessDiagram()
        {
            var visited = new HashSet<IProcess>();
            return BuildDiagram(_currentProcess, 0, visited);

            static string BuildDiagram(IProcess? process, int indent, HashSet<IProcess> visited)
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