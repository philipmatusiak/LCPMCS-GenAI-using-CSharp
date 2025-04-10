using System;

namespace CustomerManagementApp.Models
{
    /// <summary>
    /// Represents a customer address
    /// </summary>
    public class Address
    {
        /// <summary>
        /// Unique identifier for the address
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Foreign key to the customer this address belongs to
        /// </summary>
        public int CustomerId { get; set; }
        
        /// <summary>
        /// Street address including house/apartment number
        /// </summary>
        public string Street { get; set; }
        
        /// <summary>
        /// City name
        /// </summary>
        public string City { get; set; }
        
        /// <summary>
        /// State or province
        /// </summary>
        public string State { get; set; }
        
        /// <summary>
        /// Postal or ZIP code
        /// </summary>
        public string ZipCode { get; set; }
        
        /// <summary>
        /// Country name
        /// </summary>
        public string Country { get; set; }
        
        /// <summary>
        /// Indicates if this is the customer's primary address
        /// </summary>
        public bool IsPrimary { get; set; }
        
        /// <summary>
        /// Address type (Shipping, Billing, etc.)
        /// </summary>
        public string AddressType { get; set; }
        
        /// <summary>
        /// Reference to the customer this address belongs to
        /// </summary>
        public Customer Customer { get; set; }
        
        /// <summary>
        /// Returns the full address as a formatted string
        /// </summary>
        public string FullAddress => $"{Street}, {City}, {State} {ZipCode}, {Country}";
    }
}
