using System.Collections.Generic;
using CustomerManagementApp.Models;

namespace CustomerManagementApp.Repositories
{
    public interface ICustomerRepository
    {
        Customer GetById(int id);
        Customer GetByEmail(string email);
        IEnumerable<Customer> Search(string searchTerm);
        int Create(Customer customer);
        bool Update(Customer customer);
        bool Delete(int id);
    }
}
