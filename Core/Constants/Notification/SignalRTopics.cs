using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Constants.Notification
{
    public static class NotificationTemplateCodes
    {
        public const string BookingCreated = "booking-created";
        public const string BookingApproved = "booking-approved";
        public const string BookingCancelled = "booking-cancelled";
        public const string BookingRejected = "booking-rejected";
    }
    public static class SignalRTopic
    {
        public const string BookingCreated = "booking-created";
        public const string BookingApproved = "booking-approved";
        public const string BookingCancelled = "booking-cancelled";
        public const string BookingRejected = "booking-rejected";
    }
}
