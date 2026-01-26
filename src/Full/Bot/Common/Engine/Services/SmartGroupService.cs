using Common.Engine.Storage;
using Microsoft.Extensions.Logging;

using Common.Engine.Models;

namespace Common.Engine.Services;

/// <summary>
/// DTO for smart group data
/// </summary>
public class SmartGroupDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string CreatedByUpn { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastResolvedDate { get; set; }
    public int? LastResolvedMemberCount { get; set; }
}

/// <summary>
/// DTO for smart group member
/// </summary>
public class SmartGroupMemberDto
{
    public string UserPrincipalName { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? Mail { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? OfficeLocation { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? CompanyName { get; set; }
    public string? ManagerUpn { get; set; }
    public string? ManagerDisplayName { get; set; }
    public string? EmployeeType { get; set; }
    public DateTimeOffset? HireDate { get; set; }
    public double? ConfidenceScore { get; set; }
    
    // License information
    public bool HasCopilotLicense { get; set; }
    
    // Copilot usage statistics
    public DateTime? CopilotLastActivityDate { get; set; }
    public DateTime? CopilotChatLastActivityDate { get; set; }
    public DateTime? TeamsCopilotLastActivityDate { get; set; }
    public DateTime? WordCopilotLastActivityDate { get; set; }
    public DateTime? ExcelCopilotLastActivityDate { get; set; }
    public DateTime? PowerPointCopilotLastActivityDate { get; set; }
    public DateTime? OutlookCopilotLastActivityDate { get; set; }
    public DateTime? OneNoteCopilotLastActivityDate { get; set; }
    public DateTime? LoopCopilotLastActivityDate { get; set; }
    public DateTime? LastCopilotStatsUpdate { get; set; }
}

/// <summary>
/// Result of smart group resolution
/// </summary>
public class SmartGroupResolutionResult
{
    public string SmartGroupId { get; set; } = null!;
    public string SmartGroupName { get; set; } = null!;
    public List<SmartGroupMemberDto> Members { get; set; } = [];
    public DateTime ResolvedAt { get; set; }
    public bool FromCache { get; set; }
}

/// <summary>
/// Service for managing smart groups and resolving their members via AI.
/// </summary>
public class SmartGroupService
{
    private readonly SmartGroupStorageManager _storageManager;
    private readonly AIFoundryService? _aiFoundryService;
    private readonly CachedUserService _userService;
    private readonly ILogger<SmartGroupService> _logger;

    public SmartGroupService(
        SmartGroupStorageManager storageManager,
        CachedUserService userService,
        ILogger<SmartGroupService> logger,
        AIFoundryService? aiFoundryService = null)
    {
        _storageManager = storageManager;
        _userService = userService;
        _aiFoundryService = aiFoundryService;
        _logger = logger;
    }

    /// <summary>
    /// Check if AI Foundry is configured (Copilot Connected mode enabled)
    /// </summary>
    public bool IsAIEnabled => _aiFoundryService != null;

    /// <summary>
    /// Create a new smart group
    /// </summary>
    public async Task<SmartGroupDto> CreateSmartGroup(string name, string description, string createdByUpn)
    {
        _logger.LogInformation($"Creating smart group '{name}' by {createdByUpn}");
        var entity = await _storageManager.CreateSmartGroup(name, description, createdByUpn);
        return MapToDto(entity);
    }

    /// <summary>
    /// Get all smart groups
    /// </summary>
    public async Task<List<SmartGroupDto>> GetAllSmartGroups()
    {
        var entities = await _storageManager.GetAllSmartGroups();
        return entities.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Get a specific smart group
    /// </summary>
    public async Task<SmartGroupDto?> GetSmartGroup(string groupId)
    {
        var entity = await _storageManager.GetSmartGroup(groupId);
        return entity != null ? MapToDto(entity) : null;
    }

    /// <summary>
    /// Update a smart group
    /// </summary>
    public async Task<SmartGroupDto> UpdateSmartGroup(string groupId, string name, string description)
    {
        _logger.LogInformation($"Updating smart group {groupId}");
        var entity = await _storageManager.UpdateSmartGroup(groupId, name, description);
        return MapToDto(entity);
    }

    /// <summary>
    /// Delete a smart group
    /// </summary>
    public async Task DeleteSmartGroup(string groupId)
    {
        _logger.LogInformation($"Deleting smart group {groupId}");
        await _storageManager.DeleteSmartGroup(groupId);
    }

    /// <summary>
    /// Resolve smart group members using AI
    /// </summary>
    /// <param name="groupId">The smart group ID</param>
    /// <param name="forceRefresh">If true, always query AI; if false, return cached results if available</param>
    public async Task<SmartGroupResolutionResult> ResolveSmartGroupMembers(string groupId, bool forceRefresh = false)
    {
        var group = await _storageManager.GetSmartGroup(groupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Smart group {groupId} not found");
        }

        // Check if we have cached results and they're recent enough (less than 1 hour old)
        if (!forceRefresh && group.LastResolvedDate.HasValue)
        {
            var cacheAge = DateTime.UtcNow - group.LastResolvedDate.Value;
            if (cacheAge.TotalHours < 1)
            {
                var cachedMembers = await _storageManager.GetCachedSmartGroupMembers(groupId);
                if (cachedMembers.Count > 0)
                {
                    _logger.LogInformation($"Returning cached members for smart group {groupId}");
                    return new SmartGroupResolutionResult
                    {
                        SmartGroupId = groupId,
                        SmartGroupName = group.Name,
                        Members = cachedMembers.Select(MapMemberToDto).ToList(),
                        ResolvedAt = group.LastResolvedDate.Value,
                        FromCache = true
                    };
                }
            }
        }

        // Resolve using AI
        if (_aiFoundryService == null)
        {
            throw new InvalidOperationException("AI Foundry is not configured. Copilot Connected mode is disabled.");
        }

        _logger.LogInformation($"Resolving smart group {groupId} using AI...");

        // Get all users with metadata from Graph
        var users = await _userService.GetAllUsersWithMetadataAsync();
        
        // Call AI to match users
        var matches = await _aiFoundryService.ResolveSmartGroupMembersAsync(group.Description, users);

        // Cache the results
        var memberCacheEntities = matches.Select(m => 
        {
            var user = users.FirstOrDefault(u => u.UserPrincipalName.Equals(m.UserPrincipalName, StringComparison.OrdinalIgnoreCase));
            return new SmartGroupMemberCacheEntity
            {
                RowKey = m.UserPrincipalName,
                DisplayName = user?.DisplayName,
                GivenName = user?.GivenName,
                Surname = user?.Surname,
                Mail = user?.Mail,
                Department = user?.Department,
                JobTitle = user?.JobTitle,
                OfficeLocation = user?.OfficeLocation,
                City = user?.City,
                Country = user?.Country,
                State = user?.State,
                CompanyName = user?.CompanyName,
                ManagerUpn = user?.ManagerUpn,
                ManagerDisplayName = user?.ManagerDisplayName,
                EmployeeType = user?.EmployeeType,
                HireDate = user?.HireDate,
                ConfidenceScore = m.ConfidenceScore,
                HasCopilotLicense = user?.HasCopilotLicense ?? false,
                // Copilot usage statistics
                CopilotLastActivityDate = user?.CopilotLastActivityDate,
                CopilotChatLastActivityDate = user?.CopilotChatLastActivityDate,
                TeamsCopilotLastActivityDate = user?.TeamsCopilotLastActivityDate,
                WordCopilotLastActivityDate = user?.WordCopilotLastActivityDate,
                ExcelCopilotLastActivityDate = user?.ExcelCopilotLastActivityDate,
                PowerPointCopilotLastActivityDate = user?.PowerPointCopilotLastActivityDate,
                OutlookCopilotLastActivityDate = user?.OutlookCopilotLastActivityDate,
                OneNoteCopilotLastActivityDate = user?.OneNoteCopilotLastActivityDate,
                LoopCopilotLastActivityDate = user?.LoopCopilotLastActivityDate,
                LastCopilotStatsUpdate = user?.LastCopilotStatsUpdate
            };
        }).ToList();

        await _storageManager.CacheSmartGroupMembers(groupId, memberCacheEntities);
        await _storageManager.UpdateSmartGroupResolution(groupId, matches.Count);

        var result = new SmartGroupResolutionResult
        {
            SmartGroupId = groupId,
            SmartGroupName = group.Name,
            Members = memberCacheEntities.Select(MapMemberToDto).ToList(),
            ResolvedAt = DateTime.UtcNow,
            FromCache = false
        };

        _logger.LogInformation($"Resolved smart group {groupId}: {result.Members.Count} members found");
        return result;
    }

    /// <summary>
    /// Preview smart group resolution without caching (for testing descriptions)
    /// </summary>
    public async Task<List<SmartGroupMemberDto>> PreviewSmartGroupMembers(string description, int maxUsers = 100)
    {
        if (_aiFoundryService == null)
        {
            throw new InvalidOperationException("AI Foundry is not configured. Copilot Connected mode is disabled.");
        }

        _logger.LogInformation($"Previewing smart group resolution for: {description}");

        // Get users with metadata from Graph
        var users = await _userService.GetAllUsersWithMetadataAsync(maxUsers);
        
        // Call AI to match users
        var matches = await _aiFoundryService.ResolveSmartGroupMembersAsync(description, users);

        return matches.Select(m => 
        {
            var user = users.FirstOrDefault(u => u.UserPrincipalName.Equals(m.UserPrincipalName, StringComparison.OrdinalIgnoreCase));
            return new SmartGroupMemberDto
            {
                UserPrincipalName = m.UserPrincipalName,
                DisplayName = user?.DisplayName,
                GivenName = user?.GivenName,
                Surname = user?.Surname,
                Mail = user?.Mail,
                Department = user?.Department,
                JobTitle = user?.JobTitle,
                OfficeLocation = user?.OfficeLocation,
                City = user?.City,
                Country = user?.Country,
                State = user?.State,
                CompanyName = user?.CompanyName,
                ManagerUpn = user?.ManagerUpn,
                ManagerDisplayName = user?.ManagerDisplayName,
                EmployeeType = user?.EmployeeType,
                HireDate = user?.HireDate,
                ConfidenceScore = m.ConfidenceScore,
                HasCopilotLicense = user?.HasCopilotLicense ?? false,
                // Copilot usage statistics
                CopilotLastActivityDate = user?.CopilotLastActivityDate,
                CopilotChatLastActivityDate = user?.CopilotChatLastActivityDate,
                TeamsCopilotLastActivityDate = user?.TeamsCopilotLastActivityDate,
                WordCopilotLastActivityDate = user?.WordCopilotLastActivityDate,
                ExcelCopilotLastActivityDate = user?.ExcelCopilotLastActivityDate,
                PowerPointCopilotLastActivityDate = user?.PowerPointCopilotLastActivityDate,
                OutlookCopilotLastActivityDate = user?.OutlookCopilotLastActivityDate,
                OneNoteCopilotLastActivityDate = user?.OneNoteCopilotLastActivityDate,
                LoopCopilotLastActivityDate = user?.LoopCopilotLastActivityDate,
                LastCopilotStatsUpdate = user?.LastCopilotStatsUpdate
            };
        }).ToList();
    }

    /// <summary>
    /// Get UPNs for a smart group (for use in sending nudges)
    /// This will automatically resolve the group if it hasn't been resolved yet or if the cache is stale
    /// </summary>
    public async Task<List<string>> GetSmartGroupUpns(string groupId)
    {
        _logger.LogInformation($"Getting UPNs for smart group {groupId}");
        
        var group = await _storageManager.GetSmartGroup(groupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Smart group {groupId} not found");
        }

        // Check if we need to resolve (never resolved, or cache is older than 1 hour)
        bool needsResolve = !group.LastResolvedDate.HasValue || 
                           (DateTime.UtcNow - group.LastResolvedDate.Value).TotalHours >= 1;

        if (needsResolve)
        {
            _logger.LogInformation($"Smart group {groupId} needs resolution (LastResolvedDate: {group.LastResolvedDate})");
            var resolution = await ResolveSmartGroupMembers(groupId, forceRefresh: false);
            return resolution.Members.Select(m => m.UserPrincipalName).ToList();
        }
        else
        {
            // Use cached results
            _logger.LogInformation($"Using cached results for smart group {groupId}");
            var cachedMembers = await _storageManager.GetCachedSmartGroupMembers(groupId);
            if (cachedMembers.Count == 0)
            {
                _logger.LogWarning($"Smart group {groupId} has LastResolvedDate but no cached members. Re-resolving...");
                var resolution = await ResolveSmartGroupMembers(groupId, forceRefresh: true);
                return resolution.Members.Select(m => m.UserPrincipalName).ToList();
            }
            return cachedMembers.Select(m => m.RowKey).ToList();
        }
    }

    private static SmartGroupDto MapToDto(SmartGroupTableEntity entity)
    {
        return new SmartGroupDto
        {
            Id = entity.RowKey,
            Name = entity.Name,
            Description = entity.Description,
            CreatedByUpn = entity.CreatedByUpn,
            CreatedDate = entity.CreatedDate,
            LastResolvedDate = entity.LastResolvedDate,
            LastResolvedMemberCount = entity.LastResolvedMemberCount
        };
    }

    private static SmartGroupMemberDto MapMemberToDto(SmartGroupMemberCacheEntity entity)
    {
        return new SmartGroupMemberDto
        {
            UserPrincipalName = entity.RowKey,
            DisplayName = entity.DisplayName,
            GivenName = entity.GivenName,
            Surname = entity.Surname,
            Mail = entity.Mail,
            Department = entity.Department,
            JobTitle = entity.JobTitle,
            OfficeLocation = entity.OfficeLocation,
            City = entity.City,
            Country = entity.Country,
            State = entity.State,
            CompanyName = entity.CompanyName,
            ManagerUpn = entity.ManagerUpn,
            ManagerDisplayName = entity.ManagerDisplayName,
            EmployeeType = entity.EmployeeType,
            HireDate = entity.HireDate,
            ConfidenceScore = entity.ConfidenceScore,
            HasCopilotLicense = entity.HasCopilotLicense,
            // Copilot usage statistics
            CopilotLastActivityDate = entity.CopilotLastActivityDate,
            CopilotChatLastActivityDate = entity.CopilotChatLastActivityDate,
            TeamsCopilotLastActivityDate = entity.TeamsCopilotLastActivityDate,
            WordCopilotLastActivityDate = entity.WordCopilotLastActivityDate,
            ExcelCopilotLastActivityDate = entity.ExcelCopilotLastActivityDate,
            PowerPointCopilotLastActivityDate = entity.PowerPointCopilotLastActivityDate,
            OutlookCopilotLastActivityDate = entity.OutlookCopilotLastActivityDate,
            OneNoteCopilotLastActivityDate = entity.OneNoteCopilotLastActivityDate,
            LoopCopilotLastActivityDate = entity.LoopCopilotLastActivityDate,
            LastCopilotStatsUpdate = entity.LastCopilotStatsUpdate
        };
    }
}

