using System;
using System.Collections.Generic;
using System.Linq;
using CustomerManagementApp.Models;

namespace CustomerManagementApp.Services
{
    /// <summary>
    /// Original implementation with performance issues
    /// </summary>
    public class CustomerProcessor
    {
        public List<CustomerSummary> ProcessCustomerData(List<Customer> customers)
        {
            List<CustomerSummary> results = new List<CustomerSummary>();
            
            foreach (var customer in customers)
            {
                // Calculate total orders value
                decimal totalValue = 0;
                foreach (var order in customer.Orders)
                {
                    foreach (var item in order.Items)
                    {
                        totalValue += item.Price * item.Quantity;
                    }
                }
                
                // Check if customer is premium based on order history
                bool isPremium = false;
                if (totalValue > 5000 || customer.Orders.Count > 10)
                {
                    isPremium = true;
                }
                
                // Get most recent order date
                DateTime? lastOrderDate = null;
                foreach (var order in customer.Orders)
                {
                    if (lastOrderDate == null || order.OrderDate > lastOrderDate)
                    {
                        lastOrderDate = order.OrderDate;
                    }
                }
                
                // Create summary
                CustomerSummary summary = new CustomerSummary();
                summary.CustomerId = customer.Id;
                summary.Name = customer.FirstName + " " + customer.LastName;
                summary.Email = customer.Email;
                summary.TotalSpent = totalValue;
                summary.IsPremium = isPremium;
                summary.LastOrderDate = lastOrderDate;
                
                results.Add(summary);
            }
            
            // Sort by total spent
            for (int i = 0; i < results.Count; i++)
            {
                for (int j = i + 1; j < results.Count; j++)
                {
                    if (results[j].TotalSpent > results[i].TotalSpent)
                    {
                        var temp = results[i];
                        results[i] = results[j];
                        results[j] = temp;
                    }
                }
            }
            
            return results;
        }
        
        public Dictionary<string, int> GetCustomerCountByRegion(List<Customer> customers)
        {
            Dictionary<string, int> regionCounts = new Dictionary<string, int>();
            
            foreach (var customer in customers)
            {
                string region = customer.Address?.Region ?? "Unknown";
                
                if (regionCounts.ContainsKey(region))
                {
                    regionCounts[region] += 1;
                }
                else
                {
                    regionCounts.Add(region, 1);
                }
            }
            
            return regionCounts;
        }
    }
    
    // Supporting classes
    public class CustomerSummary
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public decimal TotalSpent { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? LastOrderDate { get; set; }
    }
    
    public static class CustomerExtensions
    {
        public static Address GetPrimaryAddress(this Customer customer)
        {
            return customer.Addresses.FirstOrDefault(a => a.IsPrimary) ?? customer.Addresses.FirstOrDefault();
        }
    }
}
