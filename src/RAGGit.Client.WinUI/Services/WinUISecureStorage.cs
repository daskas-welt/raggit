using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RAGGit.Client.Maui.Services;
using Windows.Security.Credentials;

namespace RAGGit.Client.WinUI.Services;

/// <summary>
/// <see cref="ISecureStorage"/> backed by Windows Credential Locker
/// (<see cref="PasswordVault"/>). Resource name is fixed per app.
/// </summary>
public sealed class WinUISecureStorage : ISecureStorage
{
    private const string Resource = "RAGGit";

    private readonly PasswordVault _vault = new();

    public Task<string?> GetAsync(string key)
    {
        try
        {
            var credential = _vault
                .FindAllByResource(Resource)
                .FirstOrDefault(c => c.UserName == key);
            if (credential is null)
            {
                return Task.FromResult<string?>(null);
            }

            credential.RetrievePassword();
            return Task.FromResult<string?>(credential.Password);
        }
        catch (Exception)
        {
            // Vault empty on fresh install throws; treat as miss.
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string key, string value)
    {
        // Replace existing entry to avoid duplicates.
        try
        {
            foreach (
                var existing in _vault
                    .FindAllByResource(Resource)
                    .Where(c => c.UserName == key)
                    .ToList()
            )
            {
                _vault.Remove(existing);
            }
        }
        catch (Exception)
        {
            // Empty vault — nothing to remove.
        }

        _vault.Add(new PasswordCredential(Resource, key, value));
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        try
        {
            foreach (
                var existing in _vault
                    .FindAllByResource(Resource)
                    .Where(c => c.UserName == key)
                    .ToList()
            )
            {
                _vault.Remove(existing);
            }
        }
        catch (Exception)
        {
            // Already absent.
        }
    }

    public void RemoveAll()
    {
        try
        {
            foreach (var existing in _vault.FindAllByResource(Resource).ToList())
            {
                _vault.Remove(existing);
            }
        }
        catch (Exception)
        {
            // Already empty.
        }
    }
}
