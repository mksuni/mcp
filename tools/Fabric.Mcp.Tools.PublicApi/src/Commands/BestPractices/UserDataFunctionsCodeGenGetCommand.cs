// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure.Mcp.Core.Commands;
using Azure.Mcp.Core.Options;
using Fabric.Mcp.Tools.PublicApi.Services;
using Microsoft.Extensions.Logging;

namespace Fabric.Mcp.Tools.PublicApi.Commands.BestPractices;

/// <summary>
/// Returns the embedded, sample-first code generation instructions for Microsoft Fabric User Data Functions (UDFs).
/// The instructions file enforces constitutional rules for generating Python UDFs, requiring:
/// - Strict grounding in Microsoft Docs via the Microsoft Docs MCP server
/// - Sample-first adaptation workflow (fetch index, fetch closest sample, adapt)
/// - Mandatory parameter casing, validation, logging and response envelope rules
/// Use this command when you need authoritative guidance before generating or modifying Fabric User Data Function code.
/// </summary>
public sealed class UserDataFunctionsCodeGenGetCommand(ILogger<UserDataFunctionsCodeGenGetCommand> logger) : GlobalCommand<GlobalOptions>()
{
    private const string ResourcePath = "Resources/user-data-functions/fabric-functions-codegen.md";
    private readonly ILogger<UserDataFunctionsCodeGenGetCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public override string Id => "5d7b2f6c-3e9c-4d6a-a0c9-5bf9f7b4f6e2"; // New GUID for this command

    public override string Name => "get";

    public override string Title => "Get User Data Functions CodeGen Instructions";

    public override string Description =>
        "Retrieves the embedded constitutional code generation instructions for Microsoft Fabric User Data Functions (UDFs). " +
        "Use before generating Python UDF code to ensure sample-first, grounded, non-fabricated output.";

    public override ToolMetadata Metadata => new()
    {
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        ReadOnly = true,
        LocalRequired = false,
        Secret = false
    };

    protected override void RegisterOptions(Command command)
    {
        // No options required; single retrieval command.
        base.RegisterOptions(command);
    }

    public override Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!Validate(parseResult.CommandResult, context.Response).IsValid)
        {
            return Task.FromResult(context.Response);
        }

        try
        {
            _logger.LogInformation("Retrieving User Data Functions code generation instructions from {ResourcePath}", ResourcePath);
            var content = EmbeddedResourceProviderService.GetEmbeddedResource(ResourcePath);
            context.Response.Results = ResponseResult.Create([content], FabricJsonContext.Default.IEnumerableString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve User Data Functions code generation instructions");
            HandleException(context, ex);
        }
        return Task.FromResult(context.Response);
    }
}