using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CustomerManagementApp.Data;
using CustomerManagementApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerManagementApp.Services
{
    public interface ICustomerAuditService
    {
        Task<int> LogCustomerActivityAsync(CustomerAuditLog auditLog);
        Task<IEnumerable<CustomerAuditLogDto>> GetCustomerAuditHistoryAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null, string actionType = null);
        Task<IEnumerable<CustomerAuditLogDto>> GetRecentAuditActivityAsync(int count = 50);
    }

    public class CustomerAuditService : ICustomerAuditService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CustomerAuditService> _logger;

        public CustomerAuditService(
            ApplicationDbContext dbContext,
            ILogger<CustomerAuditService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> LogCustomerActivityAsync(CustomerAuditLog auditLog)
        {
            if (auditLog == null)
                throw new ArgumentNullException(nameof(auditLog));

            try
            {
                _dbContext.AuditLogs.Add(auditLog);
                await _dbContext.SaveChangesAsync();
                return auditLog.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging customer activity");
                throw;
            }
        }

        public async Task<IEnumerable<CustomerAuditLogDto>> GetCustomerAuditHistoryAsync(
            int customerId, 
            DateTime? startDate = null, 
            DateTime? endDate = null, 
            string actionType = null)
        {
            try
            {
                _logger.LogInformation("Getting audit history for customer ID: {CustomerId}", customerId);

                var query = _dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.CustomerId == customerId);

                if (startDate.HasValue)
                {
                    query = query.Where(a => a.Timestamp >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(a => a.Timestamp <= endDate.Value);
                }

                if (!string.IsNullOrWhiteSpace(actionType))
                {
                    query = query.Where(a => a.ActionType == actionType);
                }

                var auditLogs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();

                return auditLogs.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit history for customer ID: {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<CustomerAuditLogDto>> GetRecentAuditActivityAsync(int count = 50)
        {
            try
            {
                _logger.LogInformation("Getting recent audit activity, count: {Count}", count);

                var auditLogs = await _dbContext.AuditLogs
                    .AsNoTracking()
                    .OrderByDescending(a => a.Timestamp)
                    .Take(count)
                    .ToListAsync();

                return auditLogs.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent audit activity");
                throw;
            }
        }

        private CustomerAuditLogDto MapToDto(CustomerAuditLog auditLog)
        {
            var dto = new CustomerAuditLogDto
            {
                Id = auditLog.Id,
                CustomerId = auditLog.CustomerId,
                ActionType = auditLog.ActionType,
                Timestamp = auditLog.Timestamp,
                UserId = auditLog.UserId,
                UserName = auditLog.UserName
            };

            if (!string.IsNullOrWhiteSpace(auditLog.OldValues))
            {
                try
                {
                    dto.OldValues = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.OldValues);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deserializing old values for audit log ID: {AuditLogId}", auditLog.Id);
                }
            }

            if (!string.IsNullOrWhiteSpace(auditLog.NewValues))
            {
                try
                {
                    dto.NewValues = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.NewValues);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deserializing new values for audit log ID: {AuditLogId}", auditLog.Id);
                }
            }

            return dto;
        }
    }

    // Entity model
    public class CustomerAuditLog
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string ActionType { get; set; } // Create, Update, Delete, etc.
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string OldValues { get; set; } // JSON serialized old values
        public string NewValues { get; set; } // JSON serialized new values
        
        // Navigation property
        public Customer Customer { get; set; }
    }

    // DTO model
    public class CustomerAuditLogDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string ActionType { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public Dictionary<string, object> OldValues { get; set; }
        public Dictionary<string, object> NewValues { get; set; }
        public List<ValueChange> Changes { get; set; } = new List<ValueChange>();
    }

    public class ValueChange
    {
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }

    // Extension method for EntityFramework to automatically track changes
    public static class AuditExtensions
    {
        public static async Task<int> LogCustomerCreatedAsync(
            this ICustomerAuditService auditService,
            Customer customer,
            string userId,
            string userName)
        {
            var newValues = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["Id"] = customer.Id,
                ["FirstName"] = customer.FirstName,
                ["LastName"] = customer.LastName,
                ["Email"] = customer.Email,
                ["Phone"] = customer.Phone,
                ["Status"] = customer.Status,
                ["DateOfBirth"] = customer.DateOfBirth
            });

            var auditLog = new CustomerAuditLog
            {
                CustomerId = customer.Id,
                ActionType = "Create",
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                UserName = userName,
                NewValues = newValues
            };

            return await auditService.LogCustomerActivityAsync(auditLog);
        }

        public static async Task<int> LogCustomerUpdatedAsync(
            this ICustomerAuditService auditService,
            Customer oldCustomer,
            Customer newCustomer,
            string userId,
            string userName)
        {
            var oldValues = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["FirstName"] = oldCustomer.FirstName,
                ["LastName"] = oldCustomer.LastName,
                ["Email"] = oldCustomer.Email,
                ["Phone"] = oldCustomer.Phone,
                ["Status"] = oldCustomer.Status,
                ["DateOfBirth"] = oldCustomer.DateOfBirth
            });

            var newValues = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["FirstName"] = newCustomer.FirstName,
                ["LastName"] = newCustomer.LastName,
                ["Email"] = newCustomer.Email,
                ["Phone"] = newCustomer.Phone,
                ["Status"] = newCustomer.Status,
                ["DateOfBirth"] = newCustomer.DateOfBirth
            });

            var auditLog = new CustomerAuditLog
            {
                CustomerId = newCustomer.Id,
                ActionType = "Update",
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                UserName = userName,
                OldValues = oldValues,
                NewValues = newValues
            };

            return await auditService.LogCustomerActivityAsync(auditLog);
        }

        public static async Task<int> LogCustomerDeletedAsync(
            this ICustomerAuditService auditService,
            Customer customer,
            string userId,
            string userName)
        {
            var oldValues = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["Id"] = customer.Id,
                ["FirstName"] = customer.FirstName,
                ["LastName"] = customer.LastName,
                ["Email"] = customer.Email,
                ["Phone"] = customer.Phone,
                ["Status"] = customer.Status,
                ["DateOfBirth"] = customer.DateOfBirth
            });

            var auditLog = new CustomerAuditLog
            {
                CustomerId = customer.Id,
                ActionType = "Delete",
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                UserName = userName,
                OldValues = oldValues
            };

            return await auditService.LogCustomerActivityAsync(auditLog);
        }
    }
}
