using OT.Assessment.App.Messaging;
using OT.Assessment.App.Models;
using OT.Assessment.App.Repository;
using Swashbuckle.AspNetCore.Annotations;
namespace OT.Assessment.App.Controllers
{
  
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerController : ControllerBase
    {
        private readonly IPlayerReadRepository _reads;
        private readonly IPublishQueue _queue;

        public PlayerController(IPublishQueue queue, IPlayerReadRepository reads)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _reads = reads ?? throw new ArgumentNullException(nameof(reads));
        }

        //POST api/player/casinowager
        /// <summary>
        /// Receives player casino wager events to publish to the local RabbitMQ queue.
        /// </summary>
        [HttpPost("CasinoWager")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> PostCasinoWager([FromBody] CasinoWagerMessage message, CancellationToken ct)
        {
            await _queue.EnqueueAsync(message, ct);
            return Ok();
        }

        //GET api/player/{playerId}/casino
        /// <summary>
        /// Returns a paginated list of the latest casino wagers for a specific player (accountId).
        /// </summary>
        [HttpGet("{playerId:guid}/casino")]
        [SwaggerResponse(StatusCodes.Status200OK, type: typeof(PaginatedResponse<PlayerWagerListItem>))]
        public async Task<IActionResult> GetCasinoForPlayer([FromRoute] Guid playerId, [FromQuery] int pageSize = 10, [FromQuery] int page = 1, CancellationToken ct = default)
        {
            if (pageSize <= 0 || pageSize > 200) pageSize = 10;
            if (page <= 0) page = 1;

            var result = await _reads.GetPlayerWagersPagedAsync(playerId, page, pageSize, ct);
            return Ok(result);
        }


        //GET api/player/topSpenders?count=10
        //    /// <summary>
        /// Returns the top {count} players based on total spending. Highest to Lowest.
        /// </summary>
        [HttpGet("TopSpenders")]
        [SwaggerResponse(StatusCodes.Status200OK, type: typeof(IEnumerable<TopSpenderDto>))]
        public async Task<IActionResult> GetTopSpenders([FromQuery] int count = 10, CancellationToken ct = default)
        {
            if (count <= 0) count = 10;
            if (count > 1000) count = 1000;

            var result = await _reads.GetTopSpendersAsync(count, ct);
            return Ok(result);
        }
    }
}
