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
    /// Payment management API - handles payment recording, reference number generation,
    /// payment history, and booking balance tracking.
    /// </summary>
    [ApiController]
    [Authorize]
    // No role has View/AssignedView for PaymentManagement in PermissionService - it's Full/Manage
    // or nothing (Photographer = None) - so Manage is the correct floor for every action here,
    // reads included. Without this a Photographer could read every customer's payment history via
    // a direct API call despite having zero payment access in the actual permission model.
    [RequireAccessLevel(SystemModule.PaymentManagement, AccessLevel.Manage)]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;

        public PaymentController(TenantDbContextFactory tenantDbContextFactory)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        /// <summary>Get all payments for the current tenant</summary>
        [HttpGet]
        public ActionResult<List<PaymentRow>> GetPayments()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.Payments
                .Include(x => x.Booking)
                .ThenInclude(x => x!.Customer)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.PaymentId)
                .AsEnumerable()
                .Select(x => new PaymentRow
                {
                    PaymentId = x.PaymentId,
                    BookingId = x.BookingId,
                    Booking = x.Booking != null && x.Booking.Customer != null
                        ? $"{x.Booking.Customer.FullName} - {x.Booking.PackageNameSnapshot}"
                        : (x.Booking?.PackageNameSnapshot ?? "N/A"),
                    AmountPaid = x.AmountPaid,
                    PaymentMethod = x.PaymentMethod.ToString(),
                    PaymentDate = x.PaymentDate,
                    ReferenceNumber = x.ReferenceNumber ?? string.Empty
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Get a specific payment by ID</summary>
        [HttpGet("{id:int}")]
        public ActionResult<PaymentDetailDto> GetPayment(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var payment = db.Payments.Find(id);
            if (payment is null) return NotFound(new { message = "Payment not found." });

            return Ok(new PaymentDetailDto
            {
                PaymentId = payment.PaymentId,
                BookingId = payment.BookingId,
                AmountPaid = payment.AmountPaid,
                PaymentMethod = payment.PaymentMethod,
                PaymentDate = payment.PaymentDate,
                ReferenceNumber = payment.ReferenceNumber,
                CreatedAt = payment.CreatedAt
            });
        }

        /// <summary>Get all payments for a specific booking with balance summary</summary>
        [HttpGet("booking/{bookingId:int}")]
        public ActionResult<BookingPaymentSummaryDto> GetBookingPaymentSummary(int bookingId)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var booking = db.Bookings.Find(bookingId);
            if (booking is null)
                return NotFound(new { message = "Booking not found." });

            var payments = db.Payments
                .Where(x => x.BookingId == bookingId && !x.IsDeleted)
                .OrderByDescending(x => x.PaymentDate)
                .ThenByDescending(x => x.PaymentId)
                .Select(x => new PaymentDetailDto
                {
                    PaymentId = x.PaymentId,
                    BookingId = x.BookingId,
                    AmountPaid = x.AmountPaid,
                    PaymentMethod = x.PaymentMethod,
                    PaymentDate = x.PaymentDate,
                    ReferenceNumber = x.ReferenceNumber,
                    CreatedAt = x.CreatedAt
                })
                .ToList();

            var totalPaid = payments.Sum(x => x.AmountPaid);
            var totalPrice = booking.PriceSnapshot + AddOnsService.GetAddOnsTotal(db, bookingId);
            return Ok(new BookingPaymentSummaryDto
            {
                BookingId = bookingId,
                BookingPrice = totalPrice,
                TotalPaid = totalPaid,
                RemainingBalance = totalPrice - totalPaid,
                Payments = payments
            });
        }

        /// <summary>Generate the next reference number for a payment</summary>
        [HttpGet("next-reference")]
        public ActionResult<string> GetNextReferenceNumber()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var maxNumeric = db.Payments
                .Select(p => p.ReferenceNumber)
                .AsEnumerable()
                .Where(r => !string.IsNullOrWhiteSpace(r) && r!.All(char.IsDigit))
                .Select(r => long.Parse(r!))
                .DefaultIfEmpty(-1)
                .Max();

            var nextNumber = (maxNumeric + 1).ToString("D4");
            return Ok(nextNumber);
        }

        /// <summary>Record a new payment for a booking</summary>
        [HttpPost]
        public ActionResult<CreatePaymentResponse> CreatePayment(CreatePaymentRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();

            var booking = db.Bookings.Find(request.BookingId);
            if (booking is null)
                return BadRequest(new { message = "Booking not found." });

            var totalPaid = db.Payments
                .Where(x => x.BookingId == request.BookingId && !x.IsDeleted)
                .Sum(x => x.AmountPaid);

            var remainingBalance = booking.PriceSnapshot + AddOnsService.GetAddOnsTotal(db, request.BookingId) - totalPaid;

            if (remainingBalance <= 0)
                return BadRequest(new { message = "This booking is already fully paid." });

            if (request.AmountPaid <= 0)
                return BadRequest(new { message = "Payment amount must be greater than zero." });

            // Calculate change if payment exceeds remaining balance
            var change = request.AmountPaid > remainingBalance ? request.AmountPaid - remainingBalance : 0m;
            var amountToApply = request.AmountPaid > remainingBalance ? remainingBalance : request.AmountPaid;

            var payment = new Payment
            {
                BookingId = request.BookingId,
                AmountPaid = amountToApply,
                PaymentMethod = request.PaymentMethod,
                PaymentDate = request.PaymentDate.Date,
                ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            db.Payments.Add(payment);
            TenantSyncService.SaveAndSync(db);

            return Ok(new CreatePaymentResponse
            {
                PaymentId = payment.PaymentId,
                TotalPaid = totalPaid + amountToApply,
                RemainingBalance = remainingBalance - amountToApply,
                Change = change
            });
        }

        /// <summary>Update an existing payment</summary>
        [HttpPut("{id:int}")]
        public IActionResult UpdatePayment(int id, UpdatePaymentRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var payment = db.Payments.Find(id);
            if (payment is null)
                return NotFound(new { message = "Payment not found." });

            if (request.AmountPaid <= 0)
                return BadRequest(new { message = "Payment amount must be greater than zero." });

            payment.AmountPaid = request.AmountPaid;
            payment.PaymentMethod = request.PaymentMethod;
            payment.PaymentDate = request.PaymentDate.Date;
            payment.ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();

            TenantSyncService.SaveAndSync(db);
            return Ok();
        }

        /// <summary>Delete a payment (soft delete)</summary>
        [HttpDelete("{id:int}")]
        public IActionResult DeletePayment(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var payment = db.Payments.Find(id);
            if (payment is null)
                return NotFound(new { message = "Payment not found." });

            payment.IsDeleted = true;
            TenantSyncService.SaveAndSync(db);
            return Ok();
        }
    }
}
