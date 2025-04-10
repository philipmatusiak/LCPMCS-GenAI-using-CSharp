using System;
using System.Collections.Generic;

namespace CustomerManagementApp.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Status { get; set; }
        public Address Address { get; set; }
        public List<Order> Orders { get; set; } = new List<Order>();
        
        public string FullName => $"{FirstName} {LastName}";
        
        public int? Age
        {
            get
            {
                if (!DateOfBirth.HasValue)
                    return null;
                    
                var age = DateTime.Today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value.Date > DateTime.Today.AddYears(-age))
                    age--;
                    
                return age;
            }
        }
    }
    
    public class Address
    {
        public int Id { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public bool IsPrimary { get; set; }
        public string AddressType { get; set; }
        
        public string FullAddress => $"{Street}, {City}, {State} {ZipCode}";
    }
    
    public class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public string OrderStatus { get; set; }
        public decimal OrderTotal { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
    
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal LineTotal => Quantity * Price;
    }
}
