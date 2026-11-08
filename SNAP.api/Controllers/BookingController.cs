using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// Booking management API - handles all booking CRUD operations, availability checking,
    /// overlap validation, and add-ons management. Coordinates between customers, packages,
    /// photographers, and inventory.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequireAccessLevel(SystemModule.BookingScheduling, AccessLevel.AssignedView)]
    [Route("api/bookings")]
    public class BookingController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;
        private readonly BranchScope _branchScope;
        private static readonly TimeSpan StudioOpen = TimeSpan.FromHours(8);
        private static readonly TimeSpan StudioClose = TimeSpan.FromHours(18);
        private static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(30);

        public BookingController(TenantDbContextFactory tenantDbContextFactory, BranchScope branchScope)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
            _branchScope = branchScope;
        }

        /// <summary>Get all bookings for the current tenant, filtered by branch when appropriate</summary>
        [HttpGet]
        public ActionResult<List<BookingRow>> GetBookings(int? branchId = null)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var scopedBranchId = _branchScope.ResolveForRead(branchId);
            var rows = db.Bookings
                .Include(x => x.Customer)
                .Include(x => x.AssignedPhotographer)
                .Include(x => x.Branch)
                .Where(x => x.Status != BookingStatus.Cancelled)
                .Where(x => scopedBranchId == null || x.BranchId == scopedBranchId)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.BookingId)
                .Select(x => new BookingRow
                {
                    BookingId = x.BookingId,
                    CustomerName = x.Customer != null ? x.Customer.FullName : x.PackageNameSnapshot,
                    Package = x.PackageNameSnapshot,
                    SessionDate = x.SessionDate,
                    SessionStartTime = x.SessionStartTime,
                    SessionEndTime = x.SessionEndTime,
                    Photographer = x.AssignedPhotographer != null ? x.AssignedPhotographer.FullName : "Unassigned",
                    Location = x.Location,
                    Status = x.Status.ToString(),
                    Price = x.PriceSnapshot,
                    Branch = x.Branch != null ? x.Branch.BranchName : string.Empty
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Get a specific booking by ID</summary>
        [HttpGet("{id:int}")]
        public ActionResult<BookingDetailDto> GetBooking(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var booking = db.Bookings.Find(id);
            if (booking is null) return NotFound(new { message = "Booking not found." });

            return Ok(new BookingDetailDto
            {
                BookingId = booking.BookingId,
                CustomerId = booking.CustomerId,
                PackageId = booking.PackageId,
                AssignedPhotographerUserId = booking.AssignedPhotographerUserId,
                PackageNameSnapshot = booking.PackageNameSnapshot,
                PriceSnapshot = booking.PriceSnapshot,
                DurationSnapshot = booking.DurationSnapshot,
                SessionDate = booking.SessionDate,
                SessionStartTime = booking.SessionStartTime,
                SessionEndTime = booking.SessionEndTime,
                Location = booking.Location,
                Status = booking.Status,
                CreatedAt = booking.CreatedAt
            });
        }

        /// <summary>Get lookup data needed for booking creation (customers, packages, photographers, items)</summary>
        [HttpGet("lookup-data")]
        public ActionResult<BookingLookupDataDto> GetLookupData()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            return Ok(new BookingLookupDataDto
            {
                Customers = db.Customers
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.FullName)
                    .Select(x => new BookingCustomerDto
                    {
                        CustomerId = x.CustomerId,
                        FullName = x.FullName,
                        ContactNumber = x.ContactNumber,
                        EmailAddress = x.EmailAddress,
                        Address = x.Address
                    })
                    .ToList(),
                Packages = db.PhotographyPackages
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.PackageName)
                    .Select(x => new BookingPackageDto
                    {
                        PackageId = x.PackageId,
                        PackageName = x.PackageName,
                        Price = x.Price,
                        DurationHours = x.DurationHours
                    })
                    .ToList(),
                Photographers = db.TenantUsers
                    .Where(x => x.Status == UserStatus.Active && x.Role == SystemRole.Photographer)
                    .OrderBy(x => x.FullName)
                    .Select(x => new BookingPhotographerDto
                    {
                        UserId = x.UserId,
                        FullName = x.FullName
                    })
                    .ToList(),
                InventoryItems = db.InventoryItems
                    .Where(x => x.IsActive && x.IsForSale)
                    .OrderBy(x => x.ItemName)
                    .Select(x => new BookingInventoryItemDto
                    {
                        InventoryId = x.InventoryId,
                        ItemName = x.ItemName,
                        SellingPrice = x.SellingPrice
                    })
                    .ToList()
            });
        }

        /// <summary>Get available time slots for a given date, photographer, and package duration</summary>
        [HttpPost("available-slots")]
        public ActionResult<List<TimeSlotDto>> GetAvailableSlots(AvailableSlotsRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();

            var package = db.PhotographyPackages.Find(request.PackageId);
            if (package is null)
                return BadRequest(new { message = "Package not found." });

            var duration = TimeSpan.FromHours((double)package.DurationHours);
            var existingBookingsThatDay = db.Bookings
                .Where(x => x.SessionDate.Date == request.SessionDate.Date && x.Status != BookingStatus.Cancelled)
                .ToList();

            var slots = new List<TimeSlotDto>();
            var overlapService = new BookingOverlapService();

            for (var start = StudioOpen; start + duration <= StudioClose; start += SlotStep)
            {
                var candidate = new Booking
                {
                    BookingId = 0,
                    SessionDate = request.SessionDate,
                    SessionStartTime = start,
                    SessionEndTime = start + duration,
                    AssignedPhotographerUserId = request.PhotographerId,
                    Status = BookingStatus.Pending
                };

                if (!overlapService.HasOverlap(candidate, existingBookingsThatDay))
                {
                    slots.Add(new TimeSlotDto
                    {
                        StartTime = start,
                        EndTime = start + duration
                    });
                }
            }

            return Ok(slots);
        }

        /// <summary>Create a new booking</summary>
        [RequireAccessLevel(SystemModule.BookingScheduling, AccessLevel.Manage)]
        [HttpPost]
        public ActionResult<int> CreateBooking(CreateBookingRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();

            // Validate package exists
            var package = db.PhotographyPackages.Find(request.PackageId);
            if (package is null)
                return BadRequest(new { message = "Package not found." });

            int customerId;

            // If registering a new customer
            if (!string.IsNullOrEmpty(request.NewCustomerName))
            {
                if (string.IsNullOrWhiteSpace(request.NewCustomerContact))
                    return BadRequest(new { message = "Contact number is required for new customers." });

                if (!Validation.IsValidPhMobileNumber(request.NewCustomerContact))
                    return BadRequest(new { message = "Invalid PH mobile number format." });

                if (!string.IsNullOrWhiteSpace(request.NewCustomerEmail) && 
                    !Validation.IsValidEmail(request.NewCustomerEmail))
                    return BadRequest(new { message = "Invalid email address format." });

                var newCustomer = new Customer
                {
                    FullName = request.NewCustomerName.Trim(),
                    ContactNumber = request.NewCustomerContact.Trim(),
                    EmailAddress = string.IsNullOrWhiteSpace(request.NewCustomerEmail) ? null : request.NewCustomerEmail.Trim(),
                    Address = string.IsNullOrWhiteSpace(request.NewCustomerAddress) ? null : request.NewCustomerAddress.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Customers.Add(newCustomer);
                TenantSyncService.SaveAndSync(db);
                customerId = newCustomer.CustomerId;
            }
            else
            {
                if (request.CustomerId <= 0)
                    return BadRequest(new { message = "Select an existing customer or provide new customer details." });

                var customer = db.Customers.Find(request.CustomerId);
                if (customer is null || !customer.IsActive)
                    return BadRequest(new { message = "Selected customer not found or inactive." });

                customerId = request.CustomerId;
            }

            // Check for overlaps
            var overlapService = new BookingOverlapService();
            var candidate = new Booking
            {
                BookingId = 0,
                SessionDate = request.SessionDate,
                SessionStartTime = request.SessionStartTime,
                SessionEndTime = request.SessionEndTime,
                AssignedPhotographerUserId = request.AssignedPhotographerUserId,
                Status = BookingStatus.Pending
            };

            var existingBookingsThatDay = db.Bookings
                .Where(x => x.SessionDate.Date == request.SessionDate.Date && x.Status != BookingStatus.Cancelled)
                .ToList();

            if (overlapService.HasOverlap(candidate, existingBookingsThatDay))
                return BadRequest(new { message = "This time slot conflicts with an existing booking for the selected photographer." });

            // Create booking
            var booking = new Booking
            {
                CustomerId = customerId,
                PackageId = package.PackageId,
                AssignedPhotographerUserId = request.AssignedPhotographerUserId,
                PackageNameSnapshot = package.PackageName,
                PriceSnapshot = package.Price,
                DurationSnapshot = package.DurationHours,
                SessionDate = request.SessionDate,
                SessionStartTime = request.SessionStartTime,
                SessionEndTime = request.SessionEndTime,
                Location = request.Location.Trim(),
                Status = BookingStatus.Pending,
                BranchId = _branchScope.ResolveForWrite(db, request.BranchId),
                CreatedAt = DateTime.UtcNow
            };

            db.Bookings.Add(booking);
            TenantSyncService.SaveAndSync(db);

            // Apply add-ons if provided
            if (request.AddOns.Count > 0)
            {
                var addOnSelections = request.AddOns
                    .Select(x => new AddOnsService.AddOnSelection(x.InventoryId, x.Quantity, x.UnitPrice))
                    .ToList();
                AddOnsService.ApplyAddOns(db, booking.BookingId, customerId, addOnSelections);
                TenantSyncService.SaveAndSync(db);
            }

            return Ok(booking.BookingId);
        }

        /// <summary>Update an existing booking (reschedule, reassign photographer, change add-ons)</summary>
        [RequireAccessLevel(SystemModule.BookingScheduling, AccessLevel.Manage)]
        [HttpPut("{id:int}")]
        public IActionResult UpdateBooking(int id, UpdateBookingRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var booking = db.Bookings.Find(id);
            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            // Verify time slot is available (excluding current booking)
            var overlapService = new BookingOverlapService();
            var candidate = new Booking
            {
                BookingId = id,
                SessionDate = request.SessionDate,
                SessionStartTime = request.SessionStartTime,
                SessionEndTime = request.SessionEndTime,
                AssignedPhotographerUserId = request.AssignedPhotographerUserId,
                Status = request.Status
            };

            var existingBookingsThatDay = db.Bookings
                .Where(x => x.SessionDate.Date == request.SessionDate.Date && x.Status != BookingStatus.Cancelled && x.BookingId != id)
                .ToList();

            if (overlapService.HasOverlap(candidate, existingBookingsThatDay))
                return BadRequest(new { message = "This time slot conflicts with another booking." });

            // Update booking details
            booking.SessionDate = request.SessionDate;
            booking.SessionStartTime = request.SessionStartTime;
            booking.SessionEndTime = request.SessionEndTime;
            booking.Location = request.Location.Trim();
            booking.Status = request.Status;
            booking.AssignedPhotographerUserId = request.AssignedPhotographerUserId;

            // Update add-ons
            var addOnSelections = request.AddOns
                .Select(x => new AddOnsService.AddOnSelection(x.InventoryId, x.Quantity, x.UnitPrice))
                .ToList();
            AddOnsService.ApplyAddOns(db, id, booking.CustomerId, addOnSelections);

            TenantSyncService.SaveAndSync(db);
            return Ok();
        }

        /// <summary>Cancel a booking</summary>
        [RequireAccessLevel(SystemModule.BookingScheduling, AccessLevel.Manage)]
        [HttpPost("{id:int}/cancel")]
        public IActionResult CancelBooking(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var booking = db.Bookings.Find(id);
            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            if (booking.Status == BookingStatus.Cancelled)
                return BadRequest(new { message = "Booking is already cancelled." });

            // Reverse all add-ons
            AddOnsService.ReverseAllAddOns(db, id);

            // Mark as cancelled
            booking.Status = BookingStatus.Cancelled;

            TenantSyncService.SaveAndSync(db);
            return Ok();
        }

        /// <summary>Delete a booking (hard delete - use cancel for soft delete)</summary>
        [RequireAccessLevel(SystemModule.BookingScheduling, AccessLevel.Manage)]
        [HttpDelete("{id:int}")]
        public IActionResult DeleteBooking(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var booking = db.Bookings.Find(id);
            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            db.Bookings.Remove(booking);
            TenantSyncService.SaveAndSync(db);
            return Ok();
        }


    }
}
