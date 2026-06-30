using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using AppleEsportsErp.Api.Hubs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AppleEsportsErp.Api.Services;

public class DeferredBillingMonitorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DeferredBillingMonitorService> _logger;

    public DeferredBillingMonitorService(IServiceProvider services, ILogger<DeferredBillingMonitorService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeferredBillingMonitorService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOldDeferredBillsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing CheckOldDeferredBillsAsync.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CheckOldDeferredBillsAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

        // Check for bills that became exactly 1 week old in the last hour
        var oneWeekAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var oneWeekAndOneHourAgo = oneWeekAgo.AddHours(-1);

        var oldBills = await db.Bills
            .Where(b => b.IsDeferred && b.Status == BillStatus.Pending && b.CreatedAt <= oneWeekAgo && b.CreatedAt > oneWeekAndOneHourAgo)
            .ToListAsync(stoppingToken);

        if (!oldBills.Any())
            return;

        foreach (var bill in oldBills)
        {
            _logger.LogWarning("Bill {BillId} is deferred and unpaid for over a week! (Created: {CreatedAt})", bill.Id, bill.CreatedAt);
            
            var payload = new
            {
                id = Guid.NewGuid(),
                type = "Alert",
                message = $"Urgent: Deferred Bill #{bill.BillNumber} for {(string.IsNullOrEmpty(bill.CustomerName) ? "Guest" : bill.CustomerName)} is unpaid for over a week (Amount: {bill.TotalAmount}). Please collect payment.",
                level = "error",
                timestamp = DateTimeOffset.UtcNow
            };

            await hubContext.Clients.Group($"branch:{bill.BranchId}").SendAsync("ReceiveNotification", payload, cancellationToken: stoppingToken);
            
            // Also notify super admins
            await hubContext.Clients.Group("Role:SuperAdmin").SendAsync("ReceiveNotification", payload, cancellationToken: stoppingToken);
            await hubContext.Clients.Group("Role:Admin").SendAsync("ReceiveNotification", payload, cancellationToken: stoppingToken);
        }
    }
}
