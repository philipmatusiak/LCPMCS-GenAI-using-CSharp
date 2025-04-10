using System.Collections.Generic;
using CustomerManagementApp.Models;

namespace CustomerManagementApp.Services
{
    public interface ICustomerService
    {
        Customer GetCustomerById(int id);
        IEnumerable<Customer> SearchCustomers(string searchTerm);
        int CreateCustomer(Customer customer);
        bool UpdateCustomer(Customer customer);
        bool DeleteCustomer(int id);
    }
}
