using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Admin;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.Materials.Vendors
{
    // Sprint 4 Phase F Wave 1 PR #4 — Vendor edit voice-ready upgrade.
    //
    // Migrated from PageModel → VoiceReadyPageModel.
    // Mutations now flow through IVendorService (Result<T> + idempotency +
    // audit, ADR-014 D2/D3/D4) so the same code path will be reusable by the
    // future voice-AI MCP tool layer.
    //
    // Read-only data (form lookups, PO history) still lives on AppDbContext
    // here — those reads have no audit/idempotency surface area.
    //
    // PR #100 (B-02) auth posture preserved: Viewer cannot edit. Admin and
    // Accountant only.
    [Authorize(Roles = "Admin,Accountant")]
    public class EditModel : VoiceReadyPageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IVendorService _vendorService;

        public EditModel(
            AppDbContext db,
            IModuleGuardService moduleGuard,
            ILookupService lookupService,
            ITenantContext tenantContext,
            IVendorService vendorService)
        {
            _db = db;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
            _vendorService = vendorService;
        }

        public Vendor Vendor { get; set; } = null!;
        public string Mode { get; set; } = "view";
        public bool IsEditMode => Mode == "edit";
        public bool IsViewMode => Mode == "view";
        public List<SelectListItem> VendorTypeOptions { get; set; } = new();
        public List<SelectListItem> PaymentTermsOptions { get; set; } = new();
        public List<PurchaseOrder> RecentPOs { get; set; } = new();
        public int POCount { get; set; }
        public decimal TotalPOValue { get; set; }
        public string ActiveTab { get; set; } = "info";

        public async Task<IActionResult> OnGetAsync(int id, string? mode, string? tab)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("vendors"))
                return RedirectToPage("/ModuleDisabled", new { module = "Vendor Management" });

            var r = await _vendorService.GetAsync(id, HttpContext.RequestAborted);
            if (r.IsFailure || r.Value is null) return RedirectToPage("/Admin/Vendors");

            Vendor = r.Value;
            Mode = mode ?? "view";
            ActiveTab = tab ?? "info";

            if (IsEditMode && !User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                Mode = "view";

            await LoadFormDataAsync();
            await LoadPurchaseHistoryAsync(id);
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int id,
            string code,
            string name,
            int vendorType,
            int paymentTerms,
            string? taxId,
            decimal? creditLimit,
            bool is1099Vendor,
            bool isPreferred,
            string? contactName,
            string? phone,
            string? fax,
            string? email,
            string? website,
            string? address,
            string? city,
            string? state,
            string? postalCode,
            string? country,
            string? notes,
            string? accountNumber,
            string? currency,
            string? legalName,
            bool isActive,
            string? activeTab,
            Guid? idempotencyKey)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                return Forbid();

            var actorUserId = ResolveActorUserId();
            var key = idempotencyKey ?? Guid.NewGuid();

            // Load current state so we can preserve fields the active tab doesn't touch.
            var current = await _vendorService.GetAsync(id, HttpContext.RequestAborted);
            if (current.IsFailure || current.Value is null)
            {
                TempData["Error"] = "Vendor not found.";
                return RedirectToPage("/Admin/Vendors");
            }
            var v = current.Value;

            if (activeTab == "contact")
            {
                var req = new UpdateVendorContactRequest(
                    ContactName: contactName,
                    Phone: phone,
                    Fax: fax,
                    Email: email,
                    Website: website,
                    Address: address,
                    City: city,
                    State: state,
                    PostalCode: postalCode,
                    Country: country);

                var r = await _vendorService.UpdateContactAsync(
                    id, req, actorUserId, key, HttpContext.RequestAborted);
                if (r.IsFailure)
                {
                    TempData["Error"] = r.Error;
                    return RedirectToPage(new { id, mode = "edit", tab = "contact" });
                }
                TempData["Success"] = $"Vendor {r.Value!.Code} contact updated.";
            }
            else
            {
                var req = new UpdateVendorInfoRequest(
                    Code: code,
                    Name: name,
                    LegalName: legalName,
                    VendorType: (VendorType)vendorType,
                    PaymentTerms: (PaymentTerms)paymentTerms,
                    TaxId: taxId,
                    CreditLimit: creditLimit,
                    Is1099Vendor: is1099Vendor,
                    IsPreferred: isPreferred,
                    AccountNumber: accountNumber,
                    Currency: currency,
                    Notes: notes,
                    IsActive: isActive);

                var r = await _vendorService.UpdateInfoAsync(
                    id, req, actorUserId, key, HttpContext.RequestAborted);
                if (r.IsFailure)
                {
                    TempData["Error"] = r.Error;
                    return RedirectToPage(new { id, mode = "edit", tab = "info" });
                }
                TempData["Success"] = $"Vendor {r.Value!.Code} updated successfully.";
            }

            return RedirectToPage(new { id, mode = "view", tab = activeTab ?? "info" });
        }

        public async Task<IActionResult> OnPostDuplicateAsync(int id, Guid? idempotencyKey)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                return Forbid();

            var actorUserId = ResolveActorUserId();
            var key = idempotencyKey ?? Guid.NewGuid();

            var r = await _vendorService.DuplicateAsync(
                id, actorUserId, key, HttpContext.RequestAborted);
            if (r.IsFailure || r.Value is null)
            {
                TempData["Error"] = r.Error ?? "Failed to duplicate vendor.";
                return RedirectToPage("/Admin/Vendors");
            }

            TempData["Success"] = $"Vendor duplicated as {r.Value.Code}.";
            return RedirectToPage(new { id = r.Value.Id, mode = "view" });
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int id, Guid? idempotencyKey)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                return Forbid();

            var actorUserId = ResolveActorUserId();
            var key = idempotencyKey ?? Guid.NewGuid();

            var r = await _vendorService.ToggleActiveAsync(
                id, actorUserId, key, HttpContext.RequestAborted);
            if (r.IsFailure)
            {
                TempData["Error"] = r.Error;
                return RedirectToPage(new { id, mode = "view" });
            }

            // Re-fetch to get the new IsActive label for the toast.
            var refreshed = await _vendorService.GetAsync(id, HttpContext.RequestAborted);
            var v = refreshed.Value;
            TempData["Success"] = v is null
                ? "Vendor status updated."
                : $"Vendor {v.Code} {(v.IsActive ? "activated" : "deactivated")}.";

            return RedirectToPage(new { id, mode = "view" });
        }

        // ── ADR-014 D1 — voice context payload ────────────────────────────
        public override VoiceContextPayload BuildContextPayload()
        {
            var b = base.BuildContextPayload();
            return new VoiceContextPayload
            {
                Route        = b.Route,
                UserId       = b.UserId,
                Roles        = b.Roles,
                TenantId     = b.TenantId,
                EntityType   = nameof(Vendor),
                EntityId     = Vendor?.Id.ToString(),
                RelatedIds   = b.RelatedIds,
                FocusedField = b.FocusedField,
                Tab          = string.IsNullOrEmpty(b.Tab) ? ActiveTab : b.Tab,
                BuiltAt      = b.BuiltAt,
            };
        }

        // ── helpers ───────────────────────────────────────────────────────
        private int ResolveActorUserId()
        {
            var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var n) ? n : 0;
        }

        private async Task LoadFormDataAsync()
        {
            VendorTypeOptions = await _lookupService.GetSelectListAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, "VendorType", null, "-- Select --");
            PaymentTermsOptions = await _lookupService.GetSelectListAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, "PaymentTerms", null, "-- Select --");
        }

        private async Task LoadPurchaseHistoryAsync(int vendorId)
        {
            var poQuery = _db.PurchaseOrders.Where(po =>
                po.VendorId == vendorId &&
                (po.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(po.CompanyId ?? 0)));
            POCount = await poQuery.CountAsync();
            TotalPOValue = await poQuery.SumAsync(po => (decimal?)po.Total) ?? 0;
            RecentPOs = await poQuery
                .OrderByDescending(po => po.OrderDate)
                .Take(10)
                .ToListAsync();
        }
    }
}
