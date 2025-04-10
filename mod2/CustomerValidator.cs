using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CustomerManagementApp.Models;

namespace CustomerManagementApp.Validation
{
    public class CustomerValidator : IValidator<Customer>
    {
        public ValidationResult Validate(Customer customer)
        {
            var errors = new List<ValidationFailure>();
            
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }
            
            if (string.IsNullOrWhiteSpace(customer.FirstName))
            {
                errors.Add(new ValidationFailure("FirstName", "First name cannot be empty."));
            }
            
            if (string.IsNullOrWhiteSpace(customer.LastName))
            {
                errors.Add(new ValidationFailure("LastName", "Last name cannot be empty."));
            }
            
            if (!string.IsNullOrWhiteSpace(customer.Email) && 
                !Regex.IsMatch(customer.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                errors.Add(new ValidationFailure("Email", "Email format is invalid."));
            }
            
            if (customer.DateOfBirth.HasValue)
            {
                var age = DateTime.Today.Year - customer.DateOfBirth.Value.Year;
                if (customer.DateOfBirth.Value.Date > DateTime.Today.AddYears(-age))
                {
                    age--;
                }
                
                if (age < 18)
                {
                    errors.Add(new ValidationFailure("DateOfBirth", "Customer must be at least 18 years old."));
                }
                
                if (age > 120)
                {
                    errors.Add(new ValidationFailure("DateOfBirth", "Age cannot exceed 120 years."));
                }
            }
            
            return new ValidationResult(errors);
        }
    }
    
    public interface IValidator<T>
    {
        ValidationResult Validate(T entity);
    }
    
    public class ValidationResult
    {
        public List<ValidationFailure> Errors { get; }
        
        public bool IsValid => Errors.Count == 0;
        
        public ValidationResult(List<ValidationFailure> errors)
        {
            Errors = errors ?? new List<ValidationFailure>();
        }
    }
    
    public class ValidationFailure
    {
        public string PropertyName { get; }
        public string ErrorMessage { get; }
        
        public ValidationFailure(string propertyName, string errorMessage)
        {
            PropertyName = propertyName;
            ErrorMessage = errorMessage;
        }
    }
    
    public class ValidationException : Exception
    {
        public List<ValidationFailure> Errors { get; }
        
        public ValidationException(List<ValidationFailure> errors)
            : base("Validation failed. See Errors for details.")
        {
            Errors = errors;
        }
    }
}
