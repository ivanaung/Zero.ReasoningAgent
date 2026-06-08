using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IUserAccountService
{
    Task EnsureDefaultAdminAsync(CancellationToken cancellationToken = default);
    Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<AppUser> FindOrProvisionGoogleUserAsync(string googleSubjectId, string email, string displayName, string? preferredUserId = null, CancellationToken cancellationToken = default);
}

public class UserAccountService(AppDbContext context) : IUserAccountService
{
    public const string DefaultAdminUsername = "admin";
    public const string DefaultAdminPassword = "Admin@123";

    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public async Task EnsureDefaultAdminAsync(CancellationToken cancellationToken = default)
    {
        if (await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = new AppUser
        {
            Id = Guid.NewGuid().ToString("n"),
            Username = DefaultAdminUsername,
            Email = null,
            DisplayName = "System Administrator",
            Role = AppRoles.Admin,
            AuthMode = AppAuthModes.Local,
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        admin.PasswordHash = _passwordHasher.HashPassword(admin, DefaultAdminPassword);

        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim();
        var user = await context.Users.FirstOrDefaultAsync(item => item.Username == normalizedUsername && item.IsActive, cancellationToken);
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    public async Task<AppUser> FindOrProvisionGoogleUserAsync(string googleSubjectId, string email, string displayName, string? preferredUserId = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(preferredUserId))
        {
            var preferredUser = await context.Users.FirstOrDefaultAsync(item => item.Id == preferredUserId && item.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("The current signed-in user could not be found.");

            var conflictingLinkedAccount = await context.UserGoogleAccounts
                .FirstOrDefaultAsync(item => item.GoogleSubjectId == googleSubjectId && item.UserId != preferredUser.Id, cancellationToken);
            if (conflictingLinkedAccount != null)
            {
                var linkedConflictUser = await context.Users.FirstOrDefaultAsync(item => item.Id == conflictingLinkedAccount.UserId, cancellationToken);
                if (linkedConflictUser == null || !IsMergeableGoogleOnlyUser(linkedConflictUser))
                {
                    throw new InvalidOperationException("This Google account is already linked to another app user.");
                }

                context.UserGoogleAccounts.Remove(conflictingLinkedAccount);
                var duplicateCache = await context.CachedEmailMessages
                    .Where(item => item.UserId == linkedConflictUser.Id)
                    .ToListAsync(cancellationToken);
                if (duplicateCache.Count > 0)
                {
                    context.CachedEmailMessages.RemoveRange(duplicateCache);
                }

                context.Users.Remove(linkedConflictUser);
            }

            var conflictingEmailUser = await context.Users
                .FirstOrDefaultAsync(item => item.Email == email && item.Id != preferredUser.Id, cancellationToken);
            if (conflictingEmailUser != null)
            {
                if (!IsMergeableGoogleOnlyUser(conflictingEmailUser))
                {
                    throw new InvalidOperationException("This Google email is already linked to another app user.");
                }

                var duplicateLinkedAccount = await context.UserGoogleAccounts
                    .FirstOrDefaultAsync(item => item.UserId == conflictingEmailUser.Id, cancellationToken);
                if (duplicateLinkedAccount != null)
                {
                    context.UserGoogleAccounts.Remove(duplicateLinkedAccount);
                }

                var duplicateCache = await context.CachedEmailMessages
                    .Where(item => item.UserId == conflictingEmailUser.Id)
                    .ToListAsync(cancellationToken);
                if (duplicateCache.Count > 0)
                {
                    context.CachedEmailMessages.RemoveRange(duplicateCache);
                }

                context.Users.Remove(conflictingEmailUser);
            }

            preferredUser.DisplayName = string.IsNullOrWhiteSpace(displayName) ? preferredUser.DisplayName : displayName;
            preferredUser.Email = email;
            preferredUser.AuthMode = MergeGoogleAuthMode(preferredUser.AuthMode);
            preferredUser.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return preferredUser;
        }

        var linkedAccount = await context.UserGoogleAccounts.FirstOrDefaultAsync(item => item.GoogleSubjectId == googleSubjectId, cancellationToken);
        if (linkedAccount != null)
        {
            var linkedUser = await context.Users.FirstOrDefaultAsync(item => item.Id == linkedAccount.UserId, cancellationToken);
            if (linkedUser != null)
            {
                linkedUser.DisplayName = string.IsNullOrWhiteSpace(displayName) ? linkedUser.DisplayName : displayName;
                linkedUser.Email = email;
                linkedUser.AuthMode = MergeGoogleAuthMode(linkedUser.AuthMode);
                linkedUser.UpdatedAtUtc = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return linkedUser;
            }
        }

        var existingByEmail = await context.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (existingByEmail != null)
        {
            existingByEmail.DisplayName = string.IsNullOrWhiteSpace(displayName) ? existingByEmail.DisplayName : displayName;
            existingByEmail.AuthMode = MergeGoogleAuthMode(existingByEmail.AuthMode);
            existingByEmail.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return existingByEmail;
        }

        var baseUsername = email.Split('@')[0];
        var username = baseUsername;
        var suffix = 1;
        while (await context.Users.AnyAsync(item => item.Username == username, cancellationToken))
        {
            suffix++;
            username = $"{baseUsername}{suffix}";
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString("n"),
            Username = username,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            Role = AppRoles.User,
            AuthMode = AppAuthModes.Google,
            IsActive = true,
            MustChangePassword = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static string MergeGoogleAuthMode(string currentMode)
    {
        return currentMode switch
        {
            AppAuthModes.Local => AppAuthModes.Both,
            AppAuthModes.Both => AppAuthModes.Both,
            _ => AppAuthModes.Google
        };
    }

    private static bool IsMergeableGoogleOnlyUser(AppUser user)
    {
        return user.Role == AppRoles.User
            && user.AuthMode == AppAuthModes.Google
            && string.IsNullOrWhiteSpace(user.PasswordHash);
    }
}
