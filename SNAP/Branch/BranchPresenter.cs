using SNAP.domain.dtos;

namespace SNAP
{
    /// <summary>
    /// Server calls for the Branch Management module - the Presenter half of the same MVP split
    /// InventoryPresenter uses. BranchForm builds controls and forwards actions here; it never
    /// touches the network itself. ApiClient turns the API's 400/404 responses into
    /// InvalidOperationException, so business-rule rejections ("promote another branch first")
    /// surface to the View exactly like any other failure.
    /// </summary>
    public class BranchPresenter
    {
        public List<BranchRow> GetBranches() =>
            ApiClient.Get<List<BranchRow>>("/api/branches");

        public List<BranchRow> GetActiveBranches() =>
            ApiClient.Get<List<BranchRow>>("/api/branches/active");

        public BranchDetailDto GetBranch(int branchId) =>
            ApiClient.Get<BranchDetailDto>($"/api/branches/{branchId}");

        public void AddBranch(string branchName, string? address, string? contactNumber, bool isMainBranch) =>
            ApiClient.Post("/api/branches", new CreateBranchRequest
            {
                BranchName = branchName,
                Address = address,
                ContactNumber = contactNumber,
                IsMainBranch = isMainBranch
            });

        public void UpdateBranch(int branchId, string branchName, string? address, string? contactNumber, bool isMainBranch, bool isActive) =>
            ApiClient.Put($"/api/branches/{branchId}", new UpdateBranchRequest
            {
                BranchName = branchName,
                Address = address,
                ContactNumber = contactNumber,
                IsMainBranch = isMainBranch,
                IsActive = isActive
            });
    }
}
