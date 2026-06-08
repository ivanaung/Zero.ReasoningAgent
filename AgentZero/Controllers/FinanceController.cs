using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Authorize]
public class FinanceController(IFinanceService financeService, ICurrentUserService currentUserService, IProjectService projectService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await financeService.GetDashboardAsync(currentUserService.UserId, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Transactions(CancellationToken cancellationToken)
    {
        await LoadFinanceSelectData(cancellationToken);
        return View(await financeService.GetTransactionsAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTransaction(FinanceTransactionFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id > 0)
        {
            await financeService.UpdateTransactionAsync(currentUserService.UserId, model, cancellationToken);
        }
        else
        {
            await financeService.CreateTransactionAsync(currentUserService.UserId, model, cancellationToken);
        }

        return RedirectToAction(nameof(Transactions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(int id, CancellationToken cancellationToken)
    {
        await financeService.DeleteTransactionAsync(currentUserService.UserId, id, cancellationToken);
        return RedirectToAction(nameof(Transactions));
    }

    public async Task<IActionResult> Accounts(CancellationToken cancellationToken)
    {
        return View(await financeService.GetAccountsAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAccount(FinanceAccount model, CancellationToken cancellationToken)
    {
        if (model.Id > 0)
        {
            await financeService.UpdateAccountAsync(currentUserService.UserId, model, cancellationToken);
        }
        else
        {
            await financeService.CreateAccountAsync(currentUserService.UserId, model, cancellationToken);
        }

        return RedirectToAction(nameof(Accounts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken cancellationToken)
    {
        await financeService.DeleteAccountAsync(currentUserService.UserId, id, cancellationToken);
        return RedirectToAction(nameof(Accounts));
    }

    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        return View(await financeService.GetCategoriesAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(FinanceCategory model, CancellationToken cancellationToken)
    {
        if (model.Id > 0)
        {
            await financeService.UpdateCategoryAsync(currentUserService.UserId, model, cancellationToken);
        }
        else
        {
            await financeService.CreateCategoryAsync(currentUserService.UserId, model, cancellationToken);
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
    {
        await financeService.DeleteCategoryAsync(currentUserService.UserId, id, cancellationToken);
        return RedirectToAction(nameof(Categories));
    }

    public async Task<IActionResult> Budgets(CancellationToken cancellationToken)
    {
        ViewBag.Categories = new SelectList(await financeService.GetCategoriesAsync(currentUserService.UserId, cancellationToken), "Id", "Name");
        return View(await financeService.GetBudgetsAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBudget(FinanceBudget model, CancellationToken cancellationToken)
    {
        await financeService.SaveBudgetAsync(currentUserService.UserId, model, cancellationToken);
        return RedirectToAction(nameof(Budgets));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBudget(int id, CancellationToken cancellationToken)
    {
        await financeService.DeleteBudgetAsync(currentUserService.UserId, id, cancellationToken);
        return RedirectToAction(nameof(Budgets));
    }

    public async Task<IActionResult> Recurring(CancellationToken cancellationToken)
    {
        await LoadFinanceSelectData(cancellationToken);
        return View(await financeService.GetRecurringItemsAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRecurring(FinanceRecurringItem model, CancellationToken cancellationToken)
    {
        await financeService.SaveRecurringItemAsync(currentUserService.UserId, model, cancellationToken);
        return RedirectToAction(nameof(Recurring));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRecurring(int id, CancellationToken cancellationToken)
    {
        await financeService.DeleteRecurringItemAsync(currentUserService.UserId, id, cancellationToken);
        return RedirectToAction(nameof(Recurring));
    }

    [HttpGet]
    public async Task<IActionResult> Export(int month, int year, CancellationToken cancellationToken)
    {
        var data = await financeService.ExportTransactionsToExcelAsync(currentUserService.UserId, month, year, cancellationToken);
        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Finance_{year}_{month:D2}.xlsx");
    }

    private async Task LoadFinanceSelectData(CancellationToken cancellationToken)
    {
        ViewBag.Accounts = new SelectList(await financeService.GetAccountsAsync(currentUserService.UserId, cancellationToken), "Id", "Name");
        ViewBag.Categories = new SelectList(await financeService.GetCategoriesAsync(currentUserService.UserId, cancellationToken), "Id", "Name");
        ViewBag.Projects = new SelectList(await projectService.GetAllAsync(), "Id", "Name");
    }
}
