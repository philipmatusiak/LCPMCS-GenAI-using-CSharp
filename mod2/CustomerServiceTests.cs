using System;
using System.Collections.Generic;
using System.Linq;
using CustomerManagementApp.Models;
using CustomerManagementApp.Repositories;
using CustomerManagementApp.Services;
using CustomerManagementApp.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace CustomerManagementApp.Tests
{
    public class CustomerServiceTests
    {
        private readonly CustomerService _sut;
        private readonly Mock<ICustomerRepository> _mockRepository;
        private readonly Mock<ICustomerValidator> _mockValidator;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ILogger<CustomerService>> _mockLogger;
        
        public CustomerServiceTests()
        {
            _mockRepository = new Mock<ICustomerRepository>();
            _mockValidator = new Mock<ICustomerValidator>();
            _mockEmailService = new Mock<IEmailService>();
            _mockLogger = new Mock<ILogger<CustomerService>>();
            
            _sut = new CustomerService(
                _mockRepository.Object,
                _mockValidator.Object,
                _mockEmailService.Object,
                _mockLogger.Object
            );
        }
        
        [Fact]
        public void GetCustomerById_WithPositiveId_ReturnsCustomer()
        {
            // Arrange
            var expectedCustomer = new Customer { Id = 1, FirstName = "John", LastName = "Doe" };
            _mockRepository.Setup(repo => repo.GetById(1)).Returns(expectedCustomer);
            
            // Act
            var result = _sut.GetCustomerById(1);
            
            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedCustomer);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GetCustomerById_WithNonPositiveId_ThrowsArgumentException(int invalidId)
        {
            // Act & Assert
            Action act = () => _sut.GetCustomerById(invalidId);
            
            act.Should().Throw<ArgumentException>()
                .WithMessage("*ID must be positive*")
                .WithParameterName("id");
        }
        
        [Fact]
        public void GetCustomerById_WhenCustomerDoesNotExist_ReturnsNull()
        {
            // Arrange
            _mockRepository.Setup(repo => repo.GetById(It.IsAny<int>())).Returns((Customer)null);
            
            // Act
            var result = _sut.GetCustomerById(42);
            
            // Assert
            result.Should().BeNull();
        }
        
        [Fact]
        public void CreateCustomer_WithValidCustomer_ReturnsNewId()
        {
            // Arrange
            var customer = new Customer { 
                FirstName = "Jane", 
                LastName = "Smith", 
                Email = "jane.smith@example.com" 
            };
            
            _mockValidator.Setup(v => v.Validate(customer))
                .Returns(new ValidationResult(new List<ValidationFailure>()));
                
            _mockRepository.Setup(r => r.GetByEmail(customer.Email))
                .Returns((Customer)null);
                
            _mockRepository.Setup(r => r.Create(customer)).Returns(42);
            
            // Act
            var result = _sut.CreateCustomer(customer);
            
            // Assert
            result.Should().Be(42);
            _mockEmailService.Verify(e => e.SendWelcomeEmail(customer.Email, customer.FirstName), Times.Once);
        }
        
        [Fact]
        public void CreateCustomer_WithNullCustomer_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => _sut.CreateCustomer(null);
            
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("customer");
        }
        
        [Fact]
        public void CreateCustomer_WithInvalidCustomer_ThrowsValidationException()
        {
            // Arrange
            var customer = new Customer { FirstName = "Jane", LastName = "" };
            var validationFailures = new List<ValidationFailure> 
            { 
                new ValidationFailure("LastName", "Last name cannot be empty") 
            };
            
            _mockValidator.Setup(v => v.Validate(customer))
                .Returns(new ValidationResult(validationFailures));
            
            // Act & Assert
            Action act = () => _sut.CreateCustomer(customer);
            
            act.Should().Throw<ValidationException>()
                .Which.Errors.Should().ContainSingle(e => e.PropertyName == "LastName");
        }
        
        [Fact]
        public void CreateCustomer_WithDuplicateEmail_ThrowsDuplicateCustomerException()
        {
            // Arrange
            var customer = new Customer { 
                FirstName = "Jane", 
                LastName = "Smith", 
                Email = "jane.smith@example.com" 
            };
            
            _mockValidator.Setup(v => v.Validate(customer))
                .Returns(new ValidationResult(new List<ValidationFailure>()));
                
            _mockRepository.Setup(r => r.GetByEmail(customer.Email))
                .Returns(new Customer { Id = 5, Email = customer.Email });
            
            // Act & Assert
            Action act = () => _sut.CreateCustomer(customer);
            
            act.Should().Throw<DuplicateCustomerException>()
                .WithMessage("*email already exists*");
        }
        
        [Fact]
        public void UpdateCustomer_WithValidCustomer_ReturnsTrue()
        {
            // Arrange
            var customer = new Customer { 
                Id = 1, 
                FirstName = "John", 
                LastName = "Doe", 
                Email = "john.doe@example.com" 
            };
            
            _mockValidator.Setup(v => v.Validate(customer))
                .Returns(new ValidationResult(new List<ValidationFailure>()));
                
            _mockRepository.Setup(r => r.GetById(customer.Id))
                .Returns(customer);
                
            _mockRepository.Setup(r => r.Update(customer)).Returns(true);
            
            // Act
            var result = _sut.UpdateCustomer(customer);
            
            // Assert
            result.Should().BeTrue();
        }
        
        [Fact]
        public void UpdateCustomer_WithNonExistentCustomer_ReturnsFalse()
        {
            // Arrange
            var customer = new Customer { 
                Id = 999, 
                FirstName = "John", 
                LastName = "Doe", 
                Email = "john.doe@example.com" 
            };
            
            _mockValidator.Setup(v => v.Validate(customer))
                .Returns(new ValidationResult(new List<ValidationFailure>()));
                
            _mockRepository.Setup(r => r.GetById(999))
                .Returns((Customer)null);
            
            // Act
            var result = _sut.UpdateCustomer(customer);
            
            // Assert
            result.Should().BeFalse();
        }
        
        [Fact]
        public void DeleteCustomer_WithExistingId_ReturnsTrue()
        {
            // Arrange
            var customer = new Customer { Id = 1 };
            _mockRepository.Setup(r => r.GetById(1)).Returns(customer);
            _mockRepository.Setup(r => r.Delete(1)).Returns(true);
            
            // Act
            var result = _sut.DeleteCustomer(1);
            
            // Assert
            result.Should().BeTrue();
        }
        
        [Fact]
        public void DeleteCustomer_WithNonExistentId_ReturnsFalse()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetById(999)).Returns((Customer)null);
            
            // Act
            var result = _sut.DeleteCustomer(999);
            
            // Assert
            result.Should().BeFalse();
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void DeleteCustomer_WithNonPositiveId_ThrowsArgumentException(int invalidId)
        {
            // Act & Assert
            Action act = () => _sut.DeleteCustomer(invalidId);
            
            act.Should().Throw<ArgumentException>()
                .WithMessage("*ID must be positive*")
                .WithParameterName("id");
        }
        
        [Fact]
        public void SearchCustomers_WithValidTerm_ReturnsMatchingCustomers()
        {
            // Arrange
            var customers = new List<Customer>
            {
                new Customer { Id = 1, FirstName = "John", LastName = "Doe" },
                new Customer { Id = 2, FirstName = "Jane", LastName = "Smith" }
            };
            
            _mockRepository.Setup(r => r.Search("John")).Returns(customers.Where(c => c.FirstName == "John"));
            
            // Act
            var results = _sut.SearchCustomers("John").ToList();
            
            // Assert
            results.Should().HaveCount(1);
            results[0].Id.Should().Be(1);
            results[0].FirstName.Should().Be("John");
        }
        
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void SearchCustomers_WithEmptyTerm_ReturnsEmptyList(string emptyTerm)
        {
            // Act
            var results = _sut.SearchCustomers(emptyTerm);
            
            // Assert
            results.Should().BeEmpty();
            _mockRepository.Verify(r => r.Search(It.IsAny<string>()), Times.Never);
        }
    }
}
