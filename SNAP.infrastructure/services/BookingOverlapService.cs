using SNAP.domain.entities;

namespace SNAP.infrastructure.services
{
    public class BookingOverlapService
    {
        public bool HasOverlap(Booking candidate, IEnumerable<Booking> existingBookings)
        {
            foreach (var existing in existingBookings)
            {
                if (existing.BookingId == candidate.BookingId)
                {
                    continue;
                }

                if (existing.Status == SNAP.domain.enums.BookingStatus.Cancelled)
                {
                    continue;
                }

                if (existing.SessionDate.Date != candidate.SessionDate.Date)
                {
                    continue;
                }

                // A studio can run simultaneous sessions with different photographers; only
                // conflict when the same photographer (or the shared "unassigned" slot) is double-booked.
                if (existing.AssignedPhotographerUserId != candidate.AssignedPhotographerUserId)
                {
                    continue;
                }

                var newStart = candidate.SessionStartTime;
                var newEnd = candidate.SessionEndTime;
                var existingStart = existing.SessionStartTime;
                var existingEnd = existing.SessionEndTime;

                var overlaps = newStart < existingEnd && newEnd > existingStart;
                if (overlaps)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
