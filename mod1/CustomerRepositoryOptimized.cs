using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using CustomerManagementApp.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CustomerManagementApp.Repositories
{
    /// <summary>
    /// Optimized repository implementation with performance improvements
    /// </summary>
    public class CustomerRepositoryOptimized : ICustomerRepository
    {
        private readonly string _connectionString;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CustomerRepositoryOptimized> _logger;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);
        
        public CustomerRepositoryOptimized(
            string connectionString,
            IMemoryCache cache,
            ILogger<CustomerRepositoryOptimized> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            // Try to get from cache first
            string cacheKey = "AllCustomers";
            if (_cache.TryGetValue(cacheKey, out List<Customer> cachedCustomers))
            {
                _logger.LogInformation("Returning all customers from cache");
                return cachedCustomers;
            }
            
            _logger.LogInformation("Fetching all customers from database");
            
            var customers = new List<Customer>();
            
            // SQL query with joins to retrieve customers with addresses and orders in a single round trip
            const string sql = @"
                SELECT c.*, a.*, o.*, oi.*
                FROM Customers c
                LEFT JOIN Addresses a ON c.CustomerId = a.CustomerId
                LEFT JOIN Orders o ON c.CustomerId = o.CustomerId
                LEFT JOIN OrderItems oi ON o.OrderId = oi.OrderId
                ORDER BY c.CustomerId, a.AddressId, o.OrderId, oi.OrderItemId";
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    using (var command = new SqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var customerLookup = new Dictionary<int, Customer>();
                            var orderLookup = new Dictionary<int, Order>();
                            
                            while (await reader.ReadAsync())
                            {
                                // Get or create customer
                                var customerId = reader.GetInt32(reader.GetOrdinal("CustomerId"));
                                
                                if (!customerLookup.TryGetValue(customerId, out var customer))
                                {
                                    customer = new Customer
                                    {
                                        Id = customerId,
                                        FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                                        LastName = reader.GetString(reader.GetOrdinal("LastName")),
                                        Email = reader.GetString(reader.GetOrdinal("Email")),
                                        Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? null : reader.GetString(reader.GetOrdinal("Phone")),
                                        CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                        Status = reader.GetString(reader.GetOrdinal("Status")),
                                        Addresses = new List<Address>(),
                                        Orders = new List<Order>()
                                    };
                                    
                                    customerLookup[customerId] = customer;
                                    customers.Add(customer);
                                }
                                
                                // Add address if exists and not already added
                                if (!reader.IsDBNull(reader.GetOrdinal("AddressId")))
                                {
                                    var addressId = reader.GetInt32(reader.GetOrdinal("AddressId"));
                                    
                                    // Check if this address is already added to the customer
                                    if (!customer.Addresses.Any(a => a.Id == addressId))
                                    {
                                        var address = new Address
                                        {
                                            Id = addressId,
                                            CustomerId = customerId,
                                            Street = reader.GetString(reader.GetOrdinal("Street")),
                                            City = reader.GetString(reader.GetOrdinal("City")),
                                            State = reader.GetString(reader.GetOrdinal("State")),
                                            ZipCode = reader.GetString(reader.GetOrdinal("ZipCode")),
                                            Country = reader.GetString(reader.GetOrdinal("Country")),
                                            Region = reader.IsDBNull(reader.GetOrdinal("Region")) ? null : reader.GetString(reader.GetOrdinal("Region")),
                                            IsPrimary = reader.GetBoolean(reader.GetOrdinal("IsPrimary")),
                                            AddressType = reader.GetString(reader.GetOrdinal("AddressType"))
                                        };
                                        
                                        customer.Addresses.Add(address);
                                    }
                                }
                                
                                // Add order if exists and not already added
                                if (!reader.IsDBNull(reader.GetOrdinal("OrderId")))
                                {
                                    var orderId = reader.GetInt32(reader.GetOrdinal("OrderId"));
                                    
                                    if (!orderLookup.TryGetValue(orderId, out var order))
                                    {
                                        order = new Order
                                        {
                                            Id = orderId,
                                            CustomerId = customerId,
                                            OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                                            OrderStatus = reader.GetString(reader.GetOrdinal("OrderStatus")),
                                            OrderTotal = reader.GetDecimal(reader.GetOrdinal("OrderTotal")),
                                            Items = new List<OrderItem>()
                                        };
                                        
                                        orderLookup[orderId] = order;
                                        
                                        // Add to customer's orders if not already there
                                        if (!customer.Orders.Any(o => o.Id == orderId))
                                        {
                                            customer.Orders.Add(order);
                                        }
                                    }
                                    
                                    // Add order item if exists
                                    if (!reader.IsDBNull(reader.GetOrdinal("OrderItemId")))
                                    {
                                        var orderItemId = reader.GetInt32(reader.GetOrdinal("OrderItemId"));
                                        
                                        // Check if this item is already added to the order
                                        if (!order.Items.Any(i => i.Id == orderItemId))
                                        {
                                            var orderItem = new OrderItem
                                            {
                                                Id = orderItemId,
                                                OrderId = orderId,
                                                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                                ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                                                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                                                Price = reader.GetDecimal(reader.GetOrdinal("Price"))
                                            };
                                            
                                            order.Items.Add(orderItem);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Cache the result
                _cache.Set(cacheKey, customers, _cacheDuration);
                
                _logger.LogInformation("Retrieved {Count} customers from database", customers.Count);
                
                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all customers");
                throw;
            }
        }
        
        public async Task<List<Customer>> SearchCustomersAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return await GetAllCustomersAsync();
            }
            
            // Try to get from cache first
            string cacheKey = $"SearchCustomers_{term}";
            if (_cache.TryGetValue(cacheKey, out List<Customer> cachedResults))
            {
                _logger.LogInformation("Returning search results from cache for term: {Term}", term);
                return cachedResults;
            }
            
            _logger.LogInformation("Searching customers with term: {Term}", term);
            
            // Prepare the search parameter with proper wildcards
            var searchParam = $"%{term}%";
            
            var customers = new List<Customer>();
            
            // SQL query with joins and indexable search conditions
            const string sql = @"
                SELECT c.*, a.*, o.*, oi.*
                FROM Customers c
                LEFT JOIN Addresses a ON c.CustomerId = a.CustomerId
                LEFT JOIN Orders o ON c.CustomerId = o.CustomerId
                LEFT JOIN OrderItems oi ON o.OrderId = oi.OrderId
                WHERE c.FirstName LIKE @term 
                   OR c.LastName LIKE @term
                   OR c.Email LIKE @term
                   OR c.Phone LIKE @term
                ORDER BY c.LastName, c.FirstName, a.AddressId, o.OrderId, oi.OrderItemId";
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@term", SqlDbType.NVarChar).Value = searchParam;
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var customerLookup = new Dictionary<int, Customer>();
                            var orderLookup = new Dictionary<int, Order>();
                            
                            while (await reader.ReadAsync())
                            {
                                // Get or create customer
                                var customerId = reader.GetInt32(reader.GetOrdinal("CustomerId"));
                                
                                if (!customerLookup.TryGetValue(customerId, out var customer))
                                {
                                    customer = new Customer
                                    {
                                        Id = customerId,
                                        FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                                        LastName = reader.GetString(reader.GetOrdinal("LastName")),
                                        Email = reader.GetString(reader.GetOrdinal("Email")),
                                        Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? null : reader.GetString(reader.GetOrdinal("Phone")),
                                        CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                        Status = reader.GetString(reader.GetOrdinal("Status")),
                                        Addresses = new List<Address>(),
                                        Orders = new List<Order>()
                                    };
                                    
                                    customerLookup[customerId] = customer;
                                    customers.Add(customer);
                                }
                                
                                // Add address if exists and not already added
                                if (!reader.IsDBNull(reader.GetOrdinal("AddressId")))
                                {
                                    var addressId = reader.GetInt32(reader.GetOrdinal("AddressId"));
                                    
                                    // Check if this address is already added to the customer
                                    if (!customer.Addresses.Any(a => a.Id == addressId))
                                    {
                                        var address = new Address
                                        {
                                            Id = addressId,
                                            CustomerId = customerId,
                                            Street = reader.GetString(reader.GetOrdinal("Street")),
                                            City = reader.GetString(reader.GetOrdinal("City")),
                                            State = reader.GetString(reader.GetOrdinal("State")),
                                            ZipCode = reader.GetString(reader.GetOrdinal("ZipCode")),
                                            Country = reader.GetString(reader.GetOrdinal("Country")),
                                            Region = reader.IsDBNull(reader.GetOrdinal("Region")) ? null : reader.GetString(reader.GetOrdinal("Region")),
                                            IsPrimary = reader.GetBoolean(reader.GetOrdinal("IsPrimary")),
                                            AddressType = reader.GetString(reader.GetOrdinal("AddressType"))
                                        };
                                        
                                        customer.Addresses.Add(address);
                                    }
                                }
                                
                                // Add order if exists and not already added
                                if (!reader.IsDBNull(reader.GetOrdinal("OrderId")))
                                {
                                    var orderId = reader.GetInt32(reader.GetOrdinal("OrderId"));
                                    
                                    if (!orderLookup.TryGetValue(orderId, out var order))
                                    {
                                        order = new Order
                                        {
                                            Id = orderId,
                                            CustomerId = customerId,
                                            OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                                            OrderStatus = reader.GetString(reader.GetOrdinal("OrderStatus")),
                                            OrderTotal = reader.GetDecimal(reader.GetOrdinal("OrderTotal")),
                                            Items = new List<OrderItem>()
                                        };
                                        
                                        orderLookup[orderId] = order;
                                        
                                        // Add to customer's orders if not already there
                                        if (!customer.Orders.Any(o => o.Id == orderId))
                                        {
                                            customer.Orders.Add(order);
                                        }
                                    }
                                    
                                    // Add order item if exists
                                    if (!reader.IsDBNull(reader.GetOrdinal("OrderItemId")))
                                    {
                                        var orderItemId = reader.GetInt32(reader.GetOrdinal("OrderItemId"));
                                        
                                        // Check if this item is already added to the order
                                        if (!order.Items.Any(i => i.Id == orderItemId))
                                        {
                                            var orderItem = new OrderItem
                                            {
                                                Id = orderItemId,
                                                OrderId = orderId,
                                                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                                ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                                                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                                                Price = reader.GetDecimal(reader.GetOrdinal("Price"))
                                            };
                                            
                                            order.Items.Add(orderItem);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Cache the result
                _cache.Set(cacheKey, customers, _cacheDuration);
                
                _logger.LogInformation("Found {Count} customers matching search term: {Term}", customers.Count, term);
                
                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with term: {Term}", term);
                throw;
            }
        }
        
        public async Task<Customer> GetCustomerDetailsAsync(int id)
        {
            // Try to get from cache first
            string cacheKey = $"Customer_{id}";
            if (_cache.TryGetValue(cacheKey, out Customer cachedCustomer))
            {
                _logger.LogInformation("Returning customer details from cache for ID: {CustomerId}", id);
                return cachedCustomer;
            }
            
            _logger.LogInformation("Fetching customer details for ID: {CustomerId}", id);
            
            Customer customer = null;
            
            // SQL query with joins to retrieve customer with addresses and orders in a single round trip
            const string sql = @"
                SELECT c.*, a.*, o.*, oi.*
                FROM Customers c
                LEFT JOIN Addresses a ON c.CustomerId = a.CustomerId
                LEFT JOIN Orders o ON c.CustomerId = o.CustomerId
                LEFT JOIN OrderItems oi ON o.OrderId = oi.OrderId
                WHERE c.CustomerId = @id
                ORDER BY a.AddressId, o.OrderId, oi.OrderItemId";
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var orderLookup = new Dictionary<int, Order>();
                            
                            while (await reader.ReadAsync())
                            {
                                // Create customer if first row
                                if (customer == null)
                                {
                                    customer = new Customer
                                    {
                                        Id = id,
                                        FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                                        LastName = reader.GetString(reader.GetOrdinal("LastName")),
                                        Email = reader.GetString(reader.GetOrdinal("Email")),
                                        Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? null : reader.GetString(reader.GetOrdinal("Phone")),
                                        CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                        Status = reader.GetString(reader.GetOrdinal("Status")),
                                        Addresses = new List<Address>(),
                                        Orders = new List<Order>()
                                    };
                                }
                                
                                // Add address if exists and not already added
                                if (!reader.IsDBNull(reader.GetOrdinal("AddressId")))
                                {
                                    var addressId = reader.GetInt32(reader.GetOrdinal("AddressId"));
                                    
                                    // Check if this address is already added to the customer
                                    if (!customer.Addresses.Any(a => a.Id == addressId))
                                    {
                                        var address = new Address
                                        {
                                            Id = addressId,
                                            CustomerId = id,
                                            Street = reader.GetString(reader.GetOrdinal("Street")),
                                            City = reader.GetString(reader.GetOrdinal("City")),
                                            State = reader.GetString(reader.GetOrdinal("State")),
                                            ZipCode = reader.GetString(reader.GetOrdinal("ZipCode")),
                                            Country = reader.GetString(reader.GetOrdinal("Country")),
                                            Region = reader.IsDBNull(reader.GetOrdinal("Region")) ? null : reader.GetString(reader.GetOrdinal("Region")),
                                            IsPrimary = reader.GetBoolean(reader.GetOrdinal("IsPrimary")),
                                            AddressType = reader.GetString(reader.GetOrdinal("AddressType"))
                                        };
                                        
                                        customer.Addresses.Add(address);
                                    }
                                }
                                
                                // Add order if exists and not already added
                                if (!reader.IsDBNull(reader.GetOrdinal("OrderId")))
                                {
                                    var orderId = reader.GetInt32(reader.GetOrdinal("OrderId"));
                                    
                                    if (!orderLookup.TryGetValue(orderId, out var order))
                                    {
                                        order = new Order
                                        {
                                            Id = orderId,
                                            CustomerId = id,
                                            OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                                            OrderStatus = reader.GetString(reader.GetOrdinal("OrderStatus")),
                                            OrderTotal = reader.GetDecimal(reader.GetOrdinal("OrderTotal")),
                                            Items = new List<OrderItem>()
                                        };
                                        
                                        orderLookup[orderId] = order;
                                        customer.Orders.Add(order);
                                    }
                                    
                                    // Add order item if exists
                                    if (!reader.IsDBNull(reader.GetOrdinal("OrderItemId")))
                                    {
                                        var orderItemId = reader.GetInt32(reader.GetOrdinal("OrderItemId"));
                                        
                                        // Check if this item is already added to the order
                                        if (!order.Items.Any(i => i.Id == orderItemId))
                                        {
                                            var orderItem = new OrderItem
                                            {
                                                Id = orderItemId,
                                                OrderId = orderId,
                                                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                                ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                                                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                                                Price = reader.GetDecimal(reader.GetOrdinal("Price"))
                                            };
                                            
                                            order.Items.Add(orderItem);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (customer != null)
                {
                    // Cache the result
                    _cache.Set(cacheKey, customer, _cacheDuration);
                    
                    _logger.LogInformation("Retrieved customer details for ID: {CustomerId}", id);
                }
                else
                {
                    _logger.LogWarning("Customer with ID {CustomerId} not found", id);
                }
                
                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer details for ID: {CustomerId}", id);
                throw;
            }
        }
    }
}
