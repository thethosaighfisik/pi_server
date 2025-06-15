using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PiServer.Models;
using System.Text.Json.Serialization;




namespace PiServer.Services
{
    public class ProcessService
    {
        private readonly EnvironmentManager _environment;
        private ProcessBuilder _builder;

        public ProcessService(EnvironmentManager environment)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _builder = new ProcessBuilder(_environment);
        }


        public async Task<ProcessResponse> AddSendAsync(SendRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                var channelName = request.Channel ?? "default_channel";
                var channel = _environment.GetOrCreateChannel(channelName);

                IProcess? continuation = null;
                if (request.Continuation != null)
                {
                    var continuationBuilder = new ProcessBuilder(_environment);
                    await BuildProcessFromRequest(continuationBuilder, request.Continuation);
                    continuation = continuationBuilder.GetCurrentProcess();
                }

                _builder.AddSend(channelName, request.Message ?? string.Empty, continuation);

                // Фактически отправляем сообщение в канал
                await _environment.TransferAsync(channelName, request.Message ?? string.Empty);

                return SuccessResponse(_builder.GetProcessDiagram());
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        


        public async Task<ProcessResponse> AddReceiveAsync(ReceiveRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {

                var channel = _environment.GetOrCreateChannel(request.Channel ?? "default_channel");

                Console.WriteLine($"[DEBUG] Messages in queue before DequeueAll: {channel.PeekAll().Count}");

                if (!request.WaitForMessage)
                {
                    var messages = channel.DequeueAll(request.Filter);
                    Console.WriteLine($"[DEBUG] Messages dequeued: {string.Join(", ", messages)}");
                    return new ProcessResponse
                    {
                        Diagram = $"ReceiveAll({request.Channel})",
                        Result = string.Join("; ", messages),
                        Success = true
                    };
                }

                // Если не нужно ждать — отдаем всё, что есть
                if (!request.WaitForMessage)
                {
                    var messages = channel.DequeueAll(request.Filter);
                    return new ProcessResponse
                    {
                        Diagram = $"ReceiveAll({request.Channel})",
                        Result = string.Join("; ", messages),
                        Success = true
                    };
                }

                // Стандартное поведение с ожиданием
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string? receivedMessage = await channel.ReceiveAsync(cts.Token);

                if (request.Filter != null && receivedMessage != null &&
                    !receivedMessage.Contains(request.Filter))
                {
                    receivedMessage = null;
                }

                return new ProcessResponse
                {
                    Diagram = $"Receive({request.Channel})",
                    Result = receivedMessage,
                    Success = true
                };
            }
            catch (OperationCanceledException)
            {
                return new ProcessResponse
                {
                    Diagram = $"Receive({request.Channel})",
                    Result = null,
                    Success = false,
                    Error = "Receive timeout"
                };
            }
            catch (Exception ex)
            {
                return new ProcessResponse
                {
                    Diagram = $"Receive({request.Channel})",
                    Result = null,
                    Success = false,
                    Error = ex.Message
                };
            }

        }











     public async Task<ProcessResponse> AddParallelAsync(ParallelRequest request)
{
    foreach (var process in request.Processes)
    {
        var json = process.Data.GetRawText();

        switch (process.Type?.ToLowerInvariant())
        {
            case "send":
                var send = System.Text.Json.JsonSerializer.Deserialize<SendRequest>(json);
                await AddSendAsync(send);  // вызов напрямую, без _processService
                break;

            case "receive":
                var receive = System.Text.Json.JsonSerializer.Deserialize<ReceiveRequest>(json);
                await AddReceiveAsync(receive);
                break;

            case "parallel":
                var parallel = System.Text.Json.JsonSerializer.Deserialize<ParallelRequest>(json);
                await AddParallelAsync(parallel);
                break;

            default:
                return new ProcessResponse
                {
                    Success = false,
                    Error = $"Unknown process type: {process.Type}"
                };
        }
    }

    return new ProcessResponse { Success = true, Result = "Parallel processes added" };
}




        public async Task<ProcessResponse> AddReplicationAsync(ReplicationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                var templateBuilder = new ProcessBuilder(_environment);
                await BuildProcessFromRequest(templateBuilder, request.ProcessTemplate);

                var templateProcess = templateBuilder.GetCurrentProcess();
                _builder.AddReplication(() => templateProcess);

                return SuccessResponse(_builder.GetProcessDiagram());
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public async Task<ProcessResponse> AddNewChannelAsync(NewChannelRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                _builder.AddNewChannel(
                    name: request.Name ?? "default_channel",
                    strategy: request.Strategy
                );
                return SuccessResponse(_builder.GetProcessDiagram());
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public async Task<ProcessResponse> ExecuteAsync()
        {
            try
            {
                await _builder.ExecuteAsync();
                return SuccessResponse(_builder.GetProcessDiagram(), "Execution completed");
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public ProcessResponse Reset()
        {
            _builder = new ProcessBuilder(_environment);
            return SuccessResponse(_builder.GetProcessDiagram(), "Process reset");
        }

        public ProcessResponse GetCurrentDiagram()
        {
            return SuccessResponse(_builder.GetProcessDiagram(), "Current diagram");
        }

        private async Task BuildProcessFromRequest(ProcessBuilder builder, ProcessRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            switch (request.Type?.ToLower())
            {
                case "send":
                    var sendData = JsonConvert.DeserializeObject<SendRequest>(request.Data.ToString() ?? "{}");
                    if (sendData != null) await AddSendAsync(sendData);
                    break;

                case "receive":
                    var receiveData = JsonConvert.DeserializeObject<ReceiveRequest>(request.Data.ToString() ?? "{}");
                    if (receiveData != null) await AddReceiveAsync(receiveData);
                    break;

                case "parallel":
                    var parallelData = JsonConvert.DeserializeObject<ParallelRequest>(request.Data.ToString() ?? "{}");
                    if (parallelData != null) await AddParallelAsync(parallelData);
                    break;

                case "replication":
                    var replicationData = JsonConvert.DeserializeObject<ReplicationRequest>(request.Data.ToString() ?? "{}");
                    if (replicationData != null) await AddReplicationAsync(replicationData);
                    break;

                case "newchannel":
                    var channelData = JsonConvert.DeserializeObject<NewChannelRequest>(request.Data.ToString() ?? "{}");
                    if (channelData != null) await AddNewChannelAsync(channelData);
                    break;

                default:
                    builder.AddInactive();
                    break;
            }
        }

        private ProcessResponse SuccessResponse(string diagram, string? result = null)
        {
            return new ProcessResponse
            {
                Diagram = diagram ?? string.Empty,
                Result = result,
                Success = true,
                Error = null
            };
        }

        private ProcessResponse ErrorResponse(string error, string? diagram = null)
        {
            return new ProcessResponse
            {
                Diagram = diagram ?? _builder.GetProcessDiagram(),
                Result = null,
                Success = false,
                Error = error ?? "Unknown error"
            };
        }
    }
}