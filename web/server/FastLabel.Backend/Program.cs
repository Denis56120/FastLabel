var builder = WebApplication.CreateBuilder(args);

ProgramHelpers.AddEnvironmentConfiguration(builder);
ProgramHelpers.AddAppServices(builder);
await ProgramHelpers.ConfigurePipelineAsync(builder);

public partial class Program { }