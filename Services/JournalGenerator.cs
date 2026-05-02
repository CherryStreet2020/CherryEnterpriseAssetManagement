using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public static class JournalGenerator
    {
        public static async Task<JournalEntry> GenerateMonthlyAsync(
            AppDbContext db,
            int bookId,
            DateTime month,
            string createdBy = "system",
            int? companyId = null)
        {
            var period  = new DateTime(month.Year, month.Month, 1);
            var posting = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));

            var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId)
                       ?? throw new InvalidOperationException("Book not found.");

            var map = await db.BookGlAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.BookId == bookId)
                      ?? throw new InvalidOperationException("Book GL Account mapping not found. Go to Books → GL Accounts and fill it in.");

            if (string.IsNullOrWhiteSpace(map.DepreciationExpense) ||
                string.IsNullOrWhiteSpace(map.AccumulatedDepreciation))
            {
                throw new InvalidOperationException("DepreciationExpense and AccumulatedDepreciation GL accounts are required.");
            }

            decimal totalMonthly = await TryUseExistingDepreciationService(db, bookId, period)
                                   ?? await FallbackStraightLineMonthlyAsync(db, bookId, period, companyId);

            totalMonthly = Math.Round(totalMonthly, 2, MidpointRounding.AwayFromZero);

            var batch = $"DEP-{book.Code}-{period:yyyyMM}";

            var entry = new JournalEntry
            {
                Batch       = batch,
                PostingDate = posting,
                Reference   = $"DEP {book.Code} {period:yyyy-MM}",
                Source      = "DEP",
                Description = $"Monthly depreciation — {book.Name} {period:yyyy-MM}",
                CreatedUtc  = DateTime.UtcNow
            };

            entry.Lines = new()
            {
                new JournalLine
                {
                    Account     = map.DepreciationExpense!,
                    Description = "Depreciation expense",
                    Debit       = totalMonthly,
                    Credit      = 0m
                },
                new JournalLine
                {
                    Account     = map.AccumulatedDepreciation!,
                    Description = "Accumulated depreciation",
                    Debit       = 0m,
                    Credit      = totalMonthly
                }
            };

            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        }

        private static async Task<decimal?> TryUseExistingDepreciationService(AppDbContext db, int bookId, DateTime period)
        {
            var svcType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "DepreciationService");

            if (svcType == null) return null;

            object? svc = null;
            try
            {
                var ctor = svcType.GetConstructors()
                    .FirstOrDefault(c =>
                    {
                        var ps = c.GetParameters();
                        return ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(typeof(AppDbContext));
                    });

                svc = ctor != null ? ctor.Invoke(new object[] { db }) : Activator.CreateInstance(svcType);
            }
            catch
            {
            }

            if (svc == null) return null;

            var method = svcType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name is "CalculateMonthly" or "Calculate" &&
                                     m.GetParameters().Any(p => p.ParameterType == typeof(int)) &&
                                     m.GetParameters().Any(p => p.ParameterType == typeof(DateTime)));

            if (method == null) return null;

            object? result;
            try
            {
                var parameters = method.GetParameters();
                object[] args = parameters.Length switch
                {
                    2 => new object[] { bookId, period },
                    3 => new object[] { bookId, period, null! },
                    _ => new object[] { bookId, period }
                };

                result = method.Invoke(svc, args);
            }
            catch
            {
                return null;
            }

            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                var prop = task.GetType().GetProperty("Result");
                result = prop?.GetValue(task);
            }

            if (result is System.Collections.IEnumerable rows)
            {
                decimal sum = 0m;
                foreach (var r in rows)
                {
                    var t = r.GetType();
                    var val = GetDecimalByNames(r, t, "Monthly", "Depreciation", "Amount");
                    sum += val;
                }
                return sum;
            }

            return null;
        }

        private static async Task<decimal> FallbackStraightLineMonthlyAsync(AppDbContext db, int bookId, DateTime period, int? companyId = null)
        {
            _ = await db.Books.FirstAsync(b => b.Id == bookId);

            var assetsQuery = db.Assets.AsNoTracking().AsQueryable();
            if (companyId.HasValue)
                assetsQuery = assetsQuery.Where(a => a.CompanyId == companyId);

            var assets   = await assetsQuery.ToListAsync();
            var monthEnd = new DateTime(period.Year, period.Month, DateTime.DaysInMonth(period.Year, period.Month));

            decimal total = 0m;
            foreach (var a in assets)
            {
                var t = a.GetType();

                var cost    = GetDecimalByNames(a, t, "Cost", "Acquisition", "AcquisitionCost", "AcqCost");
                var salvage = GetDecimalByNames(a, t, "Salvage", "SalvageValue");
                var life    = (int)GetDecimalByNames(a, t, "LifeMonths", "UsefulLifeMonths", "Life", "LifeMo");

                var dInSvc = GetDateByNames(a, t, "InServiceDate", "InService", "PlacedInService", "ServiceDate")
                             ?? DateTime.MinValue;

                if (life <= 0) continue;
                if (dInSvc > monthEnd) continue;

                var basis = cost - salvage;
                if (basis <= 0) continue;

                var monthly = basis / life;
                total += monthly;
            }

            return total;
        }

        private static decimal GetDecimalByNames(object obj, Type t, params string[] names)
        {
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (p != null)
                {
                    var v = p.GetValue(obj);
                    if (v == null) continue;
                    try { return Convert.ToDecimal(v); } catch { }
                }
            }
            return 0m;
        }

        private static DateTime? GetDateByNames(object obj, Type t, params string[] names)
        {
            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (p != null)
                {
                    var v = p.GetValue(obj);
                    if (v == null) continue;
                    try { return Convert.ToDateTime(v); } catch { }
                }
            }
            return null;
        }
    }
}
