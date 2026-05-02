// TENANT SCOPING EXCEPTION: Seeding services operate cross-tenant by design.
// They populate reference/demo data for all companies during initial setup.
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Abs.FixedAssets.Services.Seeding
{
    public abstract class BaseSeedStep<TEntity> : ISeedStep where TEntity : class
    {
        protected readonly AppDbContext Context;
        protected readonly ILogger Logger;

        public abstract string StepName { get; }
        public abstract string DomainName { get; }
        public abstract string NaturalKeyDescription { get; }

        protected BaseSeedStep(AppDbContext context, ILogger logger)
        {
            Context = context;
            Logger = logger;
        }

        protected static bool StringEquals(string? a, string? b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new SeedStepResult
            {
                StepName = StepName,
                DomainName = DomainName,
                StartTime = DateTime.UtcNow
            };

            try
            {
                await OnBeforeExecuteAsync(cancellationToken);
                var seedData = GetSeedData();

                foreach (var item in seedData)
                {
                    try
                    {
                        var existingEntity = await FindByNaturalKeyAsync(item, cancellationToken);

                        if (existingEntity != null)
                        {
                            if (ShouldUpdate(existingEntity, item))
                            {
                                UpdateEntity(existingEntity, item);
                                result.Updated++;
                            }
                            else
                            {
                                result.Skipped++;
                            }
                        }
                        else
                        {
                            Context.Set<TEntity>().Add(item);
                            result.Inserted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        result.Errors.Add($"Error processing {GetNaturalKeyValue(item)}: {ex.Message}");
                    }
                }

                await Context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Step execution error: {ex.Message}");
                Logger.LogError(ex, "Error in seed step {Step}", StepName);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }

        protected abstract IEnumerable<TEntity> GetSeedData();
        protected abstract Task<TEntity?> FindByNaturalKeyAsync(TEntity item, CancellationToken cancellationToken);
        protected abstract string GetNaturalKeyValue(TEntity item);
        protected abstract bool ShouldUpdate(TEntity existing, TEntity incoming);
        protected abstract void UpdateEntity(TEntity existing, TEntity incoming);
        
        protected virtual Task OnBeforeExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken cancellationToken = default)
        {
            var result = new PreviewStepResult
            {
                StepName = StepName,
                DomainName = DomainName,
                PreviewTime = DateTime.UtcNow
            };

            try
            {
                var seedData = GetSeedData().ToList();
                result.TotalInSeedData = seedData.Count;

                foreach (var item in seedData)
                {
                    try
                    {
                        var existingEntity = await FindByNaturalKeyAsync(item, cancellationToken);

                        if (existingEntity != null)
                        {
                            if (ShouldUpdate(existingEntity, item))
                            {
                                result.WouldUpdate++;
                            }
                            else
                            {
                                result.WouldSkip++;
                            }
                        }
                        else
                        {
                            result.WouldCreate++;
                        }
                    }
                    catch
                    {
                        result.WouldSkip++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in preview step {Step}", StepName);
            }

            return result;
        }
    }

    public static class SeedStepFactory
    {
        public static Func<AppDbContext, ILogger, ISeedStep> Create<TEntity, TStep>()
            where TEntity : class
            where TStep : ISeedStep
        {
            return (ctx, log) => (ISeedStep)Activator.CreateInstance(typeof(TStep), ctx, log)!;
        }
    }
}
