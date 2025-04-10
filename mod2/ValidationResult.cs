using System;
using System.Collections.Generic;

namespace CustomerManagementApp.Validation
{
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
