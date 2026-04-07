using Ferre.Filters;
using Ferre.Models.Catalog;
using Ferre.Services.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Ferre.Controllers;

// Controlador reemplazado por la sección de Admin; mantener si deseas ruta separada.
[RequireSession("administrador")]
public sealed class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var categories = await _categoryService.GetAllAsync();
        var model = new CategoriesIndexViewModel(categories, new CategoryFormModel(), null, "az", 1, 1, categories.Count, categories.Count);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] CategoryFormModel model)
    {
        if (!ModelState.IsValid)
        {
            var categories = await _categoryService.GetAllAsync();
            return View("Index", new CategoriesIndexViewModel(categories, model, null, "az", 1, 1, categories.Count, categories.Count));
        }

        var result = await _categoryService.CreateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo registrar la categoría.");
            var categories = await _categoryService.GetAllAsync();
            return View("Index", new CategoriesIndexViewModel(categories, model, null, "az", 1, 1, categories.Count, categories.Count));
        }

        TempData["SuccessMessage"] = "Categoría registrada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null)
        {
            TempData["ErrorMessage"] = "No se encontró la categoría solicitada.";
            return RedirectToAction(nameof(Index));
        }

        var model = new CategoryFormModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _categoryService.UpdateAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo actualizar la categoría.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Categoría actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _categoryService.DeleteAsync(id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo eliminar la categoría.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Categoría eliminada correctamente.";
        return RedirectToAction(nameof(Index));
    }
}
