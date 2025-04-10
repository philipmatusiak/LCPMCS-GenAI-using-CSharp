using System;
using System.Threading;
using System.Threading.Tasks;
using CustomerManagementApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CustomerManagementApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerSearchService _searchService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            ICustomerSearchService searchService,
            ILogger<CustomersController> logger)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Searches for customers based on the provided search term
        /// </summary>
        /// <param name="searchTerm">Optional term to search by name, email, or address</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (max 100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paged list of customers matching the search criteria</returns>
        /// <response code="200">Returns the matching customers</response>
        /// <response code="400">If the parameters are invalid</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet("search")]
        [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<CustomerDto>>> SearchCustomers(
            [FromQuery] string? searchTerm,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                return BadRequest("Page number must be greater than 0");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest("Page size must be between 1 and 100");
            }

            try
            {
                var result = await _searchService.SearchCustomersAsync(
                    searchTerm ?? string.Empty, 
                    pageNumber, 
                    pageSize, 
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching for customers");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                                  "An error occurred while processing your request");
            }
        }
    }
}
