using Microsoft.Extensions.Options;
using System.DirectoryServices;

namespace qcSolicitudDesviacion.Services;

public class ActiveDirectoryService
{
    private readonly ActiveDirectoryOptions _options;
    private readonly ILogger<ActiveDirectoryService> _logger;

    public ActiveDirectoryService(
        IOptions<ActiveDirectoryOptions> options,
        ILogger<ActiveDirectoryService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public AdUserInfo? Authenticate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var samAccountName = NormalizeLogin(username);
        var bindUser = $@"{_options.NetBiosDomain}\{samAccountName}";

        try
        {
            using var entry = new DirectoryEntry(
                _options.LdapPath,
                bindUser,
                password,
                AuthenticationTypes.Secure);

            _ = entry.NativeObject;

            using var searcher = new DirectorySearcher(entry)
            {
                Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdapFilter(samAccountName)}))",
                SearchScope = SearchScope.Subtree
            };

            searcher.PropertiesToLoad.Add("sAMAccountName");
            searcher.PropertiesToLoad.Add("displayName");
            searcher.PropertiesToLoad.Add("mail");
            searcher.PropertiesToLoad.Add("department");

            var result = searcher.FindOne();

            if (result is null)
            {
                return null;
            }

            return new AdUserInfo
            {
                LoginId = GetProperty(result, "sAMAccountName") ?? samAccountName,
                DisplayName = GetProperty(result, "displayName") ?? samAccountName,
                Email = GetProperty(result, "mail"),
                Department = GetProperty(result, "department")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error autenticando usuario AD {User}", samAccountName);
            return null;
        }
    }

    private static string NormalizeLogin(string username)
    {
        var login = username.Trim();

        if (login.Contains("\\"))
        {
            return login[(login.LastIndexOf('\\') + 1)..];
        }

        if (login.Contains("@"))
        {
            return login[..login.IndexOf('@')];
        }

        return login;
    }

    private static string? GetProperty(SearchResult result, string propertyName)
    {
        if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
        {
            return result.Properties[propertyName][0]?.ToString();
        }

        return null;
    }

    private static string EscapeLdapFilter(string value)
    {
        return value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }
}