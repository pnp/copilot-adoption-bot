namespace Common.Engine.Models;

/// <summary>
/// Bot user information with Azure AD identification.
/// </summary>
public class BotUser
{
    public string UserId { get; set; } = string.Empty;
    public bool IsAzureAdUserId { get; set; } = false;
}
