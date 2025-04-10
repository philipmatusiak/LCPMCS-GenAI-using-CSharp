using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CustomerManagementApp.Models;
using CustomerManagementApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CustomerManagementApp.Services
{
    public class CustomerSearchDto
    {
        public string? SearchTerm { get; set; }
        public string Status { get; set; } = "All";
        public string SortBy { get; set; } = "Name";
        public string SortDirection { get; set; } = "Ascending";
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class CustomerSearchResultDto
    {
        public List<CustomerDto> Customers { get; set; } = new List<CustomerDto>();
        public int TotalCount { get; set; }
        public int PageCount { get; set; }
        public CustomerSearchDto SearchParameters { get; set; }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public decimal TotalSpent { get; set; }
        public AddressDto PrimaryAddress { get; set; }
    }

    public class AddressDto
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string Country { get; set; }
        public string FormattedAddress => $"{Street}, {City}, {State} {ZipCode}, {Country}";
    }

    public interface ICustomerSearchService
    {
        Task<CustomerSearchResultDto> SearchCustomersAsync(CustomerSearchDto searchParams);
        Task<CustomerSearchResultDto> GetRecentCustomersAsync(int count = 10);
        Task<List<CustomerDto>> GetTopCustomersBySpendAsync(int count = 10);
    }

    public class CustomerSearchService : ICustomerSearchService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CustomerSearchService> _logger;

        public CustomerSearchService(
            ApplicationDbContext dbContext,
            IMemoryCache cache,
            ILogger<CustomerSearchService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerSearchResultDto> SearchCustomersAsync(CustomerSearchDto searchParams)
        {
            // Validate parameters
            if (searchParams.PageNumber < 1) searchParams.PageNumber = 1;
            if (searchParams.PageSize < 1) searchParams.PageSize = 10;
            if (searchParams.PageSize > 100) searchParams.PageSize = 100;

            // Try to get from cache first
            string cacheKey = $"CustomerSearch_{searchParams.SearchTerm}_{searchParams.Status}_{searchParams.SortBy}_{searchParams.SortDirection}_{searchParams.PageNumber}_{searchParams.PageSize}";
            
            if (_cache.TryGetValue(cacheKey, out CustomerSearchResultDto cachedResult))
            {
                _logger.LogInformation("Customer search results returned from cache");
                return cachedResult;
            }

            try
            {
                // Start with the base query
                var query = _dbContext.Customers.AsNoTracking();

                // Apply search term filter
                if (!string.IsNullOrWhiteSpace(searchParams.SearchTerm))
                {
                    var term = searchParams.SearchTerm.Trim().ToLower();
                    query = query.Where(c => 
                        c.FirstName.ToLower().Contains(term) ||
                        c.LastName.ToLower().Contains(term) ||
                        c.Email.ToLower().Contains(term) ||
                        c.Phone.Contains(term)
                    );
                }

                // Apply status filter
                if (searchParams.Status != "All")
                {
                    query = query.Where(c => c.Status == searchParams.Status);
                }

                // Get total count
                var totalCount = await query.CountAsync();
                
                // Calculate page count
                var pageCount = (int)Math.Ceiling(totalCount / (double)searchParams.PageSize);

                // Apply sorting
                IQueryable<Customer> sortedQuery = searchParams.SortBy switch
                {
                    "Name" => searchParams.SortDirection == "Ascending" 
                        ? query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
                        : query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName),
                    
                    "CreatedDate" => searchParams.SortDirection == "Ascending"
                        ? query.OrderBy(c => c.CreatedDate)
                        : query.OrderByDescending(c => c.CreatedDate),
                    
                    "LastOrder" => searchParams.SortDirection == "Ascending"
                        ? query.OrderBy(c => c.Orders.Max(o => (DateTime?)o.OrderDate))
                        : query.OrderByDescending(c => c.Orders.Max(o => (DateTime?)o.OrderDate)),
                    
                    _ => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
                };

                // Apply pagination
                var pagedCustomers = await sortedQuery
                    .Skip((searchParams.PageNumber - 1) * searchParams.PageSize)
                    .Take(searchParams.PageSize)
                    .Include(c => c.Addresses.Where(a => a.IsPrimary))
                    .Include(c => c.Orders)
                    .ToListAsync();

                // Map to DTOs
                var customerDtos = pagedCustomers.Select(c => new CustomerDto
                {
                    Id = c.Id,
                    FullName = $"{c.FirstName} {c.LastName}",
                    Email = c.Email,
                    Phone = c.Phone,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    LastOrderDate = c.Orders.Any() ? c.Orders.Max(o => (DateTime?)o.OrderDate) : null,
                    TotalSpent = c.Orders.Sum(o => o.OrderTotal),
                    PrimaryAddress = c.Addresses
                        .Where(a => a.IsPrimary)
                        .Select(a => new AddressDto
                        {
                            Street = a.Street,
                            City = a.City,
                            State = a.State,
                            ZipCode = a.ZipCode,
                            Country = a.Country
                        })
                        .FirstOrDefault()
                }).ToList();

                // Create result
                var result = new CustomerSearchResultDto
                {
                    Customers = customerDtos,
                    TotalCount = totalCount,
                    PageCount = pageCount,
                    SearchParameters = searchParams
                };

                // Cache the result (5 minute expiration)
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing customer search");
                throw;
            }
        }

        public async Task<CustomerSearchResultDto> GetRecentCustomersAsync(int count = 10)
        {
            var searchParams = new CustomerSearchDto
            {
                SortBy = "CreatedDate",
                SortDirection = "Descending",
                PageSize = count,
                Status = "All"
            };

            return await SearchCustomersAsync(searchParams);
        }

        public async Task<List<CustomerDto>> GetTopCustomersBySpendAsync(int count = 10)
        {
            // Try to get from cache first
            string cacheKey = $"TopCustomersBySpend_{count}";
            
            if (_cache.TryGetValue(cacheKey, out List<CustomerDto> cachedResult))
            {
                _logger.LogInformation("Top customers by spend returned from cache");
                return cachedResult;
            }

            try
            {
                var topCustomers = await _dbContext.Customers
                    .AsNoTracking()
                    .Include(c => c.Addresses.Where(a => a.IsPrimary))
                    .Include(c => c.Orders)
                    .OrderByDescending(c => c.Orders.Sum(o => o.OrderTotal))
                    .Take(count)
                    .ToListAsync();

                var result = topCustomers.Select(c => new CustomerDto
                {
                    Id = c.Id,
                    FullName = $"{c.FirstName} {c.LastName}",
                    Email = c.Email,
                    Phone = c.Phone,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    LastOrderDate = c.Orders.Any() ? c.Orders.Max(o => (DateTime?)o.OrderDate) : null,
                    TotalSpent = c.Orders.Sum(o => o.OrderTotal),
                    PrimaryAddress = c.Addresses
                        .Where(a => a.IsPrimary)
                        .Select(a => new AddressDto
                        {
                            Street = a.Street,
                            City = a.City,
                            State = a.State,
                            ZipCode = a.ZipCode,
                            Country = a.Country
                        })
                        .FirstOrDefault()
                }).ToList();

                // Cache the result (10 minute expiration)
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top customers by spend");
                throw;
            }
        }
    }
}
