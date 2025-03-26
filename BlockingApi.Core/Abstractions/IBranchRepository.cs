using BlockingApi.Data.Models;

public interface IBranchRepository
{
    Task<Branch> GetBranchById(string branchCode);
}
