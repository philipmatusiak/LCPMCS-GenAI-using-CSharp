using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using CustomerManagementApp.Models;

namespace CustomerManagementApp.Repositories
{
    /// <summary>
    /// Original repository implementation with performance issues to optimize
    /// </summary>
    public class CustomerRepository
    {
        private readonly string _connectionString;
        
        public CustomerRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        public List<Customer> GetAllCustomers()
        {
            var customers = new List<Customer>();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new SqlCommand("SELECT * FROM Customers", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var customer = new Customer
                        {
                            Id = (int)reader["CustomerId"],
                            FirstName = reader["FirstName"].ToString(),
                            LastName = reader["LastName"].ToString(),
                            Email = reader["Email"].ToString(),
                            Phone = reader["Phone"]?.ToString(),
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            Status = reader["Status"].ToString()
                        };
                        
                        // Get customer addresses (separate query)
                        customer.Addresses = GetCustomerAddresses(customer.Id);
                        
                        // Get customer orders (separate query)
                        customer.Orders = GetCustomerOrders(customer.Id);
                        
                        customers.Add(customer);
                    }
                }
            }
            
            return customers;
        }
        
        public List<Customer> SearchCustomers(string term)
        {
            var customers = new List<Customer>();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new SqlCommand(
                    "SELECT * FROM Customers WHERE FirstName LIKE '%' + @term + '%' " +
                    "OR LastName LIKE '%' + @term + '%' " +
                    "OR Email LIKE '%' + @term + '%' " +
                    "OR Phone LIKE '%' + @term + '%'", connection))
                {
                    command.Parameters.AddWithValue("@term", term);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var customer = new Customer
                            {
                                Id = (int)reader["CustomerId"],
                                FirstName = reader["FirstName"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Email = reader["Email"].ToString(),
                                Phone = reader["Phone"]?.ToString(),
                                CreatedDate = (DateTime)reader["CreatedDate"],
                                Status = reader["Status"].ToString()
                            };
                            
                            // Get customer addresses (separate query)
                            customer.Addresses = GetCustomerAddresses(customer.Id);
                            
                            // Get customer orders (separate query)
                            customer.Orders = GetCustomerOrders(customer.Id);
                            
                            customers.Add(customer);
                        }
                    }
                }
            }
            
            return customers;
        }
        
        public Customer GetCustomerDetails(int id)
        {
            Customer customer = null;
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new SqlCommand("SELECT * FROM Customers WHERE CustomerId = @id", connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer = new Customer
                            {
                                Id = (int)reader["CustomerId"],
                                FirstName = reader["FirstName"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Email = reader["Email"].ToString(),
                                Phone = reader["Phone"]?.ToString(),
                                CreatedDate = (DateTime)reader["CreatedDate"],
                                Status = reader["Status"].ToString()
                            };
                        }
                    }
                }
                
                if (customer != null)
                {
                    // Get customer addresses (separate query)
                    customer.Addresses = GetCustomerAddresses(customer.Id);
                    
                    // Get customer orders (separate query)
                    customer.Orders = GetCustomerOrders(customer.Id);
                }
            }
            
            return customer;
        }
        
        private List<Address> GetCustomerAddresses(int customerId)
        {
            var addresses = new List<Address>();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new SqlCommand("SELECT * FROM Addresses WHERE CustomerId = @customerId", connection))
                {
                    command.Parameters.AddWithValue("@customerId", customerId);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            addresses.Add(new Address
                            {
                                Id = (int)reader["AddressId"],
                                CustomerId = customerId,
                                Street = reader["Street"].ToString(),
                                City = reader["City"].ToString(),
                                State = reader["State"].ToString(),
                                ZipCode = reader["ZipCode"].ToString(),
                                Country = reader["Country"].ToString(),
                                Region = reader["Region"]?.ToString(),
                                IsPrimary = (bool)reader["IsPrimary"],
                                AddressType = reader["AddressType"].ToString()
                            });
                        }
                    }
                }
            }
            
            return addresses;
        }
        
        private List<Order> GetCustomerOrders(int customerId)
        {
            var orders = new List<Order>();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new SqlCommand("SELECT * FROM Orders WHERE CustomerId = @customerId", connection))
                {
                    command.Parameters.AddWithValue("@customerId", customerId);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var order = new Order
                            {
                                Id = (int)reader["OrderId"],
                                CustomerId = customerId,
                                OrderDate = (DateTime)reader["OrderDate"],
                                OrderStatus = reader["OrderStatus"].ToString(),
                                OrderTotal = (decimal)reader["OrderTotal"]
                            };
                            
                            orders.Add(order);
                        }
                    }
                }
                
                // Get order items for each order (separate query)
                foreach (var order in orders)
                {
                    order.Items = GetOrderItems(order.Id);
                }
            }
            
            return orders;
        }
        
        private List<OrderItem> GetOrderItems(int orderId)
        {
            var items = new List<OrderItem>();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new SqlCommand("SELECT * FROM OrderItems WHERE OrderId = @orderId", connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new OrderItem
                            {
                                Id = (int)reader["OrderItemId"],
                                OrderId = orderId,
                                ProductId = (int)reader["ProductId"],
                                ProductName = reader["ProductName"].ToString(),
                                Quantity = (int)reader["Quantity"],
                                Price = (decimal)reader["Price"]
                            });
                        }
                    }
                }
            }
            
            return items;
        }
    }
}
