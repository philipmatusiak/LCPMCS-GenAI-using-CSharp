using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CustomerManagementApp.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CustomerManagementApp.Services
{
    /// <summary>
    /// Optimized implementation following GitHub Copilot suggestions
    /// </summary>
    public class CustomerProcessorOptimized
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
        
        public CustomerProcessorOptimized(IMemoryCache cache = null)
        {
            _cache = cache;
        }
        
        // Optimized version using LINQ and parallel processing
        public List<CustomerSummary> ProcessCustomerData(List<Customer> customers)
        {
            // Early return for empty lists to avoid unnecessary processing
            if (customers == null || customers.Count == 0)
            {
                return new List<CustomerSummary>();
            }
            
            // Check cache first if caching is enabled
            if (_cache != null)
            {
                string cacheKey = $"CustomerSummaries_{string.Join("_", customers.Select(c => c.Id))}";
                if (_cache.TryGetValue(cacheKey, out List<CustomerSummary> cachedResult))
                {
                    return cachedResult;
                }
            }
            
            // Use parallel processing for better performance on large datasets
            var results = customers.AsParallel()
                .Select(customer => 
                {
                    // Calculate total orders value efficiently using LINQ
                    decimal totalValue = customer.Orders
                        .SelectMany(order => order.Items)
                        .Sum(item => item.Price * item.Quantity);
                        
                    // Create and return the summary object
                    return new CustomerSummary
                    {
                        CustomerId = customer.Id,
                        Name = $"{customer.FirstName} {customer.LastName}",
                        Email = customer.Email,
                        TotalSpent = totalValue,
                        IsPremium = totalValue > 5000 || customer.Orders.Count > 10,
                        LastOrderDate = customer.Orders.Any() 
                            ? customer.Orders.Max(o => o.OrderDate) 
                            : null
                    };
                })
                .OrderByDescending(summary => summary.TotalSpent)
                .ToList();
            
            // Store in cache if caching is enabled
            if (_cache != null)
            {
                string cacheKey = $"CustomerSummaries_{string.Join("_", customers.Select(c => c.Id))}";
                _cache.Set(cacheKey, results, _cacheDuration);
            }
            
            return results;
        }
        
        // Optimized version with LINQ
        public Dictionary<string, int> GetCustomerCountByRegion(List<Customer> customers)
        {
            if (customers == null || customers.Count == 0)
            {
                return new Dictionary<string, int>();
            }
            
            return customers
                .GroupBy(c => c.Address?.Region ?? "Unknown")
                .ToDictionary(
                    group => group.Key,
                    group => group.Count()
                );
        }
        
        // Async version for improved responsiveness
        public async Task<List<CustomerSummary>> ProcessCustomerDataAsync(List<Customer> customers)
        {
            return await Task.Run(() => ProcessCustomerData(customers));
        }
        
        // Enhanced version with error handling and more features
        public List<CustomerSummary> ProcessCustomerDataEnhanced(
            List<Customer> customers,
            decimal premiumThreshold = 5000,
            int premiumOrderCount = 10,
            bool includePremiumDetails = false)
        {
            if (customers == null || customers.Count == 0)
            {
                return new List<CustomerSummary>();
            }
            
            try
            {
                var results = customers.AsParallel()
                    .Select(customer => 
                    {
                        // Pre-compute item sums per order for reuse
                        var orderTotals = customer.Orders
                            .ToDictionary(
                                order => order,
                                order => order.Items.Sum(item => item.Price * item.Quantity)
                            );
                        
                        // Get total spent across all orders
                        decimal totalValue = orderTotals.Values.Sum();
                        
                        bool isPremium = totalValue > premiumThreshold || customer.Orders.Count > premiumOrderCount;
                        
                        var summary = new CustomerSummary
                        {
                            CustomerId = customer.Id,
                            Name = $"{customer.FirstName} {customer.LastName}",
                            Email = customer.Email,
                            TotalSpent = totalValue,
                            IsPremium = isPremium,
                            LastOrderDate = customer.Orders.Any() 
                                ? customer.Orders.Max(o => o.OrderDate) 
                                : null
                        };
                        
                        if (includePremiumDetails && isPremium)
                        {
                            summary.MostValuableOrders = orderTotals
                                .OrderByDescending(pair => pair.Value)
                                .Take(3)
                                .Select(pair => new { OrderId = pair.Key.Id, Value = pair.Value })
                                .ToList();
                        }
                        
                        return summary;
                    })
                    .OrderByDescending(summary => summary.TotalSpent)
                    .ToList();
                
                return results;
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error processing customer data: {ex.Message}");
                throw;
            }
        }
    }
    
    // Enhanced CustomerSummary with more features
    public class CustomerSummaryEnhanced : CustomerSummary
    {
        public List<dynamic> MostValuableOrders { get; set; }
        public string CustomerSegment => DetermineSegment();
        
        private string DetermineSegment()
        {
            if (TotalSpent > 10000) return "VIP";
            if (TotalSpent > 5000) return "Premium";
            if (TotalSpent > 1000) return "Regular";
            return "New";
        }
    }
}
