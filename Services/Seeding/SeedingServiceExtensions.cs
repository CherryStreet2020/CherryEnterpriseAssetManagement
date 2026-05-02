using Abs.FixedAssets.Services.Seeding.Pipelines;

namespace Abs.FixedAssets.Services.Seeding
{
    public static class SeedingServiceExtensions
    {
        public static IServiceCollection AddSeedingServices(this IServiceCollection services)
        {
            services.AddScoped<ISeedPipelineExecutor, SeedPipelineExecutor>();

            services.AddScoped<SystemReferenceSeedPipeline>();
            services.AddScoped<OrgAndFinanceSeedPipeline>();
            services.AddScoped<VendorsAndPartsFoundationSeedPipeline>();
            services.AddScoped<EamExecutionMastersSeedPipeline>();
            services.AddScoped<DemoScenarioSeedPipeline>();
            services.AddScoped<DemoPackV1Pipeline>();
            services.AddScoped<DemoPackV2Pipeline>();
            services.AddScoped<LookupSeedPipeline>();

            return services;
        }
    }
}
