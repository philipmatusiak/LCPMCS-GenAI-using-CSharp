using System;
using System.Collections.Generic;

namespace CustomerManagementApp.Models
{
    /// <summary>
    /// Represents a customer in the system
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// Unique identifier for the customer
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Customer's first name
        /// </summary>
        public string FirstName { get; set; }
        
        /// <summary>
        /// Customer's last name
        /// </summary>
        public string LastName { get; set; }
        
        /// <summary>
        /// Customer's email address
        /// </summary>
        public string Email { get; set; }
        
        /// <summary>
        /// Customer's phone number
        /// </summary>
        public string Phone { get; set; }
        
        /// <summary>
        /// Date when the customer was created in the system
        /// </summary>
        public DateTime CreatedDate { get; set; }
        
        /// <summary>
        /// Current status of the customer (Active, Inactive)
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// Collection of addresses associated with this customer
        /// </summary>
        public List<Address> Addresses { get; set; } = new List<Address>();
        
        /// <summary>
        /// Collection of orders placed by this customer
        /// </summary>
        public List<Order> Orders { get; set; } = new List<Order>();
        
        /// <summary>
        /// Returns the full name of the customer
        /// </summary>
        public string FullName => $"{FirstName} {LastName}";
        
        /// <summary>
        /// Gets the customer's primary address if one exists
        /// </summary>
        public Address PrimaryAddress => 
            Addresses.Find(a => a.IsPrimary) ?? 
            (Addresses.Count > 0 ? Addresses[0] : null);
        
        /// <summary>
        /// Gets the date of the most recent order
        /// </summary>
        public DateTime? LastOrderDate => 
            Orders.Count > 0 ? 
            Orders.MaxBy(o => o.OrderDate)?.OrderDate : 
            null;
    }
}
