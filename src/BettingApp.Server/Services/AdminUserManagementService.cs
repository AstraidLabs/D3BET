using BettingApp.Domain.Entities;
using BettingApp.Infrastructure.Persistence;
using BettingApp.Server.Configuration;
using BettingApp.Server.Data;
using BettingApp.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BettingApp.Server.Services;

public sealed class AdminUserManagementService(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ServerIdentityDbContext identityDbContext,
    BettingDbContext bettingDbContext,
    D3CreditAdminSettingsStore d3CreditAdminSettingsStore)
{
    public async Task<AdminUserListResponse> GetUsersAsync(
        string? search,
        string? role,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user =>
                (user.UserName != null && user.UserName.Contains(term)) ||
                (user.Email != null && user.Email.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = role.Trim();
            query = query.Where(user =>
                identityDbContext.UserRoles
                    .Join(
                        identityDbContext.Roles,
                        userRole => userRole.RoleId,
                        identityRole => identityRole.Id,
                        (userRole, identityRole) => new { userRole.UserId, RoleName = identityRole.Name })
                    .Any(link => link.UserId == user.Id && link.RoleName == normalizedRole));
        }

        query = (sort ?? "user_name_asc").Trim().ToLowerInvariant() switch
        {
            "user_name_desc" => query.OrderByDescending(user => user.UserName),
            "email_asc" => query.OrderBy(user => user.Email).ThenBy(user => user.UserName),
            "email_desc" => query.OrderByDescending(user => user.Email).ThenBy(user => user.UserName),
            "status_desc" => query.OrderByDescending(user => user.EmailConfirmed).ThenBy(user => user.UserName),
            _ => query.OrderBy(user => user.UserName)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.EmailConfirmed,
                user.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToArray();
        var userRoles = await identityDbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(
                identityDbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                identityRole => identityRole.Id,
                (userRole, identityRole) => new { userRole.UserId, RoleName = identityRole.Name ?? string.Empty })
            .ToListAsync(cancellationToken);

        var rolesLookup = userRoles
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.RoleName).OrderBy(name => name).ToArray());

        var settings = await d3CreditAdminSettingsStore.LoadAsync(cancellationToken);
        var userNameLookup = users
            .Select(user => new { UserId = user.Id, LookupName = NormalizeLookupName(user.UserName ?? user.Id) })
            .ToArray();
        var bettorNames = userNameLookup.Select(item => item.LookupName).Distinct().ToArray();

        var bettorSummaries = await bettingDbContext.Bettors
            .AsNoTracking()
            .Where(bettor => bettorNames.Contains(bettor.Name.ToLower()))
            .Select(bettor => new
            {
                LookupName = bettor.Name.ToLower(),
                bettor.Id,
                WalletBalance = bettor.Wallet != null ? bettor.Wallet.Balance : 0m,
                CreditCode = bettor.Wallet != null ? bettor.Wallet.CreditCode : settings.CreditCode,
                BetCount = bettor.Bets.Count,
                LastBetPlacedAtUtc = bettor.Bets.Max(bet => (DateTime?)bet.PlacedAtUtc)
            })
            .ToListAsync(cancellationToken);

        var bettorLookup = bettorSummaries.ToDictionary(item => item.LookupName, item => item);
        var availableRoles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(roleItem => roleItem.Name)
            .Select(roleItem => roleItem.Name ?? string.Empty)
            .Where(roleName => roleName != string.Empty)
            .ToArrayAsync(cancellationToken);

        var items = users
            .Select(user =>
            {
                var lookupName = NormalizeLookupName(user.UserName ?? user.Id);
                bettorLookup.TryGetValue(lookupName, out var bettor);
                rolesLookup.TryGetValue(user.Id, out var rolesForUser);

                return new AdminUserListItemResponse(
                    user.Id,
                    user.UserName ?? user.Id,
                    user.Email,
                    user.EmailConfirmed,
                    user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    rolesForUser ?? [],
                    bettor?.Id,
                    bettor?.WalletBalance ?? 0m,
                    bettor?.CreditCode ?? settings.CreditCode,
                    bettor?.BetCount ?? 0,
                    bettor?.LastBetPlacedAtUtc);
            })
            .ToArray();

        return new AdminUserListResponse(page, pageSize, totalCount, availableRoles, items);
    }

    public async Task<AdminUserDetailResponse> GetUserDetailAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Vybraný uživatel už na serveru neexistuje.");

        var roles = await userManager.GetRolesAsync(user);
        var availableRoles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(roleItem => roleItem.Name)
            .Select(roleItem => roleItem.Name ?? string.Empty)
            .Where(roleName => roleName != string.Empty)
            .ToArrayAsync(cancellationToken);
        var settings = await d3CreditAdminSettingsStore.LoadAsync(cancellationToken);

        var bettor = await FindBettorForUserAsync(user.UserName ?? user.Id, cancellationToken);
        var wallet = bettor?.Wallet ?? new BettorWallet
        {
            BettorId = bettor?.Id ?? Guid.Empty,
            CreditCode = settings.CreditCode,
            LastMoneyToCreditRate = settings.BaseCreditsPerCurrencyUnit,
            LastCreditToMoneyRate = settings.BaseCurrencyUnitsPerCredit
        };

        var bets = bettor is null
            ? []
            : await bettingDbContext.Bets
                .AsNoTracking()
                .Where(bet => bet.BettorId == bettor.Id)
                .OrderByDescending(bet => bet.PlacedAtUtc)
                .Take(50)
                .Select(bet => new AdminUserBetResponse(
                    bet.Id,
                    bet.BettingMarketId,
                    bet.EventName,
                    bet.Odds,
                    bet.Stake,
                    bet.StakeCurrencyCode,
                    bet.StakeRealMoneyEquivalent,
                    bet.PotentialPayout,
                    bet.OutcomeStatus,
                    bet.IsPayoutProcessed,
                    bet.PlacedAtUtc))
                .ToArrayAsync(cancellationToken);

        var transactions = bettor is null
            ? []
            : await bettingDbContext.D3CreditTransactions
                .AsNoTracking()
                .Where(transaction => transaction.BettorId == bettor.Id)
                .OrderByDescending(transaction => transaction.CreatedAtUtc)
                .Take(50)
                .Select(transaction => new AdminUserCreditTransactionResponse(
                    transaction.Id,
                    transaction.Type,
                    transaction.CreditAmount,
                    transaction.RealMoneyAmount,
                    transaction.RealCurrencyCode,
                    transaction.Description,
                    transaction.Reference,
                    transaction.CreatedAtUtc))
                .ToArrayAsync(cancellationToken);

        var withdrawals = bettor is null
            ? []
            : await bettingDbContext.CreditWithdrawalRequests
                .AsNoTracking()
                .Where(withdrawal => withdrawal.BettorId == bettor.Id)
                .OrderByDescending(withdrawal => withdrawal.RequestedAtUtc)
                .Take(50)
                .Select(withdrawal => new CreditWithdrawalResponse(
                    withdrawal.Id,
                    withdrawal.BettorId,
                    withdrawal.CreditAmount,
                    withdrawal.RealMoneyAmount,
                    withdrawal.RealCurrencyCode,
                    withdrawal.CreditToMoneyRateApplied,
                    withdrawal.Status,
                    withdrawal.Reference,
                    withdrawal.Reason,
                    withdrawal.ProcessedReason,
                    withdrawal.IsAutoProcessed,
                    withdrawal.RequestedAtUtc,
                    withdrawal.ProcessedAtUtc,
                    null))
                .ToArrayAsync(cancellationToken);

        var receipts = bettor is null
            ? []
            : await bettingDbContext.ElectronicReceipts
                .AsNoTracking()
                .Where(receipt => receipt.BettorId == bettor.Id)
                .OrderByDescending(receipt => receipt.IssuedAtUtc)
                .Take(50)
                .Select(receipt => new ElectronicReceiptResponse(
                    receipt.Id,
                    receipt.Type,
                    receipt.DocumentNumber,
                    receipt.Title,
                    receipt.Summary,
                    receipt.CreditAmount,
                    receipt.RealMoneyAmount,
                    receipt.RealCurrencyCode,
                    receipt.MoneyToCreditRate,
                    receipt.CreditToMoneyRate,
                    receipt.Reference,
                    receipt.IssuedAtUtc))
                .ToArrayAsync(cancellationToken);

        return new AdminUserDetailResponse(
            user.Id,
            user.UserName ?? user.Id,
            user.Email,
            user.EmailConfirmed,
            user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            roles.OrderBy(roleName => roleName).ToArray(),
            availableRoles,
            bettor?.Id,
            new AdminUserWalletResponse(
                bettor?.Id,
                bettor?.Name ?? (user.UserName ?? user.Id),
                bettor?.Wallet?.Balance ?? 0m,
                bettor?.Wallet?.CreditCode ?? settings.CreditCode,
                bettor?.Wallet?.LastMoneyToCreditRate ?? settings.BaseCreditsPerCurrencyUnit,
                bettor?.Wallet?.LastCreditToMoneyRate ?? settings.BaseCurrencyUnitsPerCredit),
            bets,
            transactions,
            withdrawals,
            receipts);
    }

    public async Task<AdminUserDetailResponse> CreateUserAsync(SaveAdminUserRequest request, CancellationToken cancellationToken)
    {
        ValidateRequiredText(request.UserName, "Uživatelské jméno");
        ValidateRequiredText(request.Email, "E-mail");
        ValidateRequiredText(request.Password, "Heslo");
        ValidateRoles(request.Roles);

        var user = new IdentityUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = request.EmailConfirmed,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password!.Trim());
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(error => error.Description)));
        }

        var roleResult = await userManager.AddToRolesAsync(user, request.Roles.Distinct(StringComparer.Ordinal).ToArray());
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(error => error.Description)));
        }

        await EnsureBettorProfileAsync(user.UserName ?? user.Id, cancellationToken);
        return await GetUserDetailAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserDetailResponse> UpdateUserAsync(string userId, SaveAdminUserRequest request, CancellationToken cancellationToken)
    {
        ValidateRequiredText(request.UserName, "Uživatelské jméno");
        ValidateRequiredText(request.Email, "E-mail");
        ValidateRoles(request.Roles);

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Uživatel už na serveru neexistuje.");

        var previousUserName = user.UserName ?? user.Id;
        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim();

        var existingUserWithName = await userManager.FindByNameAsync(normalizedUserName);
        if (existingUserWithName is not null && !string.Equals(existingUserWithName.Id, user.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Takové uživatelské jméno už v systému existuje.");
        }

        var existingUserWithEmail = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUserWithEmail is not null && !string.Equals(existingUserWithEmail.Id, user.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Takový e-mail už v systému existuje.");
        }

        user.UserName = normalizedUserName;
        user.Email = normalizedEmail;
        user.EmailConfirmed = request.EmailConfirmed;
        user.LockoutEnabled = true;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (await userManager.HasPasswordAsync(user))
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await userManager.ResetPasswordAsync(user, resetToken, request.Password.Trim());
                if (!passwordResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(" ", passwordResult.Errors.Select(error => error.Description)));
                }
            }
            else
            {
                var addPasswordResult = await userManager.AddPasswordAsync(user, request.Password.Trim());
                if (!addPasswordResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(" ", addPasswordResult.Errors.Select(error => error.Description)));
                }
            }
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        var requestedRoles = request.Roles.Distinct(StringComparer.Ordinal).ToArray();
        var rolesToRemove = existingRoles.Except(requestedRoles, StringComparer.Ordinal).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", removeResult.Errors.Select(error => error.Description)));
            }
        }

        var rolesToAdd = requestedRoles.Except(existingRoles, StringComparer.Ordinal).ToArray();
        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", addResult.Errors.Select(error => error.Description)));
            }
        }

        await SyncBettorProfileAsync(previousUserName, normalizedUserName, cancellationToken);
        return await GetUserDetailAsync(user.Id, cancellationToken);
    }

    public async Task DeleteUserAsync(string userId, string actorUserId, CancellationToken cancellationToken)
    {
        if (string.Equals(userId, actorUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Právě přihlášený administrátor nemůže smazat sám sebe.");
        }

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Uživatel už na serveru neexistuje.");

        var bettor = await FindBettorForUserAsync(user.UserName ?? user.Id, cancellationToken);
        if (bettor is not null)
        {
            var transactions = await bettingDbContext.D3CreditTransactions
                .Where(transaction => transaction.BettorId == bettor.Id)
                .ToListAsync(cancellationToken);
            var withdrawals = await bettingDbContext.CreditWithdrawalRequests
                .Where(withdrawal => withdrawal.BettorId == bettor.Id)
                .ToListAsync(cancellationToken);
            var receipts = await bettingDbContext.ElectronicReceipts
                .Where(receipt => receipt.BettorId == bettor.Id)
                .ToListAsync(cancellationToken);
            var bets = await bettingDbContext.Bets
                .Where(bet => bet.BettorId == bettor.Id)
                .ToListAsync(cancellationToken);
            var wallet = await bettingDbContext.BettorWallets
                .FirstOrDefaultAsync(item => item.BettorId == bettor.Id, cancellationToken);

            if (transactions.Count > 0)
            {
                bettingDbContext.D3CreditTransactions.RemoveRange(transactions);
            }

            if (bets.Count > 0)
            {
                bettingDbContext.Bets.RemoveRange(bets);
            }

            if (withdrawals.Count > 0)
            {
                bettingDbContext.CreditWithdrawalRequests.RemoveRange(withdrawals);
            }

            if (receipts.Count > 0)
            {
                bettingDbContext.ElectronicReceipts.RemoveRange(receipts);
            }

            if (wallet is not null)
            {
                bettingDbContext.BettorWallets.Remove(wallet);
            }

            bettingDbContext.Bettors.Remove(bettor);
            await bettingDbContext.SaveChangesAsync(cancellationToken);
        }

        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", deleteResult.Errors.Select(error => error.Description)));
        }
    }

    public async Task<AdminUserDetailResponse> ActivateUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Uživatel už na serveru neexistuje.");

        user.EmailConfirmed = true;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return await GetUserDetailAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserDetailResponse> DeactivateUserAsync(string userId, string actorUserId, CancellationToken cancellationToken)
    {
        if (string.Equals(userId, actorUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Právě přihlášený administrátor nemůže deaktivovat vlastní účet.");
        }

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Uživatel už na serveru neexistuje.");

        user.EmailConfirmed = false;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return await GetUserDetailAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserDetailResponse> BlockUserAsync(string userId, string actorUserId, CancellationToken cancellationToken)
    {
        if (string.Equals(userId, actorUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Právě přihlášený administrátor nemůže zablokovat vlastní účet.");
        }

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Uživatel už na serveru neexistuje.");

        user.LockoutEnabled = true;
        var result = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return await GetUserDetailAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserDetailResponse> UnblockUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Uživatel už na serveru neexistuje.");

        user.LockoutEnabled = true;
        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return await GetUserDetailAsync(user.Id, cancellationToken);
    }

    private static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Pole '{fieldName}' je povinné.");
        }
    }

    private void ValidateRoles(IEnumerable<string> roles)
    {
        var requestedRoles = roles
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Select(roleName => roleName.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requestedRoles.Length == 0)
        {
            throw new InvalidOperationException("Vyberte alespoň jednu roli pro uživatele.");
        }

        var allowedRoles = new[] { Roles.Admin, Roles.Operator, Roles.Customer };
        if (requestedRoles.Except(allowedRoles, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException("Požadovaná role není v D3Bet podporovaná.");
        }
    }

    private async Task<Bettor?> FindBettorForUserAsync(string userName, CancellationToken cancellationToken)
    {
        var lookupName = NormalizeLookupName(userName);
        return await bettingDbContext.Bettors
            .Include(bettor => bettor.Wallet)
            .FirstOrDefaultAsync(bettor => bettor.Name.ToLower() == lookupName, cancellationToken);
    }

    private async Task EnsureBettorProfileAsync(string userName, CancellationToken cancellationToken)
    {
        if (await FindBettorForUserAsync(userName, cancellationToken) is not null)
        {
            return;
        }

        bettingDbContext.Bettors.Add(new Bettor
        {
            Name = userName
        });

        await bettingDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncBettorProfileAsync(string previousUserName, string updatedUserName, CancellationToken cancellationToken)
    {
        var existing = await FindBettorForUserAsync(previousUserName, cancellationToken);
        if (existing is not null)
        {
            var updatedLookup = NormalizeLookupName(updatedUserName);
            var conflicting = await bettingDbContext.Bettors
                .FirstOrDefaultAsync(bettor => bettor.Name.ToLower() == updatedLookup && bettor.Id != existing.Id, cancellationToken);

            if (conflicting is null)
            {
                existing.Name = updatedUserName;
                await bettingDbContext.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        await EnsureBettorProfileAsync(updatedUserName, cancellationToken);
    }

    private static string NormalizeLookupName(string value) => value.Trim().ToLowerInvariant();
}
