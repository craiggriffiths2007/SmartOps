using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using SmartOps.Web.Models;

namespace SmartOps.Web.Services;

public static class EstimateAuthorization
{
    // Callers must first load the contract and estimate using their staff/insurer access rules.
    public static async Task AuthorizeAsync(AppDBContext db, Contract contract, Estimate estimate, string actor)
    {
        if (contract.IsCancelled || estimate.ContractId != contract.Id || estimate.TenantId != contract.TenantId)
            throw new InvalidOperationException("This estimate cannot be authorized for this contract.");
        if (estimate.IsAuthorized && contract.SelectedEstimateId == estimate.Id)
            return;

        estimate.AuthorizedUtc ??= DateTime.UtcNow;
        estimate.AuthorizedBy ??= actor;
        contract.SelectedEstimateId = estimate.Id;
        var targetStatus = contract.Status < ContractStatus.Authorised ? ContractStatus.Authorised : contract.Status;
        var update = ContractUpdateHelper.Create(contract, targetStatus);
        update.Title = "Estimate authorized";
        update.Description = $"Estimate #{estimate.Id} ({estimate.ServiceLevel}) authorized by {actor}. Total including VAT: {estimate.Total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-GB"))}.";
        update.CustomerVisible = true;
        db.ContractUpdates.Add(update);
        await db.SaveChangesAsync();
    }
}
