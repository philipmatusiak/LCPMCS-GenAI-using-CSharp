using CustomerManagementApp.Models;
using CustomerManagementApp.Validation;

namespace CustomerManagementApp.Services
{
    public interface ICustomerValidator
    {
        ValidationResult Validate(Customer customer);
    }
}
