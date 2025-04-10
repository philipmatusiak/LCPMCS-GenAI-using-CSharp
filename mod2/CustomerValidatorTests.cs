using System;
using System.Linq;
using CustomerManagementApp.Models;
using CustomerManagementApp.Validation;
using Xunit;

namespace CustomerManagementApp.Tests
{
    public class CustomerValidatorTests
    {
        private readonly CustomerValidator _validator;
        
        public CustomerValidatorTests()
        {
            _validator = new CustomerValidator();
        }
        
        [Fact]
        public void Validate_NullCustomer_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => _validator.Validate(null));
            Assert.Equal("customer", exception.ParamName);
        }
        
        [Fact]
        public void Validate_ValidCustomer_ReturnsValidResult()
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                DateOfBirth = DateTime.Today.AddYears(-30)
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Validate_EmptyFirstName_ReturnsInvalidResult(string firstName)
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = firstName,
                LastName = "Doe",
                Email = "john.doe@example.com"
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("FirstName", error.PropertyName);
            Assert.Contains("empty", error.ErrorMessage);
        }
        
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Validate_EmptyLastName_ReturnsInvalidResult(string lastName)
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = lastName,
                Email = "john.doe@example.com"
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("LastName", error.PropertyName);
            Assert.Contains("empty", error.ErrorMessage);
        }
        
        [Theory]
        [InlineData("notanemail")]
        [InlineData("missing@tld")]
        [InlineData("@nodomain.com")]
        [InlineData("spaces in@email.com")]
        public void Validate_InvalidEmail_ReturnsInvalidResult(string email)
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
                Email = email
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("Email", error.PropertyName);
            Assert.Contains("invalid", error.ErrorMessage);
        }
        
        [Fact]
        public void Validate_EmptyEmail_DoesNotValidateFormat()
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
                Email = ""
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        
        [Theory]
        [InlineData(-17)] // 17 years old
        [InlineData(-1)]  // 1 year old
        [InlineData(0)]   // Born today
        public void Validate_CustomerUnder18_ReturnsInvalidResult(int yearOffset)
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                DateOfBirth = DateTime.Today.AddYears(yearOffset)
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("DateOfBirth", error.PropertyName);
            Assert.Contains("18", error.ErrorMessage);
        }
        
        [Theory]
        [InlineData(-121)] // 121 years old
        [InlineData(-150)] // 150 years old
        public void Validate_CustomerOver120_ReturnsInvalidResult(int yearOffset)
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                DateOfBirth = DateTime.Today.AddYears(yearOffset)
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("DateOfBirth", error.PropertyName);
            Assert.Contains("120", error.ErrorMessage);
        }
        
        [Theory]
        [InlineData(-18)]  // 18 years old - boundary
        [InlineData(-50)]  // 50 years old
        [InlineData(-120)] // 120 years old - boundary
        public void Validate_CustomerWithValidAge_ReturnsValidResult(int yearOffset)
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                DateOfBirth = DateTime.Today.AddYears(yearOffset)
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        
        [Fact]
        public void Validate_NullDateOfBirth_DoesNotValidateAge()
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                DateOfBirth = null
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        
        [Fact]
        public void Validate_MultipleInvalidFields_ReturnsAllErrors()
        {
            // Arrange
            var customer = new Customer
            {
                FirstName = "",
                LastName = "",
                Email = "invalid-email",
                DateOfBirth = DateTime.Today // Age 0
            };
            
            // Act
            var result = _validator.Validate(customer);
            
            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(4, result.Errors.Count);
            Assert.Contains(result.Errors, e => e.PropertyName == "FirstName");
            Assert.Contains(result.Errors, e => e.PropertyName == "LastName");
            Assert.Contains(result.Errors, e => e.PropertyName == "Email");
            Assert.Contains(result.Errors, e => e.PropertyName == "DateOfBirth");
        }
    }
}
