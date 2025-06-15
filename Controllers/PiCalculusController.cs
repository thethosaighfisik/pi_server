using Microsoft.AspNetCore.Mvc;
using PiServer.Models;
using PiServer.Services;
using System.Threading.Tasks;

namespace PiServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PiCalculusController : ControllerBase
    {
        private readonly ProcessService _processService;

        public PiCalculusController(ProcessService processService)
        {
            _processService = processService;
        }

        [HttpPost("send")]
        public async Task<ActionResult<ProcessResponse>> AddSend([FromBody] SendRequest request)
        {
            var response = await _processService.AddSendAsync(request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("receive")]
        public async Task<ActionResult<ProcessResponse>> AddReceive([FromBody] ReceiveRequest request)
        {
            var response = await _processService.AddReceiveAsync(request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("parallel")]
        public async Task<ActionResult<ProcessResponse>> AddParallel([FromBody] ParallelRequest request)
        {
            var response = await _processService.AddParallelAsync(request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("replication")]
        public async Task<ActionResult<ProcessResponse>> AddReplication([FromBody] ReplicationRequest request)
        {
            var response = await _processService.AddReplicationAsync(request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("newchannel")]
        public async Task<ActionResult<ProcessResponse>> AddNewChannel([FromBody] NewChannelRequest request)
        {
            if (request == null)
            {
                return BadRequest(new ProcessResponse 
                {
                    Error = "Request body is required",
                    Success = false
                });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ProcessResponse
                {
                    Error = "Channel name is required",
                    Success = false
                });
            }

            return await _processService.AddNewChannelAsync(request);
        }

        [HttpPost("execute")]
        public async Task<ActionResult<ProcessResponse>> Execute()
        {
            var response = await _processService.ExecuteAsync();
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("reset")]
        public ActionResult<ProcessResponse> Reset()
        {
            var response = _processService.Reset();
            return Ok(response);
        }

        [HttpGet("diagram")]
        public ActionResult<ProcessResponse> GetDiagram()
        {
            var response = _processService.GetCurrentDiagram();
            return Ok(response);
        }
    }
}