using System.Collections.Generic;
using CustomerManagementApp.ViewModels;

namespace CustomerManagementApp.Services
{
    public interface ICustomerService
    {
        List<CustomerViewModel> GetAllCustomers();
        CustomerViewModel GetCustomerById(int id);
        List<CustomerViewModel> SearchCustomers(string searchTerm, bool onlyActive);
        void CreateCustomer(CustomerViewModel customerViewModel);
        void UpdateCustomer(CustomerViewModel customerViewModel);
        void DeleteCustomer(int id);
        bool CustomerExists(int id);
    }
}
