using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CustomerManagementApp.Data;
using CustomerManagementApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CustomerManagementApp.Services
{
    public interface ICustomerAnalyticsService
    {
        Task<CustomerAcquisitionData> GetCustomerAcquisitionDataAsync(int months = 12);
        Task<List<RegionalDistributionData>> GetCustomerRegionalDistributionAsync();
        Task<List<TopCustomerData>> GetTopCustomersByOrderValueAsync(int count = 10);
        Task<CustomerRetentionData> GetCustomerRetentionDataAsync(int months = 12);
        Task<CustomerActivityData> GetCustomerActivityTrendsAsync(int days = 30);
    }

    public class CustomerAnalyticsService : ICustomerAnalyticsService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CustomerAnalyticsService> _logger;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

        public CustomerAnalyticsService(
            ApplicationDbContext dbContext,
            IMemoryCache cache,
            ILogger<CustomerAnalyticsService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerAcquisitionData> GetCustomerAcquisitionDataAsync(int months = 12)
        {
            string cacheKey = $"CustomerAcquisition_{months}";
            if (_cache.TryGetValue(cacheKey, out CustomerAcquisitionData cachedData))
            {
                return cachedData;
            }

            try
            {
                _logger.LogInformation("Calculating customer acquisition data for the past {Months} months", months);

                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddMonths(-months);

                var customers = await _dbContext.Customers
                    .Where(c => c.CreatedDate >= startDate)
                    .OrderBy(c => c.CreatedDate)
                    .ToListAsync();

                var monthlyData = new List<MonthlyAcquisitionData>();
                var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);

                while (currentMonth <= endDate)
                {
                    var nextMonth = currentMonth.AddMonths(1);
                    var customersInMonth = customers.Count(c => 
                        c.CreatedDate >= currentMonth && c.CreatedDate < nextMonth);

                    monthlyData.Add(new MonthlyAcquisitionData
                    {
                        Month = currentMonth,
                        NewCustomers = customersInMonth
                    });

                    currentMonth = nextMonth;
                }

                var result = new CustomerAcquisitionData
                {
                    MonthlyData = monthlyData,
                    TotalNewCustomers = monthlyData.Sum(d => d.NewCustomers)
                };

                _cache.Set(cacheKey, result, _cacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating customer acquisition data");
                throw;
            }
        }

        public async Task<List<RegionalDistributionData>> GetCustomerRegionalDistributionAsync()
        {
            string cacheKey = "CustomerRegionalDistribution";
            if (_cache.TryGetValue(cacheKey, out List<RegionalDistributionData> cachedData))
            {
                return cachedData;
            }

            try
            {
                _logger.LogInformation("Calculating customer regional distribution");

                var customerAddresses = await _dbContext.Customers
                    .Include(c => c.Addresses)
                    .AsNoTracking()
                    .ToListAsync();

                var distribution = customerAddresses
                    .SelectMany(c => c.Addresses)
                    .GroupBy(a => string.IsNullOrEmpty(a.Region) ? a.State : a.Region)
                    .Select(g => new RegionalDistributionData
                    {
                        Region = g.Key,
                        CustomerCount = g.Count(),
                        Percentage = 0 // Calculate after getting total
                    })
                    .OrderByDescending(r => r.CustomerCount)
                    .ToList();

                var total = distribution.Sum(r => r.CustomerCount);
                if (total > 0)
                {
                    foreach (var region in distribution)
                    {
                        region.Percentage = (double)region.CustomerCount / total * 100;
                    }
                }

                _cache.Set(cacheKey, distribution, _cacheDuration);
                return distribution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating customer regional distribution");
                throw;
            }
        }

        public async Task<List<TopCustomerData>> GetTopCustomersByOrderValueAsync(int count = 10)
        {
            string cacheKey = $"TopCustomers_{count}";
            if (_cache.TryGetValue(cacheKey, out List<TopCustomerData> cachedData))
            {
                return cachedData;
            }

            try
            {
                _logger.LogInformation("Calculating top {Count} customers by order value", count);

                var customers = await _dbContext.Customers
                    .Include(c => c.Orders)
                    .AsNoTracking()
                    .ToListAsync();

                var topCustomers = customers
                    .Select(c => new TopCustomerData
                    {
                        CustomerId = c.Id,
                        CustomerName = $"{c.FirstName} {c.LastName}",
                        TotalSpent = c.Orders.Sum(o => o.OrderTotal),
                        OrderCount = c.Orders.Count,
                        AverageOrderValue = c.Orders.Any() ? c.Orders.Average(o => o.OrderTotal) : 0,
                        LastOrderDate = c.Orders.Any() ? c.Orders.Max(o => o.OrderDate) : (DateTime?)null
                    })
                    .OrderByDescending(c => c.TotalSpent)
                    .Take(count)
                    .ToList();

                _cache.Set(cacheKey, topCustomers, _cacheDuration);
                return topCustomers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating top customers by order value");
                throw;
            }
        }

        public async Task<CustomerRetentionData> GetCustomerRetentionDataAsync(int months = 12)
        {
            string cacheKey = $"CustomerRetention_{months}";
            if (_cache.TryGetValue(cacheKey, out CustomerRetentionData cachedData))
            {
                return cachedData;
            }

            try
            {
                _logger.LogInformation("Calculating customer retention data for the past {Months} months", months);

                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddMonths(-months);

                var customersWithOrders = await _dbContext.Customers
                    .Include(c => c.Orders)
                    .Where(c => c.CreatedDate <= endDate)
                    .AsNoTracking()
                    .ToListAsync();

                var monthlyData = new List<MonthlyRetentionData>();
                var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);

                while (currentMonth <= endDate)
                {
                    var nextMonth = currentMonth.AddMonths(1);
                    
                    // Customers who existed before this month
                    var existingCustomers = customersWithOrders
                        .Count(c => c.CreatedDate < currentMonth);
                    
                    // Customers who had an order in this month
                    var activeCustomers = customersWithOrders
                        .Count(c => c.CreatedDate < currentMonth && 
                                   c.Orders.Any(o => o.OrderDate >= currentMonth && o.OrderDate < nextMonth));
                    
                    var retentionRate = existingCustomers > 0 
                        ? (double)activeCustomers / existingCustomers * 100 
                        : 0;

                    monthlyData.Add(new MonthlyRetentionData
                    {
                        Month = currentMonth,
                        ExistingCustomers = existingCustomers,
                        ActiveCustomers = activeCustomers,
                        RetentionRate = retentionRate
                    });

                    currentMonth = nextMonth;
                }

                var result = new CustomerRetentionData
                {
                    MonthlyData = monthlyData,
                    AverageRetentionRate = monthlyData.Any() ? monthlyData.Average(d => d.RetentionRate) : 0
                };

                _cache.Set(cacheKey, result, _cacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating customer retention data");
                throw;
            }
        }

        public async Task<CustomerActivityData> GetCustomerActivityTrendsAsync(int days = 30)
        {
            string cacheKey = $"CustomerActivity_{days}";
            if (_cache.TryGetValue(cacheKey, out CustomerActivityData cachedData))
            {
                return cachedData;
            }

            try
            {
                _logger.LogInformation("Calculating customer activity trends for the past {Days} days", days);

                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-days);

                // Get orders in the date range
                var orders = await _dbContext.Orders
                    .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                    .AsNoTracking()
                    .ToListAsync();

                // Group orders by day
                var dailyOrders = new List<DailyActivityData>();
                var currentDay = startDate.Date;

                while (currentDay <= endDate.Date)
                {
                    var nextDay = currentDay.AddDays(1);
                    var ordersInDay = orders.Count(o => o.OrderDate >= currentDay && o.OrderDate < nextDay);
                    var revenueInDay = orders
                        .Where(o => o.OrderDate >= currentDay && o.OrderDate < nextDay)
                        .Sum(o => o.OrderTotal);
                    var uniqueCustomersInDay = orders
                        .Where(o => o.OrderDate >= currentDay && o.OrderDate < nextDay)
                        .Select(o => o.CustomerId)
                        .Distinct()
                        .Count();

                    dailyOrders.Add(new DailyActivityData
                    {
                        Date = currentDay,
                        OrderCount = ordersInDay,
                        Revenue = revenueInDay,
                        UniqueCustomers = uniqueCustomersInDay
                    });

                    currentDay = nextDay;
                }

                var result = new CustomerActivityData
                {
                    DailyData = dailyOrders,
                    TotalOrders = dailyOrders.Sum(d => d.OrderCount),
                    TotalRevenue = dailyOrders.Sum(d => d.Revenue),
                    UniqueCustomers = orders.Select(o => o.CustomerId).Distinct().Count()
                };

                _cache.Set(cacheKey, result, _cacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating customer activity trends");
                throw;
            }
        }
    }

    #region Data Transfer Objects

    public class CustomerAcquisitionData
    {
        public List<MonthlyAcquisitionData> MonthlyData { get; set; } = new List<MonthlyAcquisitionData>();
        public int TotalNewCustomers { get; set; }
    }

    public class MonthlyAcquisitionData
    {
        public DateTime Month { get; set; }
        public int NewCustomers { get; set; }
    }

    public class RegionalDistributionData
    {
        public string Region { get; set; }
        public int CustomerCount { get; set; }
        public double Percentage { get; set; }
    }

    public class TopCustomerData
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalSpent { get; set; }
        public int OrderCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public DateTime? LastOrderDate { get; set; }
    }

    public class CustomerRetentionData
    {
        public List<MonthlyRetentionData> MonthlyData { get; set; } = new List<MonthlyRetentionData>();
        public double AverageRetentionRate { get; set; }
    }

    public class MonthlyRetentionData
    {
        public DateTime Month { get; set; }
        public int ExistingCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public double RetentionRate { get; set; }
    }

    public class CustomerActivityData
    {
        public List<DailyActivityData> DailyData { get; set; } = new List<DailyActivityData>();
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int UniqueCustomers { get; set; }
    }

    public class DailyActivityData
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
        public int UniqueCustomers { get; set; }
    }

    #endregion
}
