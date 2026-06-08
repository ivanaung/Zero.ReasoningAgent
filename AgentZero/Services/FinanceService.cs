using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IFinanceService
{
    Task<FinanceDashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<FinanceTransactionListItemViewModel>> GetTransactionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<FinanceTransaction?> GetTransactionAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task CreateTransactionAsync(string userId, FinanceTransactionFormViewModel model, CancellationToken cancellationToken = default);
    Task UpdateTransactionAsync(string userId, FinanceTransactionFormViewModel model, CancellationToken cancellationToken = default);
    Task DeleteTransactionAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<List<FinanceAccount>> GetAccountsAsync(string userId, CancellationToken cancellationToken = default);
    Task CreateAccountAsync(string userId, FinanceAccount account, CancellationToken cancellationToken = default);
    Task UpdateAccountAsync(string userId, FinanceAccount account, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<List<FinanceCategory>> GetCategoriesAsync(string userId, CancellationToken cancellationToken = default);
    Task CreateCategoryAsync(string userId, FinanceCategory category, CancellationToken cancellationToken = default);
    Task UpdateCategoryAsync(string userId, FinanceCategory category, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<List<FinanceBudget>> GetBudgetsAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveBudgetAsync(string userId, FinanceBudget budget, CancellationToken cancellationToken = default);
    Task DeleteBudgetAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<List<FinanceRecurringItem>> GetRecurringItemsAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveRecurringItemAsync(string userId, FinanceRecurringItem item, CancellationToken cancellationToken = default);
    Task DeleteRecurringItemAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<FinanceSummaryWidgetViewModel> GetSummaryWidgetAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> GetFinanceSummaryAsync(string userId, DateTime? monthStart = null, FinanceScope? scope = null, CancellationToken cancellationToken = default);
    Task<string> GetUpcomingBillsAsync(string userId, int days = 7, CancellationToken cancellationToken = default);
    Task<string> GetProjectFinanceSummaryAsync(string userId, int projectId, CancellationToken cancellationToken = default);
    Task<string> CreateFinanceTransactionAsync(string userId, string title, decimal amount, FinanceScope scope, FinanceTransactionType type, string? categoryName = null, int? projectId = null, DateTime? transactionDate = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportTransactionsToExcelAsync(string userId, int month, int year, CancellationToken cancellationToken = default);
}

public class FinanceService(
    AppDbContext context,
    IProjectService projectService,
    IOperationalUsageStore operationalUsageStore,
    ILogger<FinanceService> logger) : IFinanceService
{
    public async Task<FinanceDashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(userId, cancellationToken);

        var now = DateTime.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var transactions = (await GetTransactionsCoreAsync(userId, cancellationToken))
            .Where(item => item.TransactionDate >= monthStart && item.TransactionDate < monthEnd)
            .OrderByDescending(item => item.TransactionDate)
            .ToList();

        var categories = await GetCategoriesAsync(userId, cancellationToken);
        var budgets = await GetBudgetsAsync(userId, cancellationToken);
        var recurring = await context.FinanceRecurringItems
            .Include(item => item.FinanceCategory)
            .Include(item => item.Project)
            .Where(item => item.UserId == userId && item.IsActive)
            .OrderBy(item => item.NextDueDate)
            .ToListAsync(cancellationToken);

        var warnings = budgets
            .Select(budget =>
            {
                var actual = transactions
                    .Where(item => item.FinanceCategoryId == budget.FinanceCategoryId
                        && item.Scope == budget.Scope
                        && item.Type == FinanceTransactionType.Expense)
                    .Sum(item => item.Amount);

                return new FinanceBudgetWarningItemViewModel
                {
                    CategoryName = budget.FinanceCategory?.Name ?? "Unknown",
                    Scope = budget.Scope,
                    BudgetAmount = budget.BudgetAmount,
                    ActualAmount = actual
                };
            })
            .Where(item => item.ActualAmount > item.BudgetAmount)
            .OrderByDescending(item => item.ActualAmount - item.BudgetAmount)
            .ToList();

        var projects = await projectService.GetAllAsync();

        return new FinanceDashboardViewModel
        {
            MonthStart = monthStart,
            MonthlyIncome = transactions.Where(item => item.Type == FinanceTransactionType.Income).Sum(item => item.Amount),
            MonthlyExpense = transactions.Where(item => item.Type == FinanceTransactionType.Expense).Sum(item => item.Amount),
            PersonalSpending = transactions.Where(item => item.Scope == FinanceScope.Personal && item.Type == FinanceTransactionType.Expense).Sum(item => item.Amount),
            BusinessSpending = transactions.Where(item => item.Scope == FinanceScope.Business && item.Type == FinanceTransactionType.Expense).Sum(item => item.Amount),
            BudgetWarningCount = warnings.Count,
            BudgetWarnings = warnings,
            UpcomingBills = recurring
                .Where(item => item.Type == FinanceCategoryType.Expense && item.NextDueDate >= now)
                .Take(8)
                .Select(MapRecurringSummary)
                .ToList(),
            NextUpcomingBill = recurring
                .Where(item => item.Type == FinanceCategoryType.Expense && item.NextDueDate >= now)
                .Select(MapRecurringSummary)
                .FirstOrDefault(),
            RecentTransactions = transactions.Take(10).Select(MapTransaction).ToList(),
            ProjectLinkedTransactions = transactions.Where(item => item.ProjectId.HasValue).Take(8).Select(MapTransaction).ToList(),
            Accounts = await GetAccountsAsync(userId, cancellationToken),
            Categories = categories,
            Projects = projects.Select(item => new ProjectOptionItemViewModel { Id = item.Id, Name = item.Name }).ToList(),
            Budgets = budgets,
            RecurringItems = recurring
        };
    }

    public async Task<List<FinanceTransactionListItemViewModel>> GetTransactionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(userId, cancellationToken);
        var items = await GetTransactionsCoreAsync(userId, cancellationToken);
        return items.Select(MapTransaction).ToList();
    }

    public async Task<FinanceTransaction?> GetTransactionAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        if (await UseOperationalStoreAsync(cancellationToken))
        {
            try
            {
                return await operationalUsageStore.GetFinanceTransactionAsync(userId, id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read finance transaction {TransactionId} from PostgreSQL. Falling back to SQLite.", id);
            }
        }

        return await context.FinanceTransactions.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    }

    public async Task CreateTransactionAsync(string userId, FinanceTransactionFormViewModel model, CancellationToken cancellationToken = default)
    {
        var transaction = new FinanceTransaction
        {
            UserId = userId,
            FinanceAccountId = model.FinanceAccountId,
            FinanceCategoryId = model.FinanceCategoryId,
            ProjectId = model.ProjectId,
            Type = model.Type,
            Scope = model.Scope,
            Amount = model.Amount,
            TransactionDate = model.TransactionDate,
            Description = model.Description?.Trim(),
            PaymentMethod = model.PaymentMethod?.Trim(),
            Notes = model.Notes?.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        context.FinanceTransactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        await MirrorFinanceTransactionAsync(transaction, cancellationToken);
    }

    public async Task UpdateTransactionAsync(string userId, FinanceTransactionFormViewModel model, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceTransactions.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == model.Id, cancellationToken);
        if (existing == null)
        {
            return;
        }

        existing.FinanceAccountId = model.FinanceAccountId;
        existing.FinanceCategoryId = model.FinanceCategoryId;
        existing.ProjectId = model.ProjectId;
        existing.Type = model.Type;
        existing.Scope = model.Scope;
        existing.Amount = model.Amount;
        existing.TransactionDate = model.TransactionDate;
        existing.Description = model.Description?.Trim();
        existing.PaymentMethod = model.PaymentMethod?.Trim();
        existing.Notes = model.Notes?.Trim();
        existing.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        await MirrorFinanceTransactionAsync(existing, cancellationToken);
    }

    public async Task DeleteTransactionAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceTransactions.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (existing == null)
        {
            return;
        }

        context.FinanceTransactions.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        await MirrorDeleteFinanceTransactionAsync(existing.Id, cancellationToken);
    }

    public async Task<List<FinanceAccount>> GetAccountsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(userId, cancellationToken);
        return await context.FinanceAccounts.Where(item => item.UserId == userId).OrderBy(item => item.Name).ToListAsync(cancellationToken);
    }

    public async Task CreateAccountAsync(string userId, FinanceAccount account, CancellationToken cancellationToken = default)
    {
        account.UserId = userId;
        account.CreatedAt = DateTime.Now;
        account.UpdatedAt = DateTime.Now;
        context.FinanceAccounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAccountAsync(string userId, FinanceAccount account, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceAccounts.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == account.Id, cancellationToken);
        if (existing == null)
        {
            return;
        }

        existing.Name = account.Name;
        existing.Type = account.Type;
        existing.Scope = account.Scope;
        existing.OpeningBalance = account.OpeningBalance;
        existing.IsActive = account.IsActive;
        existing.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAccountAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceAccounts.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (existing == null)
        {
            return;
        }
        context.FinanceAccounts.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<FinanceCategory>> GetCategoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(userId, cancellationToken);
        return await context.FinanceCategories.Where(item => item.UserId == userId).OrderBy(item => item.Name).ToListAsync(cancellationToken);
    }

    public async Task CreateCategoryAsync(string userId, FinanceCategory category, CancellationToken cancellationToken = default)
    {
        category.UserId = userId;
        category.CreatedAt = DateTime.Now;
        category.UpdatedAt = DateTime.Now;
        context.FinanceCategories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCategoryAsync(string userId, FinanceCategory category, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceCategories.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == category.Id, cancellationToken);
        if (existing == null)
        {
            return;
        }
        existing.Name = category.Name;
        existing.Type = category.Type;
        existing.Scope = category.Scope;
        existing.IsDefault = category.IsDefault;
        existing.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCategoryAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceCategories.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (existing == null)
        {
            return;
        }
        context.FinanceCategories.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<FinanceBudget>> GetBudgetsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(userId, cancellationToken);
        return await context.FinanceBudgets
            .Include(item => item.FinanceCategory)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Year)
            .ThenByDescending(item => item.Month)
            .ThenBy(item => item.FinanceCategory!.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveBudgetAsync(string userId, FinanceBudget budget, CancellationToken cancellationToken = default)
    {
        var existing = budget.Id > 0
            ? await context.FinanceBudgets.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == budget.Id, cancellationToken)
            : await context.FinanceBudgets.FirstOrDefaultAsync(item =>
                item.UserId == userId
                && item.FinanceCategoryId == budget.FinanceCategoryId
                && item.Scope == budget.Scope
                && item.Month == budget.Month
                && item.Year == budget.Year, cancellationToken);

        if (existing == null)
        {
            budget.UserId = userId;
            budget.CreatedAt = DateTime.Now;
            budget.UpdatedAt = DateTime.Now;
            context.FinanceBudgets.Add(budget);
        }
        else
        {
            existing.FinanceCategoryId = budget.FinanceCategoryId;
            existing.Scope = budget.Scope;
            existing.Month = budget.Month;
            existing.Year = budget.Year;
            existing.BudgetAmount = budget.BudgetAmount;
            existing.UpdatedAt = DateTime.Now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBudgetAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceBudgets.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (existing == null)
        {
            return;
        }
        context.FinanceBudgets.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<FinanceRecurringItem>> GetRecurringItemsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(userId, cancellationToken);
        return await context.FinanceRecurringItems
            .Include(item => item.FinanceAccount)
            .Include(item => item.FinanceCategory)
            .Include(item => item.Project)
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.NextDueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveRecurringItemAsync(string userId, FinanceRecurringItem item, CancellationToken cancellationToken = default)
    {
        var existing = item.Id > 0
            ? await context.FinanceRecurringItems.FirstOrDefaultAsync(model => model.UserId == userId && model.Id == item.Id, cancellationToken)
            : null;

        if (existing == null)
        {
            item.UserId = userId;
            item.CreatedAt = DateTime.Now;
            item.UpdatedAt = DateTime.Now;
            context.FinanceRecurringItems.Add(item);
        }
        else
        {
            existing.FinanceAccountId = item.FinanceAccountId;
            existing.FinanceCategoryId = item.FinanceCategoryId;
            existing.ProjectId = item.ProjectId;
            existing.Type = item.Type;
            existing.Scope = item.Scope;
            existing.Name = item.Name;
            existing.Amount = item.Amount;
            existing.Frequency = item.Frequency;
            existing.NextDueDate = item.NextDueDate;
            existing.IsActive = item.IsActive;
            existing.UpdatedAt = DateTime.Now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRecurringItemAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var existing = await context.FinanceRecurringItems.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (existing == null)
        {
            return;
        }
        context.FinanceRecurringItems.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinanceSummaryWidgetViewModel> GetSummaryWidgetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var dashboard = await GetDashboardAsync(userId, cancellationToken);
        return new FinanceSummaryWidgetViewModel
        {
            MonthlyIncome = dashboard.MonthlyIncome,
            MonthlyExpense = dashboard.MonthlyExpense,
            BudgetWarningCount = dashboard.BudgetWarningCount,
            NextUpcomingBill = dashboard.NextUpcomingBill
        };
    }

    public async Task<string> GetFinanceSummaryAsync(string userId, DateTime? monthStart = null, FinanceScope? scope = null, CancellationToken cancellationToken = default)
    {
        var start = monthStart.HasValue ? new DateTime(monthStart.Value.Year, monthStart.Value.Month, 1) : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var end = start.AddMonths(1);
        var items = (await GetTransactionsCoreAsync(userId, cancellationToken))
            .Where(item => item.TransactionDate >= start && item.TransactionDate < end);
        if (scope.HasValue)
        {
            items = items.Where(item => item.Scope == scope.Value);
        }
        var financeItems = items.ToList();
        var income = financeItems.Where(item => item.Type == FinanceTransactionType.Income).Sum(item => item.Amount);
        var expense = financeItems.Where(item => item.Type == FinanceTransactionType.Expense).Sum(item => item.Amount);
        var scopeLabel = scope?.ToString() ?? "All";
        return $"Finance summary for {scopeLabel} {start:yyyy-MM}: income={income:0.00}; expense={expense:0.00}; net={(income - expense):0.00}.";
    }

    public async Task<string> GetUpcomingBillsAsync(string userId, int days = 7, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Today;
        var end = now.AddDays(Math.Max(1, days));
        var items = await context.FinanceRecurringItems
            .Include(item => item.FinanceCategory)
            .Where(item => item.UserId == userId && item.IsActive && item.Type == FinanceCategoryType.Expense && item.NextDueDate >= now && item.NextDueDate <= end)
            .OrderBy(item => item.NextDueDate)
            .Take(10)
            .ToListAsync(cancellationToken);

        return items.Count == 0
            ? $"No bills are due in the next {days} days."
            : "Upcoming bills: " + string.Join(" | ", items.Select(item => $"{item.Name} {item.Amount:0.00} due {item.NextDueDate:yyyy-MM-dd} category={item.FinanceCategory?.Name ?? "None"}"));
    }

    public async Task<string> GetProjectFinanceSummaryAsync(string userId, int projectId, CancellationToken cancellationToken = default)
    {
        var items = (await GetTransactionsCoreAsync(userId, cancellationToken))
            .Where(item => item.ProjectId == projectId)
            .ToList();

        if (items.Count == 0)
        {
            return $"No finance transactions were linked to project {projectId}.";
        }

        var income = items.Where(item => item.Type == FinanceTransactionType.Income).Sum(item => item.Amount);
        var expense = items.Where(item => item.Type == FinanceTransactionType.Expense).Sum(item => item.Amount);
        var projectName = items.Select(item => item.Project?.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? $"Project {projectId}";
        return $"Project finance summary for {projectName}: income={income:0.00}; expense={expense:0.00}; net={(income - expense):0.00}.";
    }

    public async Task<string> CreateFinanceTransactionAsync(string userId, string title, decimal amount, FinanceScope scope, FinanceTransactionType type, string? categoryName = null, int? projectId = null, DateTime? transactionDate = null, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(userId, cancellationToken);

        var categories = await GetCategoriesAsync(userId, cancellationToken);
        var targetCategory = categories.FirstOrDefault(item =>
            string.Equals(item.Name, categoryName, StringComparison.OrdinalIgnoreCase)
            && (type == FinanceTransactionType.Expense ? item.Type == FinanceCategoryType.Expense : item.Type == FinanceCategoryType.Income));

        targetCategory ??= categories.FirstOrDefault(item =>
            type == FinanceTransactionType.Expense ? item.Type == FinanceCategoryType.Expense : item.Type == FinanceCategoryType.Income);

        var account = await context.FinanceAccounts
            .Where(item => item.UserId == userId && item.IsActive && item.Scope == scope)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (account == null || targetCategory == null)
        {
            return "Finance configuration is incomplete. At least one account and category are required.";
        }

        var transaction = new FinanceTransaction
        {
            UserId = userId,
            FinanceAccountId = account.Id,
            FinanceCategoryId = targetCategory.Id,
            ProjectId = projectId,
            Type = type,
            Scope = scope,
            Amount = Math.Abs(amount),
            TransactionDate = transactionDate ?? DateTime.Today,
            Description = title,
            PaymentMethod = "Manual",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        context.FinanceTransactions.Add(transaction);

        await context.SaveChangesAsync(cancellationToken);
        await MirrorFinanceTransactionAsync(transaction, cancellationToken);
        return $"Created finance transaction '{title}' amount {Math.Abs(amount):0.00} as {type} under {scope}.";
    }

    public async Task<byte[]> ExportTransactionsToExcelAsync(string userId, int month, int year, CancellationToken cancellationToken = default)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var items = (await GetTransactionsCoreAsync(userId, cancellationToken))
            .Where(item => item.TransactionDate >= start && item.TransactionDate < end)
            .OrderByDescending(item => item.TransactionDate)
            .ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Finance");
        var headers = new[] { "#", "Date", "Type", "Scope", "Account", "Category", "Project", "Description", "Payment Method", "Amount", "Notes" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2453A6");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = 2;
        foreach (var item in items)
        {
            worksheet.Cell(row, 1).Value = item.Id;
            worksheet.Cell(row, 2).Value = item.TransactionDate.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 3).Value = item.Type.ToString();
            worksheet.Cell(row, 4).Value = item.Scope.ToString();
            worksheet.Cell(row, 5).Value = item.FinanceAccount!.Name;
            worksheet.Cell(row, 6).Value = item.FinanceCategory!.Name;
            worksheet.Cell(row, 7).Value = item.Project?.Name ?? string.Empty;
            worksheet.Cell(row, 8).Value = item.Description ?? string.Empty;
            worksheet.Cell(row, 9).Value = item.PaymentMethod ?? string.Empty;
            worksheet.Cell(row, 10).Value = item.Amount;
            worksheet.Cell(row, 11).Value = item.Notes ?? string.Empty;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private IQueryable<FinanceTransaction> BuildTransactionQuery(string userId)
    {
        return context.FinanceTransactions
            .Include(item => item.FinanceAccount)
            .Include(item => item.FinanceCategory)
            .Include(item => item.Project)
            .Where(item => item.UserId == userId);
    }

    private async Task<List<FinanceTransaction>> GetTransactionsCoreAsync(string userId, CancellationToken cancellationToken)
    {
        if (await UseOperationalStoreAsync(cancellationToken))
        {
            try
            {
                return await operationalUsageStore.GetFinanceTransactionsAsync(userId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read finance transactions from PostgreSQL. Falling back to SQLite.");
            }
        }

        return await BuildTransactionQuery(userId)
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task MirrorFinanceTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken)
    {
        if (!await UseOperationalStoreAsync(cancellationToken))
        {
            return;
        }

        try
        {
            await operationalUsageStore.MirrorFinanceTransactionAsync(transaction, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mirror finance transaction {TransactionId} to PostgreSQL.", transaction.Id);
        }
    }

    private async Task MirrorDeleteFinanceTransactionAsync(int transactionId, CancellationToken cancellationToken)
    {
        if (!await UseOperationalStoreAsync(cancellationToken))
        {
            return;
        }

        try
        {
            await operationalUsageStore.MirrorDeleteFinanceTransactionAsync(transactionId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mirror finance transaction delete {TransactionId} to PostgreSQL.", transactionId);
        }
    }

    private async Task<bool> UseOperationalStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await operationalUsageStore.IsAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Operational usage store check failed for finance transactions. Falling back to SQLite.");
            return false;
        }
    }

    private static FinanceTransactionListItemViewModel MapTransaction(FinanceTransaction item)
    {
        return new FinanceTransactionListItemViewModel
        {
            Id = item.Id,
            AccountName = item.FinanceAccount?.Name ?? string.Empty,
            CategoryName = item.FinanceCategory?.Name ?? string.Empty,
            ProjectName = item.Project?.Name,
            Description = item.Description ?? string.Empty,
            PaymentMethod = item.PaymentMethod ?? string.Empty,
            Type = item.Type,
            Scope = item.Scope,
            Amount = item.Amount,
            TransactionDate = item.TransactionDate
        };
    }

    private static FinanceRecurringItemSummaryViewModel MapRecurringSummary(FinanceRecurringItem item)
    {
        return new FinanceRecurringItemSummaryViewModel
        {
            Id = item.Id,
            Name = item.Name,
            CategoryName = item.FinanceCategory?.Name,
            ProjectName = item.Project?.Name,
            Amount = item.Amount,
            Scope = item.Scope,
            Frequency = item.Frequency,
            NextDueDate = item.NextDueDate
        };
    }

    private async Task EnsureDefaultsAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await context.FinanceAccounts.AnyAsync(item => item.UserId == userId, cancellationToken))
        {
            context.FinanceAccounts.AddRange(
                new FinanceAccount { UserId = userId, Name = "Personal Wallet", Type = FinanceAccountType.Cash, Scope = FinanceScope.Personal, OpeningBalance = 0m, IsActive = true },
                new FinanceAccount { UserId = userId, Name = "Business Account", Type = FinanceAccountType.Bank, Scope = FinanceScope.Business, OpeningBalance = 0m, IsActive = true });
        }

        if (!await context.FinanceCategories.AnyAsync(item => item.UserId == userId, cancellationToken))
        {
            var defaults = new (string Name, FinanceCategoryType Type, FinanceScope Scope)[]
            {
                ("Food", FinanceCategoryType.Expense, FinanceScope.Personal),
                ("Transport", FinanceCategoryType.Expense, FinanceScope.Personal),
                ("Utilities", FinanceCategoryType.Expense, FinanceScope.Personal),
                ("Shopping", FinanceCategoryType.Expense, FinanceScope.Personal),
                ("Health", FinanceCategoryType.Expense, FinanceScope.Personal),
                ("Education", FinanceCategoryType.Expense, FinanceScope.Personal),
                ("Hosting", FinanceCategoryType.Expense, FinanceScope.Business),
                ("Software", FinanceCategoryType.Expense, FinanceScope.Business),
                ("Marketing", FinanceCategoryType.Expense, FinanceScope.Business),
                ("Equipment", FinanceCategoryType.Expense, FinanceScope.Business),
                ("Travel", FinanceCategoryType.Expense, FinanceScope.Business),
                ("Tax", FinanceCategoryType.Expense, FinanceScope.Business),
                ("Salary", FinanceCategoryType.Income, FinanceScope.Personal),
                ("Client Payment", FinanceCategoryType.Income, FinanceScope.Business),
                ("Product Sales", FinanceCategoryType.Income, FinanceScope.Business),
                ("Other Income", FinanceCategoryType.Income, FinanceScope.Both)
            };

            context.FinanceCategories.AddRange(defaults.Select(item => new FinanceCategory
            {
                UserId = userId,
                Name = item.Name,
                Type = item.Type,
                Scope = item.Scope,
                IsDefault = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            }));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
