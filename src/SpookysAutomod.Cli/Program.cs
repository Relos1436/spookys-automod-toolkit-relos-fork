using System.CommandLine;
using SpookysAutomod.Cli.Commands;

// Root command
var rootCommand = new RootCommand("Spooky's AutoMod Toolkit - Skyrim mod creation and Fallout 4 plugin operations");

// Add global options
var jsonOption = new Option<bool>(
    aliases: new[] { "--json", "-j" },
    description: "Output results as JSON for machine parsing");
rootCommand.AddGlobalOption(jsonOption);

var verboseOption = new Option<bool>(
    aliases: new[] { "--verbose", "-v" },
    description: "Enable verbose output");
rootCommand.AddGlobalOption(verboseOption);

// Add subcommands with shared options
rootCommand.AddCommand(EspCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(Fallout4Commands.Create(jsonOption));
rootCommand.AddCommand(PapyrusCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(NifCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(ArchiveCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(McmCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(AudioCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(SkseCommands.Create(jsonOption, verboseOption));
rootCommand.AddCommand(F4seCommands.Create(jsonOption));

// Run
return await rootCommand.InvokeAsync(args);
