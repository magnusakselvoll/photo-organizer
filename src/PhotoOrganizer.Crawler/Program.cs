using System.CommandLine;
using PhotoOrganizer.Crawler.Commands;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("crawler-.log", rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var rootCommand = new RootCommand("PhotoOrganizer Crawler — discovers and processes photo libraries");

    // --- init command ---
    var initPathArg = new Argument<string>("path") { Description = "Path to the folder to initialize" };
    var initLabelOpt = new Option<string>("--label") { Description = "Human-readable label for the folder", Required = true };
    var initTypeOpt = new Option<string>("--type") { Description = "Folder type: originals, edits, or mixed", DefaultValueFactory = _ => "mixed" };
    var initEnabledOpt = new Option<bool>("--enabled") { Description = "Whether to include folder in indexing", DefaultValueFactory = _ => true };
    var initNoAddOpt = new Option<bool>("--no-add-to-config") { Description = "Skip adding the folder to the config file's ScanRoots" };
    var initConfigOpt = new Option<string?>("--config") { Description = "Path to crawler-config.json" };

    var initCommand = new Command("init", "Initialize a folder as a photo source and run a full crawl");
    initCommand.Add(initPathArg);
    initCommand.Add(initLabelOpt);
    initCommand.Add(initTypeOpt);
    initCommand.Add(initEnabledOpt);
    initCommand.Add(initNoAddOpt);
    initCommand.Add(initConfigOpt);
    initCommand.SetAction(async (parseResult, ct) =>
    {
        return await InitCommand.RunAsync(
            parseResult.GetValue(initPathArg)!,
            parseResult.GetValue(initLabelOpt)!,
            parseResult.GetValue(initTypeOpt)!,
            parseResult.GetValue(initEnabledOpt),
            addToConfig: !parseResult.GetValue(initNoAddOpt),
            parseResult.GetValue(initConfigOpt));
    });

    // --- run command ---
    var runModeOpt = new Option<string>("--mode") { Description = "Crawl mode: full, incremental, or targeted", DefaultValueFactory = _ => "incremental" };
    var runStepOpt = new Option<string?>("--step") { Description = "Step name to run (required when mode is targeted)" };
    var runConfigOpt = new Option<string?>("--config") { Description = "Path to crawler-config.json" };

    var runCommand = new Command("run", "Run the crawler over all configured scan roots");
    runCommand.Add(runModeOpt);
    runCommand.Add(runStepOpt);
    runCommand.Add(runConfigOpt);
    runCommand.SetAction(async (parseResult, ct) =>
    {
        return await RunCommand.RunAsync(
            parseResult.GetValue(runModeOpt)!,
            parseResult.GetValue(runStepOpt),
            parseResult.GetValue(runConfigOpt));
    });

    rootCommand.Add(initCommand);
    rootCommand.Add(runCommand);

    return await rootCommand.Parse(args).InvokeAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
