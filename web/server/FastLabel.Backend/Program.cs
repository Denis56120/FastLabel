var builder = WebApplication.CreateBuilder(args);

ProgramExtensions.AddEnvironmentConfiguration(builder);
ProgramExtensions.AddAppServices(builder);
await ProgramExtensions.ConfigurePipelineAsync(builder);

public partial class Program { }