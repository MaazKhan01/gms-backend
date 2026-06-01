# Hangfire Setup and Usage Guide

## Overview
Hangfire is an open-source framework for background job processing in .NET applications. It allows you to create, process, and manage background jobs with support for:
- Fire-and-forget jobs
- Delayed jobs
- Recurring jobs (scheduled tasks)
- Continuations
- Persistent storage with SQL Server

## Installation

### 1. Install Required NuGet Packages

Run these commands in the Package Manager Console or add them to your `.csproj` file:

```bash
# Core Hangfire package
dotnet add package Hangfire.Core

# ASP.NET Core integration
dotnet add package Hangfire.AspNetCore

# SQL Server storage
dotnet add package Hangfire.SqlServer
```

Or add to `.csproj`:
```xml
<PackageReference Include="Hangfire.Core" Version="1.8.9" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.9" />
<PackageReference Include="Hangfire.SqlServer" Version="1.8.9" />
```

### 2. Database Setup

Hangfire will automatically create its tables in your database on first run. The tables will be created in the `Hangfire` schema:
- `Hangfire.Job`
- `Hangfire.State`
- `Hangfire.JobParameter`
- `Hangfire.JobQueue`
- `Hangfire.Server`
- `Hangfire.Set`
- `Hangfire.Counter`
- `Hangfire.Hash`
- `Hangfire.List`
- `Hangfire.AggregatedCounter`

No manual migration needed - Hangfire handles schema creation automatically.

## Configuration

### Files Already Created:

1. **QBF.Infrastructure/Hangfire/Startup.cs**
   - Configures Hangfire services and SQL Server storage
   - Sets up Hangfire server with worker configuration

2. **QBF.Infrastructure/Hangfire/HangfireAuthorizationFilter.cs**
   - Controls access to Hangfire Dashboard
   - Currently allows all in development, requires Admin role in production

3. **QBF.Infrastructure/Hangfire/RecurringJobsConfiguration.cs**
   - Configures all recurring/scheduled jobs
   - Pre-configured with example jobs

4. **API/Program.cs**
   - Added Hangfire service registration
   - Added Hangfire dashboard middleware
   - Configured recurring jobs on startup

## Accessing the Hangfire Dashboard

Once the application is running, access the dashboard at:
```
https://localhost:{port}/hangfire
```

**Features:**
- View all jobs (succeeded, failed, processing, scheduled)
- Retry failed jobs
- Delete jobs
- View job details and execution history
- Monitor server performance
- Trigger recurring jobs manually

**Authorization:**
- Development: Open to all (for testing)
- Production: Requires Admin role

## Pre-Configured Recurring Jobs

### 1. Generate Court Slots
- **Job ID**: `generate-court-slots`
- **Schedule**: Daily at 2:00 AM (Qatar time)
- **Queue**: background
- **Purpose**: Automatically generates availability slots for all courts
- **Service**: `ISlotGenerationService.GenerateSlotsForAllCourtsAsync()`

### 2. Cleanup Expired Bookings
- **Job ID**: `cleanup-expired-bookings`
- **Schedule**: Every hour
- **Queue**: background
- **Purpose**: Remove or cancel expired/unpaid bookings
- **Note**: Placeholder - implement actual logic

### 3. Send Booking Reminders
- **Job ID**: `send-booking-reminders`
- **Schedule**: Every 30 minutes
- **Queue**: default
- **Purpose**: Send email/SMS reminders for upcoming bookings
- **Note**: Placeholder - implement actual logic

### 4. Daily Booking Report
- **Job ID**: `daily-booking-report`
- **Schedule**: Daily at 6:00 AM (Qatar time)
- **Queue**: background
- **Purpose**: Generate and send daily statistics to admins
- **Note**: Placeholder - implement actual logic

## Usage Examples

### Fire-and-Forget Jobs
Execute a job immediately in the background:

```csharp
// In a controller or service
public class BookingController : ControllerBase
{
    [HttpPost("confirm/{id}")]
    public IActionResult ConfirmBooking(Guid id)
    {
        // Process booking confirmation synchronously
        var booking = _bookingService.ConfirmBooking(id);

        // Send confirmation email asynchronously in background
        BackgroundJob.Enqueue<IEmailService>(
            x => x.SendBookingConfirmationEmailAsync(id));

        return Ok(booking);
    }
}
```

### Delayed Jobs
Schedule a job to run after a specific delay:

```csharp
// Send reminder email 24 hours before booking
BackgroundJob.Schedule<IEmailService>(
    x => x.SendBookingReminderAsync(bookingId),
    TimeSpan.FromHours(24));

// Cancel unpaid booking after 2 hours
BackgroundJob.Schedule<IBookingService>(
    x => x.AutoCancelUnpaidBookingAsync(bookingId),
    TimeSpan.FromHours(2));
```

### Recurring Jobs (Scheduled Tasks)
Add or update recurring jobs:

```csharp
// Run every day at 2 AM
RecurringJob.AddOrUpdate(
    "daily-cleanup",
    () => CleanupOldData(),
    Cron.Daily(2));

// Run every Monday at 9 AM
RecurringJob.AddOrUpdate(
    "weekly-report",
    () => GenerateWeeklyReport(),
    Cron.Weekly(DayOfWeek.Monday, 9));

// Run every 15 minutes
RecurringJob.AddOrUpdate(
    "check-status",
    () => CheckSystemStatus(),
    "*/15 * * * *");
```

### Job Continuations
Chain jobs together:

```csharp
// First job
var jobId = BackgroundJob.Enqueue<IBookingService>(
    x => x.ProcessBooking(bookingId));

// Second job runs only if first succeeds
BackgroundJob.ContinueJobWith<IEmailService>(
    jobId,
    x => x.SendConfirmationEmail(bookingId));
```

### Queue Priority
Assign jobs to different queues:

```csharp
// Critical queue (highest priority)
BackgroundJob.Enqueue<IPaymentService>(
    x => x.ProcessPayment(paymentId),
    new BackgroundJobOptions { Queue = "critical" });

// Background queue (lowest priority)
BackgroundJob.Enqueue<IReportService>(
    x => x.GenerateReport(reportId),
    new BackgroundJobOptions { Queue = "background" });
```

## Cron Expression Examples

Common cron patterns for recurring jobs:

```csharp
Cron.Minutely();                    // Every minute
Cron.Hourly();                      // Every hour at :00
Cron.Daily();                       // Every day at 00:00
Cron.Daily(14);                     // Every day at 14:00
Cron.Weekly();                      // Every Sunday at 00:00
Cron.Weekly(DayOfWeek.Monday);      // Every Monday at 00:00
Cron.Monthly();                     // First day of month at 00:00
Cron.Yearly();                      // January 1st at 00:00

// Custom expressions
"*/5 * * * *"                       // Every 5 minutes
"0 */2 * * *"                       // Every 2 hours
"30 8 * * 1-5"                      // 8:30 AM, Monday-Friday
"0 0 1 * *"                         // First day of month at midnight
"0 9,17 * * *"                      // 9 AM and 5 PM every day
```

## Best Practices

### 1. Keep Jobs Idempotent
Jobs should be safe to run multiple times:

```csharp
public async Task SendBookingReminderAsync(Guid bookingId)
{
    var booking = await _unitOfWork.Bookings.GetByIdAsync(bookingId);

    // Check if reminder already sent
    if (booking.ReminderSent)
        return;

    // Send reminder
    await _emailService.SendAsync(booking.UserEmail, "Reminder", "...");

    // Mark as sent
    booking.ReminderSent = true;
    await _unitOfWork.SaveChangesAsync();
}
```

### 2. Handle Exceptions Gracefully
```csharp
public async Task ProcessBookingAsync(Guid bookingId)
{
    try
    {
        // Process booking
        await _bookingService.ProcessAsync(bookingId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process booking {BookingId}", bookingId);
        throw; // Hangfire will retry automatically
    }
}
```

### 3. Use Appropriate Queues
- **critical**: Time-sensitive operations (payments, confirmations)
- **default**: Regular operations (notifications, updates)
- **background**: Low-priority tasks (reports, cleanup)

### 4. Set Reasonable Retry Policies
```csharp
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
public async Task SendEmailAsync(string to, string subject, string body)
{
    // Send email
}
```

### 5. Monitor Job Performance
- Check Hangfire Dashboard regularly
- Monitor failed jobs
- Set up alerts for critical job failures
- Review job execution times

## Adding New Recurring Jobs

### 1. Create the Job Method
```csharp
// In a service
public class BookingService : IBookingService
{
    public async Task AutoCancelExpiredBookingsAsync()
    {
        var expiredBookings = await _unitOfWork.Bookings
            .Query()
            .Where(b => b.Status == "Pending"
                && b.PaymentDeadline < DateTime.UtcNow)
            .ToListAsync();

        foreach (var booking in expiredBookings)
        {
            booking.Status = "Cancelled";
            booking.CancellationReason = "Payment deadline expired";
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
```

### 2. Register in RecurringJobsConfiguration.cs
```csharp
RecurringJob.AddOrUpdate<IBookingService>(
    "auto-cancel-expired-bookings",
    service => service.AutoCancelExpiredBookingsAsync(),
    Cron.Hourly,
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"),
        QueueName = "background"
    });
```

## Troubleshooting

### Issue: Jobs not executing
- Check Hangfire Dashboard for errors
- Verify database connection
- Check worker count configuration
- Review server logs

### Issue: Dashboard not accessible
- Verify URL: `/hangfire`
- Check authorization filter
- Ensure middleware is registered after authentication

### Issue: Jobs failing repeatedly
- Check exception details in dashboard
- Review job parameters
- Verify service dependencies are registered
- Check database connectivity

### Issue: High memory usage
- Reduce worker count
- Optimize job queries
- Clean up old job data regularly

## Monitoring and Maintenance

### Regular Tasks:
1. **Monitor Dashboard**: Check for failed jobs daily
2. **Review Performance**: Weekly review of job execution times
3. **Clean Old Data**: Hangfire auto-cleans, but monitor size
4. **Update Schedules**: Adjust recurring job times based on usage

### Metrics to Track:
- Jobs succeeded/failed ratio
- Average job execution time
- Queue lengths
- Server performance
- Database size (Hangfire schema)

## Production Considerations

1. **Update Authorization Filter**: Implement proper role-based authorization
2. **Configure Alerts**: Set up monitoring for job failures
3. **Scale Workers**: Adjust worker count based on load
4. **Database Maintenance**: Regular backup of Hangfire schema
5. **Logging**: Ensure all jobs log appropriately
6. **Testing**: Test all jobs in staging before production

## Additional Resources

- Official Documentation: https://docs.hangfire.io
- GitHub Repository: https://github.com/HangfireIO/Hangfire
- Dashboard Features: https://docs.hangfire.io/en/latest/configuration/using-dashboard.html
- Best Practices: https://docs.hangfire.io/en/latest/best-practices.html
