using Common.Engine.Models;

namespace Common.Engine.Services;

/// <summary>
/// Interface for loading users directly from an external data source.
/// Does not handle caching - cache logic should be implemented by consumers.
/// </summary>
public interface IExternalUserService
{
    /// <summary>
    /// Get all users directly from external data source.
    /// </summary>
    /// <param name="maxUsers">Maximum number of users to retrieve (default 999)</param>
    Task<List<EnrichedUserInfo>> GetAllUsersAsync(int maxUsers = 999);

    /// <summary>
    /// Get a single user directly from external data source.
    /// </summary>
    /// <param name="upn">User Principal Name</param>
    Task<EnrichedUserInfo?> GetUserAsync(string upn);

    /// <summary>
    /// Get users filtered by department.
    /// </summary>
    /// <param name="department">Department name to filter by</param>
    Task<List<EnrichedUserInfo>> GetUsersByDepartmentAsync(string department);

    /// <summary>
    /// Enrich users with manager information.
    /// </summary>
    /// <param name="users">List of users to enrich</param>
    Task EnrichUsersWithManagersAsync(List<EnrichedUserInfo> users);

    /// <summary>
    /// Enrich users with license information.
    /// </summary>
    /// <param name="users">List of users to enrich with license data</param>
    Task EnrichUsersWithLicenseInfoAsync(List<EnrichedUserInfo> users);
}
