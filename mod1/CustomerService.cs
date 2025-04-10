using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using CustomerManagementApp.Models;
using CustomerManagementApp.ViewModels;

namespace CustomerManagementApp.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly string _connString;
        
        public CustomerService(string cs)
        {
            _connString = cs;
        }
        
        public List<CustomerViewModel> GetAllCustomers()
        {
            var customers = new List<CustomerViewModel>();
            
            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT c.CustomerID, c.FirstName, c.LastName, c.Email, c.Phone, c.CreatedDate, c.Status FROM Customers c", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var customer = new CustomerViewModel
                        {
                            ID = (int)reader["CustomerID"],
                            FirstName = reader["FirstName"].ToString(),
                            LastName = reader["LastName"].ToString(),
                            Email = reader["Email"].ToString(),
                            Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() : "",
                            CreatedDate = (DateTime)reader["CreatedDate"],
                            Status = reader["Status"].ToString()
                        };
                        
                        customers.Add(customer);
                    }
                }
            }
            
            return customers;
        }
        
        public CustomerViewModel GetCustomerById(int id)
        {
            CustomerViewModel customer = null;
            
            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT c.CustomerID, c.FirstName, c.LastName, c.Email, c.Phone, c.CreatedDate, c.Status, a.AddressID, a.Street, a.City, a.State, a.ZipCode, a.Country, a.IsPrimary FROM Customers c LEFT JOIN Addresses a ON c.CustomerID = a.CustomerID WHERE c.CustomerID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", id);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer = new CustomerViewModel
                            {
                                ID = (int)reader["CustomerID"],
                                FirstName = reader["FirstName"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Email = reader["Email"].ToString(),
                                Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() : "",
                                CreatedDate = (DateTime)reader["CreatedDate"],
                                Status = reader["Status"].ToString()
                            };
                            
                            if (reader["AddressID"] != DBNull.Value)
                            {
                                customer.Address = new AddressViewModel
                                {
                                    ID = (int)reader["AddressID"],
                                    Street = reader["Street"].ToString(),
                                    City = reader["City"].ToString(),
                                    State = reader["State"].ToString(),
                                    ZipCode = reader["ZipCode"].ToString(),
                                    Country = reader["Country"].ToString(),
                                    IsPrimary = (bool)reader["IsPrimary"]
                                };
                            }
                        }
                    }
                }
            }
            
            return customer;
        }
        
        public List<CustomerViewModel> GetCustomers(string searchTerm, bool onlyActive)
        {
            var result = new List<CustomerViewModel>();
            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        
                        var query = "SELECT c.CustomerID, c.FirstName, c.LastName, c.Email, c.Phone, " +
                                   "c.CreatedDate, c.Status, a.Street, a.City, a.State, a.ZipCode, " +
                                   "a.Country FROM Customers c " +
                                   "LEFT JOIN Addresses a ON c.CustomerID = a.CustomerID " +
                                   "WHERE 1=1 ";
                                   
                        if (!string.IsNullOrEmpty(searchTerm))
                            query += "AND (c.FirstName LIKE @search OR c.LastName LIKE @search OR c.Email LIKE @search) ";
                            
                        if (onlyActive)
                            query += "AND c.Status = 'Active' ";
                            
                        cmd.CommandText = query;
                        
                        if (!string.IsNullOrEmpty(searchTerm))
                            cmd.Parameters.AddWithValue("@search", "%" + searchTerm + "%");
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var vm = new CustomerViewModel();
                                vm.ID = (int)reader["CustomerID"];
                                vm.FirstName = reader["FirstName"].ToString();
                                vm.LastName = reader["LastName"].ToString();
                                vm.Email = reader["Email"].ToString();
                                vm.Phone = reader["Phone"] != DBNull.Value ? 
                                    reader["Phone"].ToString() : "";
                                vm.CreatedDate = (DateTime)reader["CreatedDate"];
                                vm.Status = reader["Status"].ToString();
                                
                                // Address might be null
                                if (reader["Street"] != DBNull.Value)
                                {
                                    vm.Address = new AddressViewModel
                                    {
                                        Street = reader["Street"].ToString(),
                                        City = reader["City"].ToString(),
                                        State = reader["State"].ToString(),
                                        ZipCode = reader["ZipCode"].ToString(),
                                        Country = reader["Country"].ToString()
                                    };
                                }
                                
                                result.Add(vm);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                // Just return empty list on error
            }
            
            return result;
        }
        
        public List<CustomerViewModel> SearchCustomers(string searchTerm, bool onlyActive)
        {
            return GetCustomers(searchTerm, onlyActive);
        }
        
        public void CreateCustomer(CustomerViewModel customerViewModel)
        {
            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("INSERT INTO Customers (FirstName, LastName, Email, Phone, CreatedDate, Status) VALUES (@FirstName, @LastName, @Email, @Phone, @CreatedDate, @Status); SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@FirstName", customerViewModel.FirstName);
                    cmd.Parameters.AddWithValue("@LastName", customerViewModel.LastName);
                    cmd.Parameters.AddWithValue("@Email", customerViewModel.Email);
                    cmd.Parameters.AddWithValue("@Phone", customerViewModel.Phone ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Status", customerViewModel.Status ?? "Active");
                    
                    int customerId = Convert.ToInt32(cmd.ExecuteScalar());
                    
                    // Add address if provided
                    if (customerViewModel.Address != null)
                    {
                        using (var addrCmd = new SqlCommand("INSERT INTO Addresses (CustomerID, Street, City, State, ZipCode, Country, IsPrimary) VALUES (@CustomerID, @Street, @City, @State, @ZipCode, @Country, @IsPrimary)", conn))
                        {
                            addrCmd.Parameters.AddWithValue("@CustomerID", customerId);
                            addrCmd.Parameters.AddWithValue("@Street", customerViewModel.Address.Street);
                            addrCmd.Parameters.AddWithValue("@City", customerViewModel.Address.City);
                            addrCmd.Parameters.AddWithValue("@State", customerViewModel.Address.State);
                            addrCmd.Parameters.AddWithValue("@ZipCode", customerViewModel.Address.ZipCode);
                            addrCmd.Parameters.AddWithValue("@Country", customerViewModel.Address.Country);
                            addrCmd.Parameters.AddWithValue("@IsPrimary", customerViewModel.Address.IsPrimary);
                            
                            addrCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
        
        public void UpdateCustomer(CustomerViewModel customerViewModel)
        {
            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE Customers SET FirstName = @FirstName, LastName = @LastName, Email = @Email, Phone = @Phone, Status = @Status WHERE CustomerID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", customerViewModel.ID);
                    cmd.Parameters.AddWithValue("@FirstName", customerViewModel.FirstName);
                    cmd.Parameters.AddWithValue("@LastName", customerViewModel.LastName);
                    cmd.Parameters.AddWithValue("@Email", customerViewModel.Email);
                    cmd.Parameters.AddWithValue("@Phone", customerViewModel.Phone ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", customerViewModel.Status);
                    
                    cmd.ExecuteNonQuery();
                    
                    // Update address if provided
                    if (customerViewModel.Address != null)
                    {
                        if (customerViewModel.Address.ID > 0)
                        {
                            using (var addrCmd = new SqlCommand("UPDATE Addresses SET Street = @Street, City = @City, State = @State, ZipCode = @ZipCode, Country = @Country, IsPrimary = @IsPrimary WHERE AddressID = @AddressID", conn))
                            {
                                addrCmd.Parameters.AddWithValue("@AddressID", customerViewModel.Address.ID);
                                addrCmd.Parameters.AddWithValue("@Street", customerViewModel.Address.Street);
                                addrCmd.Parameters.AddWithValue("@City", customerViewModel.Address.City);
                                addrCmd.Parameters.AddWithValue("@State", customerViewModel.Address.State);
                                addrCmd.Parameters.AddWithValue("@ZipCode", customerViewModel.Address.ZipCode);
                                addrCmd.Parameters.AddWithValue("@Country", customerViewModel.Address.Country);
                                addrCmd.Parameters.AddWithValue("@IsPrimary", customerViewModel.Address.IsPrimary);
                                
                                addrCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (var addrCmd = new SqlCommand("INSERT INTO Addresses (CustomerID, Street, City, State, ZipCode, Country, IsPrimary) VALUES (@CustomerID, @Street, @City, @State, @ZipCode, @Country, @IsPrimary)", conn))
                            {
                                addrCmd.Parameters.AddWithValue("@CustomerID", customerViewModel.ID);
                                addrCmd.Parameters.AddWithValue("@Street", customerViewModel.Address.Street);
                                addrCmd.Parameters.AddWithValue("@City", customerViewModel.Address.City);
                                addrCmd.Parameters.AddWithValue("@State", customerViewModel.Address.State);
                                addrCmd.Parameters.AddWithValue("@ZipCode", customerViewModel.Address.ZipCode);
                                addrCmd.Parameters.AddWithValue("@Country", customerViewModel.Address.Country);
                                addrCmd.Parameters.AddWithValue("@IsPrimary", customerViewModel.Address.IsPrimary);
                                
                                addrCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }
        
        public void DeleteCustomer(int id)
        {
            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();
                
                // Delete addresses first (foreign key constraint)
                using (var addrCmd = new SqlCommand("DELETE FROM Addresses WHERE CustomerID = @CustomerID", conn))
                {
                    addrCmd.Parameters.AddWithValue("@CustomerID", id);
                    addrCmd.ExecuteNonQuery();
                }
                
                // Then delete customer
                using (var cmd = new SqlCommand("DELETE FROM Customers WHERE CustomerID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        
        public bool CustomerExists(int id)
        {
            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Customers WHERE CustomerID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", id);
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }
    }
}
