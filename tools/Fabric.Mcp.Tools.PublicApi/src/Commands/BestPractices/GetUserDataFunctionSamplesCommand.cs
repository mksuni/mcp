// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Commands;
using Azure.Mcp.Core.Extensions;
using Fabric.Mcp.Tools.PublicApi.Options;
using Fabric.Mcp.Tools.PublicApi.Services;
using Microsoft.Extensions.Logging;

namespace Fabric.Mcp.Tools.PublicApi.Commands.BestPractices;

public sealed class GetUserDataFunctionSamplesCommand(ILogger<GetUserDataFunctionSamplesCommand> logger) : GlobalCommand<BaseFabricOptions>()
{
    private const string CommandTitle = "Get User Data Function Samples";

    private readonly ILogger<GetUserDataFunctionSamplesCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public override string Id => "8a2f5c9d-1e3b-4d7a-9f6e-2c8b5a4d7e1f";

    public override string Name => "get";

    public override string Description =>
        """
        Retrieve Python sample functions for Microsoft Fabric User Data Functions from the 
        official samples repository. Returns a markdown document with links to various sample 
        functions including examples for working with Warehouse, Lakehouse, SQL Database, 
        Variable Library, and data manipulation operations. These samples demonstrate how to 
        create serverless functions that can process data and connect to various Fabric data sources.
        """;

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new()
    {
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        ReadOnly = true,
        LocalRequired = false,
        Secret = false
    };

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var fabricService = context.GetService<IFabricPublicApiService>();
            var samples = await fabricService.GetUserDataFunctionSamplesAsync(cancellationToken);

            context.Response.Results = ResponseResult.Create(new UserDataFunctionSamplesResult(samples), FabricJsonContext.Default.UserDataFunctionSamplesResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user data function samples");
            HandleException(context, ex);
        }

        return context.Response;
    }

    public record UserDataFunctionSamplesResult(string Samples);
}
