using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using CustomerManagementApp.Models;
using CustomerManagementApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace CustomerManagementApp.Services
{
    /// <summary>
    /// Refactored implementation of the customer service with improved practices
    /// </summary>
    public class CustomerServiceRefactored : ICustomerService
    {
        private readonly string _connectionString;
        private readonly ILogger<CustomerServiceRefactored> _logger;
        
        public CustomerServiceRefactored(string connectionString, ILogger<CustomerServiceRefactored> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public List<CustomerViewModel> GetAllCustomers()
        {
            try
            {
                _logger.LogInformation("Retrieving all customers");
                
                const string query = @"
                    SELECT c.CustomerID, c.FirstName, c.LastName, c.Email, c.Phone, 
                           c.CreatedDate, c.Status 
                    FROM Customers c
                    ORDER BY c.LastName, c.FirstName";
                
                var customers = ExecuteQuery(query, null, MapToCustomerViewModel);
                
                _logger.LogInformation("Retrieved {Count} customers", customers.Count);
                
                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all customers");
                throw;
            }
        }
        
        public CustomerViewModel GetCustomerById(int id)
        {
            try
            {
                _logger.LogInformation("Retrieving customer with ID: {CustomerId}", id);
                
                const string query = @"
                    SELECT c.CustomerID, c.FirstName, c.LastName, c.Email, c.Phone, 
                           c.CreatedDate, c.Status, a.AddressID, a.Street, a.City, 
                           a.State, a.ZipCode, a.Country, a.IsPrimary 
                    FROM Customers c
                    LEFT JOIN Addresses a ON c.CustomerID = a.CustomerID AND a.IsPrimary = 1
                    WHERE c.CustomerID = @ID";
                
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", id }
                };
                
                var customers = ExecuteQuery(query, parameters, MapToCustomerWithAddressViewModel);
                
                var customer = customers.FirstOrDefault();
                
                if (customer == null)
                {
                    _logger.LogWarning("Customer with ID {CustomerId} not found", id);
                }
                
                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer with ID: {CustomerId}", id);
                throw;
            }
        }
        
        public List<CustomerViewModel> SearchCustomers(string searchTerm, bool onlyActive)
        {
            try
            {
                _logger.LogInformation("Searching customers with term: {SearchTerm}, OnlyActive: {OnlyActive}", 
                    searchTerm, onlyActive);
                
                var query = @"
                    SELECT c.CustomerID, c.FirstName, c.LastName, c.Email, c.Phone, 
                           c.CreatedDate, c.Status, a.Street, a.City, a.State, 
                           a.ZipCode, a.Country 
                    FROM Customers c
                    LEFT JOIN Addresses a ON c.CustomerID = a.CustomerID AND a.IsPrimary = 1
                    WHERE 1=1";
                
                var parameters = new Dictionary<string, object>();
                
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query += @" AND (c.FirstName LIKE @Search OR c.LastName LIKE @Search 
                              OR c.Email LIKE @Search)";
                    parameters.Add("@Search", $"%{searchTerm}%");
                }
                
                if (onlyActive)
                {
                    query += " AND c.Status = @Status";
                    parameters.Add("@Status", "Active");
                }
                
                query += " ORDER BY c.LastName, c.FirstName";
                
                var customers = ExecuteQuery(query, parameters, MapToCustomerWithAddressViewModel);
                
                _logger.LogInformation("Found {Count} customers matching criteria", customers.Count);
                
                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with term: {SearchTerm}", searchTerm);
                throw;
            }
        }
        
        public void CreateCustomer(CustomerViewModel customerViewModel)
        {
            if (customerViewModel == null)
            {
                throw new ArgumentNullException(nameof(customerViewModel));
            }
            
            try
            {
                _logger.LogInformation("Creating new customer: {FirstName} {LastName}", 
                    customerViewModel.FirstName, customerViewModel.LastName);
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Insert customer
                            const string customerInsertSql = @"
                                INSERT INTO Customers (FirstName, LastName, Email, Phone, CreatedDate, Status) 
                                VALUES (@FirstName, @LastName, @Email, @Phone, @CreatedDate, @Status);
                                SELECT SCOPE_IDENTITY();";
                            
                            var customerId = ExecuteScalar<int>(connection, transaction, customerInsertSql, new Dictionary<string, object>
                            {
                                { "@FirstName", customerViewModel.FirstName },
                                { "@LastName", customerViewModel.LastName },
                                { "@Email", customerViewModel.Email },
                                { "@Phone", customerViewModel.Phone ?? (object)DBNull.Value },
                                { "@CreatedDate", DateTime.Now },
                                { "@Status", customerViewModel.Status ?? "Active" }
                            });
                            
                            // Insert address if provided
                            if (customerViewModel.Address != null)
                            {
                                const string addressInsertSql = @"
                                    INSERT INTO Addresses (CustomerID, Street, City, State, ZipCode, Country, IsPrimary) 
                                    VALUES (@CustomerID, @Street, @City, @State, @ZipCode, @Country, @IsPrimary)";
                                
                                ExecuteNonQuery(connection, transaction, addressInsertSql, new Dictionary<string, object>
                                {
                                    { "@CustomerID", customerId },
                                    { "@Street", customerViewModel.Address.Street },
                                    { "@City", customerViewModel.Address.City },
                                    { "@State", customerViewModel.Address.State },
                                    { "@ZipCode", customerViewModel.Address.ZipCode },
                                    { "@Country", customerViewModel.Address.Country },
                                    { "@IsPrimary", customerViewModel.Address.IsPrimary }
                                });
                            }
                            
                            transaction.Commit();
                            
                            _logger.LogInformation("Customer created successfully with ID: {CustomerId}", customerId);
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer: {FirstName} {LastName}", 
                    customerViewModel.FirstName, customerViewModel.LastName);
                throw;
            }
        }
        
        public void UpdateCustomer(CustomerViewModel customerViewModel)
        {
            if (customerViewModel == null)
            {
                throw new ArgumentNullException(nameof(customerViewModel));
            }
            
            try
            {
                _logger.LogInformation("Updating customer with ID: {CustomerId}", customerViewModel.ID);
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Update customer
                            const string customerUpdateSql = @"
                                UPDATE Customers 
                                SET FirstName = @FirstName, 
                                    LastName = @LastName, 
                                    Email = @Email, 
                                    Phone = @Phone, 
                                    Status = @Status 
                                WHERE CustomerID = @ID";
                            
                            ExecuteNonQuery(connection, transaction, customerUpdateSql, new Dictionary<string, object>
                            {
                                { "@ID", customerViewModel.ID },
                                { "@FirstName", customerViewModel.FirstName },
                                { "@LastName", customerViewModel.LastName },
                                { "@Email", customerViewModel.Email },
                                { "@Phone", customerViewModel.Phone ?? (object)DBNull.Value },
                                { "@Status", customerViewModel.Status }
                            });
                            
                            // Update or insert address if provided
                            if (customerViewModel.Address != null)
                            {
                                if (customerViewModel.Address.ID > 0)
                                {
                                    // Update existing address
                                    const string addressUpdateSql = @"
                                        UPDATE Addresses 
                                        SET Street = @Street, 
                                            City = @City, 
                                            State = @State, 
                                            ZipCode = @ZipCode, 
                                            Country = @Country, 
                                            IsPrimary = @IsPrimary 
                                        WHERE AddressID = @AddressID";
                                    
                                    ExecuteNonQuery(connection, transaction, addressUpdateSql, new Dictionary<string, object>
                                    {
                                        { "@AddressID", customerViewModel.Address.ID },
                                        { "@Street", customerViewModel.Address.Street },
                                        { "@City", customerViewModel.Address.City },
                                        { "@State", customerViewModel.Address.State },
                                        { "@ZipCode", customerViewModel.Address.ZipCode },
                                        { "@Country", customerViewModel.Address.Country },
                                        { "@IsPrimary", customerViewModel.Address.IsPrimary }
                                    });
                                }
                                else
                                {
                                    // Insert new address
                                    const string addressInsertSql = @"
                                        INSERT INTO Addresses (CustomerID, Street, City, State, ZipCode, Country, IsPrimary) 
                                        VALUES (@CustomerID, @Street, @City, @State, @ZipCode, @Country, @IsPrimary)";
                                    
                                    ExecuteNonQuery(connection, transaction, addressInsertSql, new Dictionary<string, object>
                                    {
                                        { "@CustomerID", customerViewModel.ID },
                                        { "@Street", customerViewModel.Address.Street },
                                        { "@City", customerViewModel.Address.City },
                                        { "@State", customerViewModel.Address.State },
                                        { "@ZipCode", customerViewModel.Address.ZipCode },
                                        { "@Country", customerViewModel.Address.Country },
                                        { "@IsPrimary", customerViewModel.Address.IsPrimary }
                                    });
                                }
                            }
                            
                            transaction.Commit();
                            
                            _logger.LogInformation("Customer updated successfully");
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer with ID: {CustomerId}", customerViewModel.ID);
                throw;
            }
        }
        
        public void DeleteCustomer(int id)
        {
            try
            {
                _logger.LogInformation("Deleting customer with ID: {CustomerId}", id);
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Delete addresses first (foreign key constraint)
                            const string addressDeleteSql = "DELETE FROM Addresses WHERE CustomerID = @CustomerID";
                            
                            ExecuteNonQuery(connection, transaction, addressDeleteSql, new Dictionary<string, object>
                            {
                                { "@CustomerID", id }
                            });
                            
                            // Then delete customer
                            const string customerDeleteSql = "DELETE FROM Customers WHERE CustomerID = @ID";
                            
                            ExecuteNonQuery(connection, transaction, customerDeleteSql, new Dictionary<string, object>
                            {
                                { "@ID", id }
                            });
                            
                            transaction.Commit();
                            
                            _logger.LogInformation("Customer deleted successfully");
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer with ID: {CustomerId}", id);
                throw;
            }
        }
        
        public bool CustomerExists(int id)
        {
            try
            {
                const string query = "SELECT COUNT(1) FROM Customers WHERE CustomerID = @ID";
                
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", id }
                };
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (var command = new SqlCommand(query, connection))
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key, param.Value);
                        }
                        
                        return (int)command.ExecuteScalar() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if customer exists with ID: {CustomerId}", id);
                throw;
            }
        }
        
        #region Helper Methods
        
        private List<T> ExecuteQuery<T>(string query, Dictionary<string, object> parameters, Func<SqlDataReader, T> mapper)
        {
            var results = new List<T>();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key, param.Value);
                        }
                    }
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(mapper(reader));
                        }
                    }
                }
            }
            
            return results;
        }
        
        private T ExecuteScalar<T>(SqlConnection connection, SqlTransaction transaction, string query, Dictionary<string, object> parameters)
        {
            using (var command = new SqlCommand(query, connection, transaction))
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
                
                return (T)Convert.ChangeType(command.ExecuteScalar(), typeof(T));
            }
        }
        
        private void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string query, Dictionary<string, object> parameters)
        {
            using (var command = new SqlCommand(query, connection, transaction))
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
                
                command.ExecuteNonQuery();
            }
        }
        
        private CustomerViewModel MapToCustomerViewModel(SqlDataReader reader)
        {
            return new CustomerViewModel
            {
                ID = (int)reader["CustomerID"],
                FirstName = reader["FirstName"].ToString(),
                LastName = reader["LastName"].ToString(),
                Email = reader["Email"].ToString(),
                Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() : null,
                CreatedDate = (DateTime)reader["CreatedDate"],
                Status = reader["Status"].ToString()
            };
        }
        
        private CustomerViewModel MapToCustomerWithAddressViewModel(SqlDataReader reader)
        {
            var customer = MapToCustomerViewModel(reader);
            
            // Add address if exists
            if (reader["Street"] != DBNull.Value)
            {
                customer.Address = new AddressViewModel
                {
                    ID = reader["AddressID"] != DBNull.Value ? (int)reader["AddressID"] : 0,
                    Street = reader["Street"].ToString(),
                    City = reader["City"].ToString(),
                    State = reader["State"].ToString(),
                    ZipCode = reader["ZipCode"].ToString(),
                    Country = reader["Country"].ToString(),
                    IsPrimary = reader["IsPrimary"] != DBNull.Value && (bool)reader["IsPrimary"]
                };
            }
            
            return customer;
        }
        
        #endregion
    }
}
