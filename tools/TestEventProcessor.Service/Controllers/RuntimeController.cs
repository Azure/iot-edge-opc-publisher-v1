using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestEventProcessor.Businesslogic;
using TestEventProcessor.Service.Authentication;
using TestEventProcessor.Service.Enums;
using TestEventProcessor.Service.Models;

namespace TestEventProcessor.Service.Controllers
{
    /// <summary>
    /// Controller that provides access to the runtime of the validator to start and stop monitoring as well as requesting a status.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [BasicAuthentication]
    public class RuntimeController : ControllerBase
    {
        private readonly ILogger<RuntimeController> _logger;
        private readonly ISimpleValidator _validator;

        public RuntimeController(ILogger<RuntimeController> logger, ISimpleValidator validator)
        {
            _logger = logger;
            _validator = validator;
        }

        /// <summary>
        /// Start/Stop monitoring of messages that are being received by the IotHub. If the command is "Start", the configuration in
        /// the model needs to be passed. For "Stop", no additional information is required.
        /// </summary>
        /// <param name="command">The command to process.</param>
        /// <returns></returns>
        [HttpPut]
        public async Task Command(CommandModel command)
        {
            switch (command.CommandType)
            {
                case CommandEnum.Start:
                    await _validator.StartAsync(command.Configuration);
                    break;
                case CommandEnum.Stop:
                    await _validator.StopAsync();
                    break;
                default: throw new NotImplementedException($"Unknown command: {command.CommandType}");
            }
        }

        /// <summary>
        /// Get the current status of the monitoring.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public Task<ValidationStatus> Status()
        {
            return Task.FromResult(_validator.Status);
        }
    }
}
