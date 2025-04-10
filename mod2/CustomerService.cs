using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CustomerManagementApp.Models;
using CustomerManagementApp.Repositories;
using CustomerManagementApp.Validation;
using Microsoft.Extensions.Logging;

namespace CustomerManagementApp.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _repository;
        private readonly ICustomerValidator _validator;
        private readonly IEmailService _emailService;
        private readonly ILogger<CustomerService> _logger;
        
        public CustomerService(
            ICustomerRepository repository, 
            ICustomerValidator validator,
            IEmailService emailService,
            ILogger<CustomerService> logger = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger;
        }
        
        public Customer GetCustomerById(int id)
        {
            if (id <= 0)
                throw new ArgumentException("ID must be positive", nameof(id));
                
            _logger?.LogInformation("Getting customer with ID: {CustomerId}", id);
            return _repository.GetById(id);
        }
        
        public IEnumerable<Customer> SearchCustomers(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<Customer>();
                
            _logger?.LogInformation("Searching customers with term: {SearchTerm}", searchTerm);
            return _repository.Search(searchTerm);
        }
        
        public int CreateCustomer(Customer customer)
        {
            if (customer == null)
                throw new ArgumentNullException(nameof(customer));
                
            var validationResult = _validator.Validate(customer);
            if (!validationResult.IsValid)
                throw new ValidationException(validationResult.Errors);
                
            var existingCustomer = _repository.GetByEmail(customer.Email);
            if (existingCustomer != null)
                throw new DuplicateCustomerException("A customer with this email already exists");
                
            var id = _repository.Create(customer);
            
            _emailService.SendWelcomeEmail(customer.Email, customer.FirstName);
            
            return id;
        }
        
        public bool UpdateCustomer(Customer customer)
        {
            if (customer == null)
                throw new ArgumentNullException(nameof(customer));
                
            if (customer.Id <= 0)
                throw new ArgumentException("ID must be positive", nameof(customer));
                
            var validationResult = _validator.Validate(customer);
            if (!validationResult.IsValid)
                throw new ValidationException(validationResult.Errors);
                
            var existingCustomer = _repository.GetById(customer.Id);
            if (existingCustomer == null)
                return false;
                
            return _repository.Update(customer);
        }
        
        public bool DeleteCustomer(int id)
        {
            if (id <= 0)
                throw new ArgumentException("ID must be positive", nameof(id));
                
            var customer = _repository.GetById(id);
            if (customer == null)
                return false;
                
            return _repository.Delete(id);
        }
    }

    public class DuplicateCustomerException : Exception
    {
        public DuplicateCustomerException(string message)
            : base(message)
        {
        }
    }
}
