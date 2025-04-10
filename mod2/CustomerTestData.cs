using System;
using System.Collections.Generic;
using CustomerManagementApp.Models;

namespace CustomerManagementApp.Tests
{
    public static class CustomerTestData
    {
        public static IEnumerable<object[]> ValidCustomers()
        {
            yield return new object[] 
            { 
                new Customer 
                { 
                    Id = 1, 
                    FirstName = "John", 
                    LastName = "Doe", 
                    Email = "john.doe@example.com",
                    Phone = "555-123-4567",
                    Address = new Address
                    {
                        Street = "123 Main St",
                        City = "Anytown",
                        State = "NY",
                        ZipCode = "12345"
                    }
                }
            };
            
            yield return new object[] 
            { 
                new Customer 
                { 
                    Id = 2, 
                    FirstName = "Jane", 
                    LastName = "Smith", 
                    Email = "jane.smith@example.com",
                    Phone = "555-987-6543",
                    Address = new Address
                    {
                        Street = "456 Oak Ave",
                        City = "Somewhere",
                        State = "CA",
                        ZipCode = "98765"
                    }
                }
            };
        }
        
        public static IEnumerable<object[]> InvalidCustomers()
        {
            // Null customer
            yield return new object[] { null };
            
            // Missing required fields
            yield return new object[] 
            { 
                new Customer { Id = 3, FirstName = "", LastName = "Blank" }
            };
            
            yield return new object[] 
            { 
                new Customer { Id = 4, FirstName = "Missing", LastName = "" }
            };
            
            // Invalid email format
            yield return new object[] 
            { 
                new Customer 
                { 
                    Id = 5, 
                    FirstName = "Bad", 
                    LastName = "Email", 
                    Email = "not-an-email"
                }
            };
        }
        
        public static IEnumerable<object[]> CustomerUpdateScenarios()
        {
            // Successful update
            yield return new object[]
            {
                new Customer 
                { 
                    Id = 1, 
                    FirstName = "John", 
                    LastName = "Doe", 
                    Email = "john.doe@example.com" 
                },
                true,
                null
            };
            
            // Non-existent customer
            yield return new object[]
            {
                new Customer 
                { 
                    Id = 999, 
                    FirstName = "Nonexistent", 
                    LastName = "Customer", 
                    Email = "nonexistent@example.com" 
                },
                false,
                null
            };
            
            // Invalid data
            yield return new object[]
            {
                new Customer 
                { 
                    Id = 2, 
                    FirstName = "", 
                    LastName = "Empty", 
                    Email = "empty@example.com" 
                },
                false,
                typeof(ArgumentException)
            };
        }
        
        public static IEnumerable<object[]> SearchScenarios()
        {
            // Search term with results
            var customers = new List<Customer>
            {
                new Customer { Id = 1, FirstName = "John", LastName = "Doe" },
                new Customer { Id = 2, FirstName = "Johnny", LastName = "Smith" }
            };
            
            yield return new object[]
            {
                "John",
                customers,
                2
            };
            
            // Search term with no results
            yield return new object[]
            {
                "XYZ",
                new List<Customer>(),
                0
            };
            
            // Empty search term
            yield return new object[]
            {
                "",
                new List<Customer>(),
                0
            };
        }
    }
}
