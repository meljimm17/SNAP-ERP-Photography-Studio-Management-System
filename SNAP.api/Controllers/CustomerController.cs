using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// Customer management API - handles customer CRUD operations, booking history,
    /// and customer-related queries.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequireAccessLevel(SystemModule.CustomerManagement, AccessLevel.AssignedView)]
    [Route("api/customers")]
    public class CustomerController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;

        public CustomerController(TenantDbContextFactory tenantDbContextFactory)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        /// <summary>Get all active customers for the current tenant</summary>
        [HttpGet]
        public ActionResult<List<CustomerRow>> GetCustomers()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.Customers
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.CustomerId)
                .Select(x => new CustomerRow
                {
                    CustomerId = x.CustomerId,
                    FullName = x.FullName,
                    ContactNumber = x.ContactNumber,
                    Email = x.EmailAddress ?? string.Empty,
                    Address = x.Address ?? string.Empty,
                    CreatedAt = x.CreatedAt
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Get a specific customer by ID</summary>
        [HttpGet("{id:int}")]
        public ActionResult<CustomerDetailDto> GetCustomer(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var customer = db.Customers.Find(id);
            if (customer is null || !customer.IsActive)
                return NotFound(new { message = "Customer not found." });

            return Ok(new CustomerDetailDto
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                ContactNumber = customer.ContactNumber,
                EmailAddress = customer.EmailAddress,
                Address = customer.Address,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt
            });
        }

        /// <summary>Get booking history for a specific customer</summary>
        [HttpGet("{customerId:int}/bookings")]
        public ActionResult<List<CustomerBookingHistoryDto>> GetCustomerBookingHistory(int customerId)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var customer = db.Customers.Find(customerId);
            if (customer is null)
                return NotFound(new { message = "Customer not found." });

            var bookings = db.Bookings
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.SessionDate)
                .Select(x => new CustomerBookingHistoryDto
                {
                    BookingId = x.BookingId,
                    Package = x.PackageNameSnapshot,
                    SessionDate = x.SessionDate,
                    SessionStartTime = x.SessionStartTime,
                    SessionEndTime = x.SessionEndTime,
                    Status = x.Status.ToString(),
                    Price = x.PriceSnapshot,
                    AmountPaid = db.Payments.Where(p => p.BookingId == x.BookingId && !p.IsDeleted).Sum(p => p.AmountPaid)
                })
                .ToList();

            return Ok(bookings);
        }

        /// <summary>Create a new customer</summary>
        [RequireAccessLevel(SystemModule.CustomerManagement, AccessLevel.Manage)]
        [HttpPost]
        public ActionResult<int> CreateCustomer(CreateCustomerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(request.ContactNumber))
                return BadRequest(new { message = "Contact number is required." });

            if (!Validation.IsValidPhMobileNumber(request.ContactNumber))
                return BadRequest(new { message = "Invalid PH mobile number format." });

            if (!string.IsNullOrWhiteSpace(request.EmailAddress) && 
                !Validation.IsValidEmail(request.EmailAddress))
                return BadRequest(new { message = "Invalid email address format." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var customer = new Customer
            {
                FullName = request.FullName.Trim(),
                ContactNumber = request.ContactNumber.Trim(),
                EmailAddress = string.IsNullOrWhiteSpace(request.EmailAddress) ? null : request.EmailAddress.Trim(),
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Customers.Add(customer);
            TenantSyncService.SaveAndSync(db);
            return Ok(customer.CustomerId);
        }

        /// <summary>Update an existing customer</summary>
        [RequireAccessLevel(SystemModule.CustomerManagement, AccessLevel.Manage)]
        [HttpPut("{id:int}")]
        public IActionResult UpdateCustomer(int id, UpdateCustomerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(request.ContactNumber))
                return BadRequest(new { message = "Contact number is required." });

            if (!Validation.IsValidPhMobileNumber(request.ContactNumber))
                return BadRequest(new { message = "Invalid PH mobile number format." });

            if (!string.IsNullOrWhiteSpace(request.EmailAddress) && 
                !Validation.IsValidEmail(request.EmailAddress))
                return BadRequest(new { message = "Invalid email address format." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var customer = db.Customers.Find(id);
            if (customer is null || !customer.IsActive)
                return NotFound(new { message = "Customer not found." });

            customer.FullName = request.FullName.Trim();
            customer.ContactNumber = request.ContactNumber.Trim();
            customer.EmailAddress = string.IsNullOrWhiteSpace(request.EmailAddress) ? null : request.EmailAddress.Trim();
            customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

            TenantSyncService.SaveAndSync(db);
            return Ok();
        }

        /// <summary>Delete a customer (soft delete)</summary>
        [RequireAccessLevel(SystemModule.CustomerManagement, AccessLevel.Manage)]
        [HttpDelete("{id:int}")]
        public IActionResult DeleteCustomer(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var customer = db.Customers.Find(id);
            if (customer is null)
                return NotFound(new { message = "Customer not found." });

            customer.IsActive = false;
            TenantSyncService.SaveAndSync(db);
            return Ok();
        }
    }
}
