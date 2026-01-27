# Security Considerations

Security best practices and guidelines for the Copilot Adoption Bot.

## Table of Contents

- [Overview](#overview)
- [Secrets Management](#secrets-management)
- [Azure Resources](#azure-resources)
- [Application Security](#application-security)
- [Network Security](#network-security)
- [Monitoring and Auditing](#monitoring-and-auditing)
- [Compliance](#compliance)

## Overview

The Copilot Adoption Bot handles sensitive data including user information, authentication tokens, and organizational data. Follow these security best practices to protect your deployment.

## Secrets Management

### Development Environment

**DO:**
- Use `dotnet user-secrets` for local development
- Store secrets outside your project directory
- Use `.env.local` for frontend secrets (already in .gitignore)
- Rotate development secrets regularly

**DON'T:**
- Commit secrets to source control
- Share secrets via email or chat
- Use production credentials locally
- Store secrets in configuration files

### Production Environment

**DO:**
- Store all secrets in Azure Key Vault
- Use Key Vault references in App Service configuration:
  ```
  @Microsoft.KeyVault(SecretUri=https://your-keyvault.vault.azure.net/secrets/SecretName/)
  ```
- Enable Azure Key Vault soft delete and purge protection
- Implement proper access policies for Key Vault
- Use managed identities to access Key Vault

**DON'T:**
- Store secrets in application settings as plain text
- Use the same secrets across environments
- Share Key Vault access broadly
- Disable audit logging

### Secret Rotation

**Regular Rotation Schedule:**
- Client secrets: Every 90 days
- Storage account keys: Every 180 days
- Bot passwords: Every 90 days
- API keys: Per vendor recommendations

**Rotation Process:**
1. Generate new secret in Azure Portal
2. Add new secret to Key Vault
3. Update App Service configuration reference
4. Verify application still works
5. Revoke old secret
6. Document rotation in audit log

## Azure Resources

### App Service

**DO:**
- Enable HTTPS only
- Use TLS 1.2 or higher
- Enable App Service Authentication (EasyAuth) if applicable
- Configure custom domains with valid SSL certificates
- Enable diagnostic logging
- Set up Application Insights
- Configure health check endpoints
- Enable deployment slots for zero-downtime updates

**DON'T:**
- Allow HTTP traffic
- Expose debug endpoints in production
- Use default .azurewebsites.net domain for sensitive apps
- Disable logging

### Storage Account

**DO:**
- Enable Azure Storage firewall
- Restrict access to specific virtual networks
- Use Azure AD authentication when possible
- Enable blob soft delete
- Enable versioning for blob storage
- Configure lifecycle management for old logs
- Enable Advanced Threat Protection
- Use SAS tokens with limited permissions and expiration

**DON'T:**
- Allow public blob access
- Use storage account keys in application code
- Share connection strings
- Grant excessive permissions to SAS tokens

**Storage Account Configuration:**
```bash
# Enable firewall
az storage account update \
  --name mystorageaccount \
  --default-action Deny

# Add allowed IP
az storage account network-rule add \
  --account-name mystorageaccount \
  --ip-address 1.2.3.4
```

### Azure Key Vault

**DO:**
- Use RBAC for access control
- Enable audit logging
- Enable soft delete and purge protection
- Use separate Key Vaults per environment
- Implement least privilege access
- Use managed identities for app access

**DON'T:**
- Grant broad access to all secrets
- Disable audit logging
- Use the same Key Vault for all environments
- Allow anonymous access

**Key Vault Access Policy:**
```bash
# Grant app managed identity access to secrets
az keyvault set-policy \
  --name mykeyvault \
  --object-id <managed-identity-object-id> \
  --secret-permissions get list
```

### Application Insights

**DO:**
- Enable Application Insights for telemetry
- Configure sampling for high-volume apps
- Set up alerts for errors and anomalies
- Use log analytics for queries
- Configure data retention policies
- Mask sensitive data in logs

**DON'T:**
- Log secrets or tokens
- Log full request/response bodies
- Disable sampling without considering costs
- Ignore security alerts

## Application Security

### Authentication and Authorization

**DO:**
- Validate all authentication tokens
- Implement proper authorization checks in controllers
- Use Azure AD for authentication
- Validate API permissions on every request
- Implement rate limiting
- Use CORS policies to restrict origins

**DON'T:**
- Trust client-side authorization
- Skip token validation
- Allow anonymous access to sensitive endpoints
- Accept tokens from untrusted issuers

**Example Authorization:**
```csharp
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessageTemplateController : ControllerBase
{
    [HttpGet("GetAll")]
    [RequiredScope("access_as_user")]
    public async Task<IActionResult> GetAll()
    {
        // Authorization check
        if (!User.Identity.IsAuthenticated)
            return Unauthorized();
            
        // Implementation
    }
}
```

### Input Validation

**DO:**
- Validate all user inputs
- Sanitize HTML and JSON
- Use parameterized queries
- Validate adaptive card JSON against schema
- Implement file upload restrictions
- Set maximum request sizes

**DON'T:**
- Trust any user input
- Execute dynamic code from user input
- Allow unlimited file uploads
- Skip JSON schema validation

**Example Validation:**
```csharp
public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(request.Name))
        return BadRequest("Template name is required");
        
    // Validate JSON
    try
    {
        var card = JsonSerializer.Deserialize<AdaptiveCard>(request.JsonPayload);
        if (card.Type != "AdaptiveCard")
            return BadRequest("Invalid adaptive card");
    }
    catch (JsonException)
    {
        return BadRequest("Invalid JSON format");
    }
    
    // Process request
}
```

### Dependency Management

**DO:**
- Keep all NuGet packages updated
- Keep all npm dependencies updated
- Use Dependabot or similar tools for alerts
- Review security advisories regularly
- Use package lock files
- Audit dependencies for vulnerabilities

**DON'T:**
- Use outdated packages with known vulnerabilities
- Ignore security alerts
- Use packages from untrusted sources
- Skip dependency audits

**Check for vulnerabilities:**
```bash
# .NET
dotnet list package --vulnerable

# npm
npm audit
```

## Network Security

### Firewall Rules

**DO:**
- Configure Azure Storage firewall
- Use virtual networks when possible
- Implement NSG rules
- Allow only necessary outbound connections
- Use private endpoints for Azure services

**DON'T:**
- Allow all inbound traffic
- Use public endpoints unnecessarily
- Skip firewall configuration
- Grant broad network access

### DDoS Protection

**DO:**
- Enable Azure DDoS Protection
- Configure rate limiting in App Service
- Use Azure Front Door or CDN
- Monitor for unusual traffic patterns
- Implement throttling in API endpoints

### API Rate Limiting

**Example rate limiting:**
```csharp
[EnableRateLimiting("api")]
[ApiController]
[Route("api/[controller]")]
public class MessageTemplateController : ControllerBase
{
    // Rate limited endpoints
}

// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});
```

## Monitoring and Auditing

### Logging

**DO:**
- Log all authentication attempts
- Log all authorization failures
- Log all data access
- Log configuration changes
- Log security events
- Use structured logging

**DON'T:**
- Log secrets or tokens
- Log personally identifiable information (PII)
- Log full request/response bodies
- Disable security logging

**Example logging:**
```csharp
_logger.LogInformation(
    "User {UserId} accessed template {TemplateId}",
    userId,
    templateId
);

_logger.LogWarning(
    "Failed authentication attempt for user {UserId} from IP {IpAddress}",
    userId,
    ipAddress
);
```

### Monitoring

**DO:**
- Set up alerts for:
  - Failed authentication attempts
  - Authorization failures
  - Unusual API usage patterns
  - Error rate increases
  - Storage access anomalies
- Monitor Application Insights metrics
- Review security logs regularly
- Implement anomaly detection

**DON'T:**
- Ignore security alerts
- Skip log analysis
- Disable monitoring in production
- Wait for user reports of issues

### Audit Trail

**DO:**
- Maintain audit logs for:
  - Template creation/modification/deletion
  - Message sends
  - Configuration changes
  - User cache updates
  - Permission changes
- Store audit logs separately from application logs
- Implement log retention policies
- Protect audit logs from modification

## Compliance

### Data Privacy

**DO:**
- Understand data residency requirements
- Document what data you collect
- Implement data retention policies
- Provide data export capabilities
- Honor data deletion requests
- Comply with GDPR/CCPA if applicable

**DON'T:**
- Store unnecessary user data
- Share data with third parties without consent
- Ignore data privacy regulations
- Keep data indefinitely

### Microsoft Graph Data

**Copilot Usage Statistics:**
- Only activity dates are stored, not content
- Data retrieved from official Microsoft APIs
- Stored in your Azure Storage account
- Same privacy policies as Microsoft 365 reports

**User Cache:**
- Basic profile information only
- No sensitive personal data
- Synced from Microsoft Graph
- Can be cleared at any time

### Access Controls

**DO:**
- Implement role-based access control (RBAC)
- Use Azure AD groups for permission management
- Document access requirements
- Review access regularly
- Remove access for inactive users

**DON'T:**
- Grant admin access broadly
- Use shared accounts
- Skip access reviews
- Allow permanent elevated access

## Incident Response

### Preparation

**Create an incident response plan:**
1. Identify security contacts
2. Document escalation procedures
3. Prepare communication templates
4. Test incident response procedures
5. Maintain contact information

### Detection

**Monitor for:**
- Unusual authentication patterns
- Unexpected API usage
- Storage access anomalies
- Error rate spikes
- Security alerts from Azure

### Response

**If a security incident occurs:**
1. Isolate affected resources
2. Rotate all credentials
3. Review audit logs
4. Notify stakeholders
5. Document the incident
6. Implement remediation
7. Update security controls

### Recovery

**After an incident:**
1. Verify all systems are secure
2. Update security documentation
3. Implement lessons learned
4. Review and update procedures
5. Conduct post-incident review

## Security Checklist

### Development

- [ ] Use user secrets for local development
- [ ] Never commit secrets to source control
- [ ] Validate all user inputs
- [ ] Implement proper error handling
- [ ] Use parameterized queries
- [ ] Keep dependencies updated

### Production

- [ ] Store secrets in Azure Key Vault
- [ ] Enable HTTPS only
- [ ] Configure storage firewall
- [ ] Enable Application Insights
- [ ] Set up security monitoring
- [ ] Configure rate limiting
- [ ] Enable Azure DDoS Protection
- [ ] Implement audit logging
- [ ] Configure backup and disaster recovery
- [ ] Document security procedures

### Ongoing

- [ ] Rotate secrets regularly
- [ ] Review access controls monthly
- [ ] Update dependencies weekly
- [ ] Review security logs daily
- [ ] Test disaster recovery quarterly
- [ ] Conduct security assessments annually

## Additional Resources

- [Azure Security Best Practices](https://docs.microsoft.com/azure/security/fundamentals/best-practices-and-patterns)
- [Azure Security Baseline](https://docs.microsoft.com/security/benchmark/azure/)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Microsoft Security Development Lifecycle](https://www.microsoft.com/securityengineering/sdl)
- [Azure Key Vault Best Practices](https://docs.microsoft.com/azure/key-vault/general/best-practices)

## Next Steps

- **Setup**: Configure security features in [SETUP.md](SETUP.md)
- **Deployment**: Deploy securely using [DEPLOYMENT.md](DEPLOYMENT.md)
- **Troubleshooting**: Security-related issues in [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
