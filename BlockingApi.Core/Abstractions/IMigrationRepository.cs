using BlockingApi.Core.Dtos;
using BlockingApi.Data.Models;
using Microsoft.AspNetCore.Http;

namespace BlockingApi.Core.Abstractions
{
  public interface IMigrationRepository
  {
    public string GetRawData();
    public string CleanData();
    public Task<string> MigrateCustomers();
    public string MigrateCustomerRelatedData();
  }
}
