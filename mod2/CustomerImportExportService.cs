using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomerManagementApp.Data;
using CustomerManagementApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;

namespace CustomerManagementApp.Services
{
    public interface ICustomerImportExportService
    {
        Task<byte[]> ExportCustomersToCsvAsync();
        Task<CustomerImportResult> ImportCustomersFromCsvAsync(Stream csvStream);
    }

    public class CustomerImportResult
    {
        public int TotalRecords { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<CustomerImportError> Errors { get; set; } = new List<CustomerImportError>();
        public List<Customer> ImportedCustomers { get; set; } = new List<Customer>();
    }

    public class CustomerImportError
    {
        public int LineNumber { get; set; }
        public string ErrorMessage { get; set; }
        public string RawData { get; set; }
    }

    public class CustomerCsvRecord
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Status { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string Country { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }

    public class CustomerImportExportService : ICustomerImportExportService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CustomerImportExportService> _logger;

        public CustomerImportExportService(
            ApplicationDbContext dbContext,
            ILogger<CustomerImportExportService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<byte[]> ExportCustomersToCsvAsync()
        {
            try
            {
                _logger.LogInformation("Starting customer export to CSV");

                // Get all customers with their primary addresses
                var customers = await _dbContext.Customers
                    .AsNoTracking()
                    .Include(c => c.Addresses.Where(a => a.IsPrimary))
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} customers for export", customers.Count);

                // Create CSV records
                var records = customers.Select(c => 
                {
                    var primaryAddress = c.Addresses.FirstOrDefault(a => a.IsPrimary) ?? c.Addresses.FirstOrDefault();
                    
                    return new CustomerCsvRecord
                    {
                        FirstName = c.FirstName,
                        LastName = c.LastName,
                        Email = c.Email,
                        Phone = c.Phone,
                        Status = c.Status,
                        DateOfBirth = c.DateOfBirth,
                        Street = primaryAddress?.Street,
                        City = primaryAddress?.City,
                        State = primaryAddress?.State,
                        ZipCode = primaryAddress?.ZipCode,
                        Country = primaryAddress?.Country
                    };
                }).ToList();

                // Configure CSV writer
                var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ",",
                    Encoding = Encoding.UTF8
                };

                // Write to memory stream
                using var memoryStream = new MemoryStream();
                using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8);
                using var csvWriter = new CsvWriter(streamWriter, configuration);

                await csvWriter.WriteRecordsAsync(records);
                await csvWriter.FlushAsync();
                await streamWriter.FlushAsync();

                _logger.LogInformation("Customer export completed successfully");

                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting customers to CSV");
                throw;
            }
        }

        public async Task<CustomerImportResult> ImportCustomersFromCsvAsync(Stream csvStream)
        {
            var result = new CustomerImportResult();

            try
            {
                _logger.LogInformation("Starting customer import from CSV");

                // Configure CSV reader
                var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ",",
                    MissingFieldFound = null,
                    HeaderValidated = null,
                    BadDataFound = context =>
                    {
                        result.Errors.Add(new CustomerImportError
                        {
                            LineNumber = context.RawRecord.Count(),
                            ErrorMessage = "Invalid CSV format",
                            RawData = context.RawRecord
                        });
                    }
                };

                using var streamReader = new StreamReader(csvStream);
                using var csvReader = new CsvReader(streamReader, configuration);

                // Read all records
                var records = csvReader.GetRecords<CustomerCsvRecord>().ToList();
                result.TotalRecords = records.Count;

                _logger.LogInformation("Found {Count} records in CSV file", records.Count);

                // Process each record
                foreach (var record in records)
                {
                    try
                    {
                        // Validate required fields
                        var validationErrors = ValidateCustomerRecord(record);
                        if (validationErrors.Count > 0)
                        {
                            result.Errors.Add(new CustomerImportError
                            {
                                LineNumber = result.SuccessCount + result.FailureCount + 1,
                                ErrorMessage = string.Join("; ", validationErrors),
                                RawData = $"{record.FirstName},{record.LastName},{record.Email}"
                            });
                            result.FailureCount++;
                            continue;
                        }

                        // Check for duplicate email
                        var existingCustomer = await _dbContext.Customers
                            .FirstOrDefaultAsync(c => c.Email == record.Email);

                        if (existingCustomer != null)
                        {
                            result.Errors.Add(new CustomerImportError
                            {
                                LineNumber = result.SuccessCount + result.FailureCount + 1,
                                ErrorMessage = $"A customer with email '{record.Email}' already exists",
                                RawData = $"{record.FirstName},{record.LastName},{record.Email}"
                            });
                            result.FailureCount++;
                            continue;
                        }

                        // Create new customer
                        var customer = new Customer
                        {
                            FirstName = record.FirstName,
                            LastName = record.LastName,
                            Email = record.Email,
                            Phone = record.Phone,
                            Status = !string.IsNullOrWhiteSpace(record.Status) ? record.Status : "Active",
                            DateOfBirth = record.DateOfBirth,
                            CreatedDate = DateTime.UtcNow
                        };

                        // Add address if provided
                        if (!string.IsNullOrWhiteSpace(record.Street) && 
                            !string.IsNullOrWhiteSpace(record.City) &&
                            !string.IsNullOrWhiteSpace(record.State))
                        {
                            var address = new Address
                            {
                                Street = record.Street,
                                City = record.City,
                                State = record.State,
                                ZipCode = record.ZipCode,
                                Country = record.Country ?? "USA",
                                IsPrimary = true,
                                AddressType = "Home"
                            };

                            customer.Addresses.Add(address);
                        }

                        // Add to context
                        await _dbContext.Customers.AddAsync(customer);
                        result.ImportedCustomers.Add(customer);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing customer record");
                        
                        result.Errors.Add(new CustomerImportError
                        {
                            LineNumber = result.SuccessCount + result.FailureCount + 1,
                            ErrorMessage = $"Error processing record: {ex.Message}",
                            RawData = $"{record.FirstName},{record.LastName},{record.Email}"
                        });
                        
                        result.FailureCount++;
                    }
                }

                // Save changes if any successful records
                if (result.SuccessCount > 0)
                {
                    await _dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Customer import completed. Success: {SuccessCount}, Failures: {FailureCount}",
                    result.SuccessCount, result.FailureCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing customers from CSV");
                throw;
            }
        }

        private List<string> ValidateCustomerRecord(CustomerCsvRecord record)
        {
            var errors = new List<string>();

            // Check required fields
            if (string.IsNullOrWhiteSpace(record.FirstName))
                errors.Add("First name is required");

            if (string.IsNullOrWhiteSpace(record.LastName))
                errors.Add("Last name is required");

            if (string.IsNullOrWhiteSpace(record.Email))
                errors.Add("Email is required");
            else if (!IsValidEmail(record.Email))
                errors.Add("Email format is invalid");

            // Validate date of birth if provided
            if (record.DateOfBirth.HasValue)
            {
                var age = CalculateAge(record.DateOfBirth.Value);
                if (age < 0)
                    errors.Add("Date of birth cannot be in the future");
                else if (age > 120)
                    errors.Add("Age cannot exceed 120 years");
            }

            return errors;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age))
                age--;
            return age;
        }
    }
}
