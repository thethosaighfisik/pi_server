// using Microsoft.AspNetCore.Mvc;
// using PiServer.Models;
// using PiServer.Services;
// using System.Threading.Tasks;

// namespace PiServer.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     public class PiCalculusController : ControllerBase
//     {
//         private readonly ProcessService _processService;

//         public PiCalculusController(ProcessService processService)
//         {
//             _processService = processService;
//         }

//         [HttpPost("send")]
//         public async Task<ActionResult<ProcessResponse>> AddSend([FromBody] SendRequest request)
//         {
//             var response = await _processService.AddSendAsync(request);
//             return response.Success ? Ok(response) : BadRequest(response);
//         }

//         [HttpPost("receive")]
//         public async Task<ActionResult<ProcessResponse>> AddReceive([FromBody] ReceiveRequest request)
//         {
//             var response = await _processService.AddReceiveAsync(request);
//             return response.Success ? Ok(response) : BadRequest(response);
//         }

//         [HttpPost("parallel")]
//         public async Task<ActionResult<ProcessResponse>> AddParallel([FromBody] ParallelRequest request)
//         {
//             var response = await _processService.AddParallelAsync(request);
//             return response.Success ? Ok(response) : BadRequest(response);
//         }

//         [HttpPost("replication")]
//         public async Task<ActionResult<ProcessResponse>> AddReplication([FromBody] ReplicationRequest request)
//         {
//             var response = await _processService.AddReplicationAsync(request);
//             return response.Success ? Ok(response) : BadRequest(response);
//         }

//         [HttpPost("newchannel")]
//         public async Task<ActionResult<ProcessResponse>> AddNewChannel([FromBody] NewChannelRequest request)
//         {
//             if (request == null)
//             {
//                 return BadRequest(new ProcessResponse 
//                 {
//                     Error = "Request body is required",
//                     Success = false
//                 });
//             }

//             if (string.IsNullOrWhiteSpace(request.Name))
//             {
//                 return BadRequest(new ProcessResponse
//                 {
//                     Error = "Channel name is required",
//                     Success = false
//                 });
//             }

//             return await _processService.AddNewChannelAsync(request);
//         }

//         [HttpPost("execute")]
//         public async Task<ActionResult<ProcessResponse>> Execute()
//         {
//             var response = await _processService.ExecuteAsync();
//             return response.Success ? Ok(response) : BadRequest(response);
//         }

//         [HttpPost("reset")]
//         public ActionResult<ProcessResponse> Reset()
//         {
//             var response = _processService.Reset();
//             return Ok(response);
//         }

//         [HttpGet("diagram")]
//         public ActionResult<ProcessResponse> GetDiagram()
//         {
//             var response = _processService.GetCurrentDiagram();
//             return Ok(response);
//         }
//     }
// }


// using Microsoft.AspNetCore.Mvc;
// using PiServer.Models;
// using PiServer.Services;
// using System.Text.Json;

// namespace PiServer.Controllers
// {
//     [ApiController]
//     [Route("api/process")]
//     public class ProcessController : ControllerBase
//     {
//         private readonly EnvironmentManager _environment;
//         private ProcessBuilder _builder;

//         public ProcessController(EnvironmentManager environment)
//         {
//             _environment = environment;
//             _builder = new ProcessBuilder(environment);
//         }

//         [HttpGet("diagram")]
//         public IActionResult GetDiagram()
//         {
//             return Ok(_builder.GetProcessDiagram());
//         }

//         [HttpPost("send")]
//         public async Task<IActionResult> AddSend([FromBody] JsonElement request)
//         {
//             try
//             {
//                 var channel = request.GetProperty("channel").GetString();
//                 var message = request.GetProperty("message").GetString();

//                 if (request.TryGetProperty("continuation", out var continuationElement))
//                 {
//                     var continuationBuilder = new ProcessBuilder(_environment);
//                     await BuildProcessFromJson(continuationBuilder, continuationElement);
//                     _builder.AddSend(channel, message, continuationBuilder.GetCurrentProcess());
//                 }
//                 else
//                 {
//                     _builder.AddSend(channel, message);
//                 }

//                 return Ok(_builder.GetProcessDiagram());
//             }
//             catch (Exception ex)
//             {
//                 return BadRequest($"Invalid request format: {ex.Message}");
//             }
//         }

//         [HttpPost("receive")]
//         public async Task<IActionResult> AddReceive([FromBody] JsonElement request)
//         {
//             try
//             {
//                 var channel = request.GetProperty("channel").GetString();
//                 var filter = request.TryGetProperty("filter", out var filterElement)
//                     ? filterElement.GetString()
//                     : "";

//                 if (request.TryGetProperty("continuation", out var continuationElement))
//                 {
//                     var continuationBuilder = new ProcessBuilder(_environment);
//                     await BuildProcessFromJson(continuationBuilder, continuationElement);

//                     _builder.AddReceive(channel, filter, msg =>
//                     {
//                         Console.WriteLine($"Received: {msg}");
//                         return continuationBuilder.GetCurrentProcess();
//                     });
//                 }
//                 else
//                 {
//                     _builder.AddReceive(channel, filter, msg =>
//                         Console.WriteLine($"Received: {msg}"));
//                 }

//                 return Ok(_builder.GetProcessDiagram());
//             }
//             catch (Exception ex)
//             {
//                 return BadRequest($"Invalid request format: {ex.Message}");
//             }
//         }

//         [HttpPost("parallel")]
//         public async Task<IActionResult> AddParallel([FromBody] JsonElement request)
//         {
//             try
//             {
//                 var processes = new List<IProcess>();
//                 foreach (var processElement in request.GetProperty("processes").EnumerateArray())
//                 {
//                     var processBuilder = new ProcessBuilder(_environment);
//                     await BuildProcessFromJson(processBuilder, processElement);
//                     processes.Add(processBuilder.GetCurrentProcess());
//                 }

//                 _builder.AddParallel(processes.ToArray());
//                 return Ok(_builder.GetProcessDiagram());
//             }
//             catch (Exception ex)
//             {
//                 return BadRequest($"Invalid request format: {ex.Message}");
//             }
//         }

//         private async Task BuildProcessFromJson(ProcessBuilder builder, JsonElement element)
//         {
//             var type = element.GetProperty("type").GetString().ToLower();

//             switch (type)
//             {
//                 case "send":
//                     var channel = element.GetProperty("channel").GetString();
//                     var message = element.GetProperty("message").GetString();

//                     if (element.TryGetProperty("continuation", out var continuationElement))
//                     {
//                         var continuationBuilder = new ProcessBuilder(_environment);
//                         await BuildProcessFromJson(continuationBuilder, continuationElement);
//                         builder.AddSend(channel, message, continuationBuilder.GetCurrentProcess());
//                     }
//                     else
//                     {
//                         builder.AddSend(channel, message);
//                     }
//                     break;

//                 case "receive":
//                     var recvChannel = element.GetProperty("channel").GetString();
//                     var filter = element.TryGetProperty("filter", out var filterElement)
//                         ? filterElement.GetString()
//                         : "";

//                     if (element.TryGetProperty("continuation", out var recvContinuation))
//                     {
//                         var continuationBuilder = new ProcessBuilder(_environment);
//                         await BuildProcessFromJson(continuationBuilder, recvContinuation);

//                         builder.AddReceive(recvChannel, filter, msg =>
//                         {
//                             Console.WriteLine($"Received: {msg}");
//                             return continuationBuilder.GetCurrentProcess();
//                         });
//                     }
//                     else
//                     {
//                         builder.AddReceive(recvChannel, filter, msg =>
//                             Console.WriteLine($"Received: {msg}"));
//                     }
//                     break;

//                 default:
//                     builder.AddInactive();
//                     break;
//             }
//         }

//         [HttpPost("execute")]
//         public async Task<IActionResult> Execute()
//         {
//             await _builder.ExecuteAsync();
//             return Ok("Execution completed");
//         }

//         [HttpPost("reset")]
//         public IActionResult Reset()
//         {
//             _builder = new ProcessBuilder(_environment);
//             return Ok("Process reset");
//         }
//         [HttpGet("logs")]
//         public IActionResult GetExecutionLogs()
//         {
//             return Ok(_builder.GetExecutionLog());
//         }

//         [HttpPost("execute/verbose")]
//         public async Task<IActionResult> ExecuteWithLogging()
//         {
//             await _builder.ExecuteAsync();
//             return Ok(new {
//                 Diagram = _builder.GetProcessDiagram(),
//                 Logs = _builder.GetExecutionLog()
//             });
//         }
//     }
// }

// using Microsoft.AspNetCore.Mvc;
// using PiServer.Models;
// using PiServer.Services;
// using System;
// using System.Collections.Generic;
// using System.Text;
// using System.Threading.Tasks;

// namespace PiServer.Controllers
// {
//     [ApiController]
//     [Route("api/process")]
//     public class ProcessController : ControllerBase
//     {
//         private readonly EnvironmentManager _environment;

//         // Храним текущий процесс и логи
//         private static IProcess? _currentProcess = null;
//         private static StringBuilder _executionLog = new StringBuilder();

//         public ProcessController(EnvironmentManager environment)
//         {
//             _environment = environment;
//         }

//         // Принимаем единый DTO, который описывает любой процесс (send, receive, parallel и т.д.)
//         [HttpPost("process")]
//         public IActionResult PostProcess([FromBody] ProcessDto dto)
//         {
//             var process = ConvertDtoToProcess(dto);
//             _currentProcess = process;
//             return Ok(process.ToString());
//         }

//         [HttpPost("execute")]
//         public async Task<IActionResult> Execute()
//         {
//             if (_currentProcess == null)
//                 return BadRequest("Process chain is empty");

//             _executionLog.Clear();
//             _executionLog.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] EXECUTION STARTED");

//             await _currentProcess.ExecuteAsync(_environment);

//             _executionLog.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] EXECUTION COMPLETED");

//             return Ok(new
//             {
//                 Diagram = _currentProcess.ToString(),
//                 Logs = _executionLog.ToString()
//             });
//         }

//         [HttpGet("diagram")]
//         public IActionResult GetDiagram()
//         {
//             if (_currentProcess == null)
//                 return Ok(new { Diagram = "No process defined." });

//             return Ok(new { Diagram = _currentProcess.ToString() });
//         }

//         // Преобразование DTO в объект процесса IProcess
//         private IProcess ConvertDtoToProcess(ProcessDto dto)
//         {
//             switch (dto.Type.ToLower())
//             {
//                 case "send":
//                     return new SendProcess(dto.Channel, dto.Message, null);

//                 case "receive":
//                     // В данном примере continuation просто завершает цепочку
//                     Func<string, IProcess> continuation = msg => new InactiveProcess();
//                     return new ReceiveProcess(dto.Channel, dto.Filter, continuation);

//                 case "parallel":
//                     var innerProcesses = new List<IProcess>();
//                     if (dto.Processes != null)
//                     {
//                         foreach (var innerDto in dto.Processes)
//                         {
//                             innerProcesses.Add(ConvertDtoToProcess(innerDto));
//                         }
//                     }
//                     return new ParallelProcess(innerProcesses.ToArray());

//                 default:
//                     return new InactiveProcess();
//             }
//         }
//     }

//     // Универсальный DTO для всех типов процессов, с рекурсией для parallel
//     public class ProcessDto
//     {
//         public string Type { get; set; } = string.Empty;
//         public string Channel { get; set; } = string.Empty;
//         public string Message { get; set; } = string.Empty;
//         public string Filter { get; set; } = string.Empty;
//         public List<ProcessDto>? Processes { get; set; }  // Для parallel процессов
//     }
// }


using Microsoft.AspNetCore.Mvc;
using PiServer.Models;
using PiServer.Services;
using System.Text;
using System.Collections.Concurrent;

namespace PiServer.Controllers
{
    [ApiController]
    [Route("api/builder")]
    public class BuilderController : ControllerBase
    {
        private readonly EnvironmentManager _environment;

        private static ProcessBuilder _builder;
        private static readonly StringBuilder _log = new();

        public BuilderController(EnvironmentManager environment)
        {
            _environment = environment;
            _builder ??= new ProcessBuilder(environment);
        }

        [HttpPost("reset")]
        public IActionResult Reset()
        {
            _builder = new ProcessBuilder(_environment);
            return Ok("Состояние сброшено.");
        }






        [HttpGet("logs")]
public IActionResult GetLogs()
{
    return Ok(new
    {
        logs = _environment.MessageLogs
    });
}



        [HttpPost("send")]
        public IActionResult AddSend([FromBody] SendDto dto)
        {
            _environment.LogMessage($"Received AddSend: channel={dto.Channel}, message={dto.Message}");
            _environment.GetOrCreateChannel(dto.Channel, ChannelStrategy.PassiveEnvironment);


            if (dto.Continuation != null)
            {
                var continuation = ProcessBuilder.BuildFromDto(dto.Continuation);
                _builder.AddSend(dto.Channel, dto.Message, continuation);
            }
            else
            {
                _builder.AddSend(dto.Channel, dto.Message);
            }


            return Ok("Send добавлен.");
        }

        [HttpPost("receive")]
        public IActionResult AddReceive([FromBody] ReceiveDto dto)
        {
            _environment.GetOrCreateChannel(dto.Channel, ChannelStrategy.PassiveEnvironment);


            if (dto.Continuation != null)
            {
                var continuation = ProcessBuilder.BuildFromDto(dto.Continuation);
                _builder.AddReceive(dto.Channel, dto.Filter, _ => continuation);
            }
            else
            {
                _builder.AddReceive(dto.Channel, dto.Filter, msg =>
                {
                    Console.WriteLine($"Получено: {msg}");
                    return new InactiveProcess();
                });
            }


            return Ok("Receive добавлен.");
        }

        [HttpPost("parallel")]
        public IActionResult AddParallel([FromBody] ParallelDto dto)
        {
            var processes = dto.Processes.Select(ProcessBuilder.BuildFromDto).ToArray();
            _builder.AddParallel(processes);

            return Ok("Parallel добавлен.");
        }

        [HttpPost("inactive")]
        public IActionResult AddInactive()
        {
            _builder.AddInactive();
            return Ok("Inactive добавлен.");
        }

        [HttpGet("diagram")]
        public IActionResult GetDiagram()
        {
            return Ok(_builder.GetProcessDiagram());
        }



        [HttpPost("execute")]
public async Task<IActionResult> Execute()
{
    try
    {
        _log.Clear();
        _log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] START");

        await _builder.ExecuteAsync();

        _log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] END");

        return Ok(new
        {
            Diagram = _builder.GetProcessDiagram(),
            Log = _log.ToString()
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            Error = ex.Message,
            StackTrace = ex.StackTrace
        });
    }
}

        // [HttpPost("execute")]
        // public async Task<IActionResult> Execute()
        // {
        //     _log.Clear();
        //     _log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] START");

        //     await _builder.ExecuteAsync();

        //     _log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] END");

        //     return Ok(new
        //     {
        //         Diagram = _builder.GetProcessDiagram(),
        //         Log = _log.ToString()
        //     });
        // }

        // private IProcess ConvertDtoToProcess(ProcessDto dto)
        // {
        //     switch (dto.Type.ToLower())
        //     {
        //         case "send":
        //             return new SendProcess(dto.Channel, dto.Message, null);

        //         case "receive":
        //             return new ReceiveProcess(dto.Channel, dto.Filter, _ => new InactiveProcess());

        //         case "parallel":
        //             var inner = dto.Processes?.Select(ConvertDtoToProcess).ToArray() ?? [];
        //             return new ParallelProcess(inner);

        //         default:
        //             return new InactiveProcess();
        //     }
        // }
    }

    // DTOs
    public class SendDto
    {
        public string Channel { get; set; } = "";
        public string Message { get; set; } = "";
        public ProcessDto? Continuation { get; set; }
    }

    public class ReceiveDto
    {
        public string Channel { get; set; } = "";
        public string Filter { get; set; } = "";
        public ProcessDto? Continuation { get; set; }
    }

    public class ParallelDto
    {
        public List<ProcessDto> Processes { get; set; } = new();
    }

    public class ProcessDto
    {
        public string Type { get; set; } = "";
        public string Channel { get; set; } = "";
        public string Message { get; set; } = "";
        public string Filter { get; set; } = "";
        public List<ProcessDto>? Processes { get; set; }
        public ProcessDto? Continuation { get; set; }

    }
}
