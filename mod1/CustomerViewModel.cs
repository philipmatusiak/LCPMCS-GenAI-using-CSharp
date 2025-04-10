using System;
using System.ComponentModel.DataAnnotations;

namespace CustomerManagementApp.ViewModels
{
    public class CustomerViewModel
    {
        public int ID { get; set; }
        
        [Required]
        [Display(Name = "First Name")]
        [StringLength(50)]
        public string FirstName { get; set; }
        
        [Required]
        [Display(Name = "Last Name")]
        [StringLength(50)]
        public string LastName { get; set; }
        
        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }
        
        [Phone]
        [StringLength(20)]
        public string Phone { get; set; }
        
        [Display(Name = "Created Date")]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; }
        
        [Required]
        public string Status { get; set; }
        
        public AddressViewModel Address { get; set; }
        
        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";
    }
    
    public class AddressViewModel
    {
        public int ID { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Street { get; set; }
        
        [Required]
        [StringLength(50)]
        public string City { get; set; }
        
        [Required]
        [StringLength(50)]
        public string State { get; set; }
        
        [Required]
        [Display(Name = "ZIP Code")]
        [StringLength(20)]
        public string ZipCode { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Country { get; set; }
        
        [Display(Name = "Primary Address")]
        public bool IsPrimary { get; set; }
        
        [Display(Name = "Full Address")]
        public string FullAddress => $"{Street}, {City}, {State} {ZipCode}, {Country}";
    }
}
