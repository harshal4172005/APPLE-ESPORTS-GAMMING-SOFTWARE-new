using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppleEsportsErp.Application.DTOs.Reports;

namespace AppleEsportsErp.Application.Interfaces;

public interface IReportsService
{
    Task<List<ReconciliationReportDto>> GetCashReconciliationReportAsync(Guid branchId, DateTime startDate, DateTime endDate);
}
