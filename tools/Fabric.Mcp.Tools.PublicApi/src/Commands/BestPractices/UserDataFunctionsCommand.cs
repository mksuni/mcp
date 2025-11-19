// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Azure.Mcp.Core.Commands;
using Azure.Mcp.Core.Extensions;
using Fabric.Mcp.Tools.PublicApi.Options;
using Fabric.Mcp.Tools.PublicApi.Options.BestPractices;
using Microsoft.Extensions.Logging;

namespace Fabric.Mcp.Tools.PublicApi.Commands.BestPractices;

public sealed class UserDataFunctionsCommand(ILogger<UserDataFunctionsCommand> logger, HttpClient httpClient) : BaseCommand<UserDataFunctionsOptions>
{
    private const string CommandTitle = "User Data Functions Samples and Code Generation";
    private readonly ILogger<UserDataFunctionsCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private static readonly Dictionary<string, string> s_contentCache = new();

    private const string SamplesListUrl = "https://raw.githubusercontent.com/microsoft/fabric-user-data-functions-samples/refs/heads/main/PYTHON/samples-llms.txt";
    private const string BaseCodeUrl = "https://raw.githubusercontent.com/microsoft/fabric-user-data-functions-samples/refs/heads/main/PYTHON/";

    public override string Id => "8a2f5c9d-1e3b-4d7a-9f6e-2c8b5a4d7e1f";

    public override string Name => "get";

    public override string Description =>
        """
        Retrieve Python sample functions and code for Microsoft Fabric User Data Functions.
        
        Use this tool when you need to:
        - Get a comprehensive list of available sample functions with descriptions and links
        - Retrieve actual Python code for specific sample functions
        - Generate code for working with Warehouse, Lakehouse, SQL Database, Variable Library, or data manipulation
        
        The 'resource' parameter specifies what type of samples to retrieve:
        - 'samples-list': Returns the complete markdown document with all available samples and their descriptions
        - 'warehouse': Returns sample code for Fabric warehouse operations
        - 'lakehouse': Returns sample code for Fabric lakehouse operations
        - 'sqldb': Returns sample code for SQL database operations
        - 'variablelibrary': Returns sample code for Variable Library operations
        - 'datamanipulation': Returns sample code for data manipulation with pandas/numpy
        - 'udfdatatypes': Returns sample code demonstrating UDF data types
        
        The 'action' parameter (optional for specific resources) specifies the operation:
        - 'all': Returns all sample code files for the specified resource
        - 'query': Returns query/read sample code
        - 'write': Returns write/export sample code
        - 'specific': Returns a specific sample file (requires 'filename' parameter)
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

    protected override void RegisterOptions(Command command)
    {
        command.Options.Add(UserDataFunctionsOptionDefinitions.Resource);
        command.Options.Add(UserDataFunctionsOptionDefinitions.Action);
        command.Options.Add(UserDataFunctionsOptionDefinitions.FileName);
        
        command.Validators.Add(commandResult =>
        {
            commandResult.TryGetValue(UserDataFunctionsOptionDefinitions.Resource, out string? resource);
            commandResult.TryGetValue(UserDataFunctionsOptionDefinitions.Action, out string? action);
            commandResult.TryGetValue(UserDataFunctionsOptionDefinitions.FileName, out string? fileName);

            if (string.IsNullOrWhiteSpace(resource))
            {
                commandResult.AddError("The resource parameter is required.");
            }
            else
            {
                bool validResource = resource is "samples-list" or "warehouse" or "lakehouse" or "sqldb" 
                    or "variablelibrary" or "datamanipulation" or "udfdatatypes";

                if (!validResource)
                {
                    commandResult.AddError("Invalid resource. Must be 'samples-list', 'warehouse', 'lakehouse', 'sqldb', 'variablelibrary', 'datamanipulation', or 'udfdatatypes'.");
                }

                if (resource != "samples-list" && string.IsNullOrWhiteSpace(action))
                {
                    commandResult.AddError("The action parameter is required for code resource types.");
                }

                if (!string.IsNullOrWhiteSpace(action))
                {
                    bool validAction = action is "all" or "query" or "write" or "specific";
                    if (!validAction)
                    {
                        commandResult.AddError("Invalid action. Must be 'all', 'query', 'write', or 'specific'.");
                    }

                    if (action == "specific" && string.IsNullOrWhiteSpace(fileName))
                    {
                        commandResult.AddError("The filename parameter is required when action is 'specific'.");
                    }
                }
            }
        });
    }

    protected override UserDataFunctionsOptions BindOptions(ParseResult parseResult)
    {
        return new UserDataFunctionsOptions
        {
            Resource = parseResult.GetValueOrDefault<string>(UserDataFunctionsOptionDefinitions.Resource.Name),
            Action = parseResult.GetValueOrDefault<string>(UserDataFunctionsOptionDefinitions.Action.Name),
            FileName = parseResult.GetValueOrDefault<string>(UserDataFunctionsOptionDefinitions.FileName.Name)
        };
    }

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!Validate(parseResult.CommandResult, context.Response).IsValid)
        {
            return context.Response;
        }

        var options = BindOptions(parseResult);

        try
        {
            if (string.IsNullOrEmpty(options.Resource))
            {
                context.Response.Status = HttpStatusCode.BadRequest;
                context.Response.Message = "The resource parameter is required.";
                return context.Response;
            }

            string content;
            if (options.Resource == "samples-list")
            {
                content = await GetSamplesListAsync(cancellationToken);
            }
            else
            {
                content = await GetSampleCodeAsync(options.Resource, options.Action, options.FileName, cancellationToken);
            }

            context.Response.Status = HttpStatusCode.OK;
            context.Response.Results = ResponseResult.Create([content], FabricJsonContext.Default.ListString);
            context.Response.Message = string.Empty;

            context.Activity?.AddTag("UserDataFunctions_Resource", options.Resource);
            context.Activity?.AddTag("UserDataFunctions_Action", options.Action);
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error getting user data function content for Resource: {Resource}, Action: {Action}",
                options.Resource, options.Action);
            context.Response.Status = httpEx.StatusCode ?? HttpStatusCode.InternalServerError;
            context.Response.Message = $"Failed to fetch content from GitHub: {httpEx.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user data function content for Resource: {Resource}, Action: {Action}",
                options.Resource, options.Action);
            HandleException(context, ex);
        }

        return context.Response;
    }

    private async Task<string> GetSamplesListAsync(CancellationToken cancellationToken)
    {
        if (s_contentCache.TryGetValue("samples-list", out string? cached))
        {
            return cached;
        }

        _logger.LogInformation("Fetching user data functions samples list from GitHub");
        var content = await FetchFromGitHubAsync(SamplesListUrl, cancellationToken);
        s_contentCache["samples-list"] = content;
        return content;
    }

    private async Task<string> GetSampleCodeAsync(string resource, string? action, string? fileName, CancellationToken cancellationToken)
    {
        var files = GetFilePathsForResource(resource, action, fileName);
        
        if (files.Length == 0)
        {
            throw new ArgumentException($"No files found for resource '{resource}' and action '{action}'");
        }

        var combinedContent = new StringBuilder();

        foreach (var file in files)
        {
            var cacheKey = $"{resource}:{file}";
            
            string content;
            if (s_contentCache.TryGetValue(cacheKey, out string? cached))
            {
                content = cached;
            }
            else
            {
                _logger.LogInformation("Fetching user data function code: {File}", file);
                var url = BaseCodeUrl + file;
                content = await FetchFromGitHubAsync(url, cancellationToken);
                s_contentCache[cacheKey] = content;
            }

            if (combinedContent.Length > 0)
            {
                combinedContent.Append("\n\n# ========================================\n\n");
            }
            combinedContent.AppendLine($"# File: {file}");
            combinedContent.AppendLine();
            combinedContent.Append(content);
        }

        return combinedContent.ToString();
    }

    private static string[] GetFilePathsForResource(string resource, string? action, string? fileName)
    {
        if (action == "specific" && !string.IsNullOrEmpty(fileName))
        {
            return [fileName];
        }

        return (resource, action) switch
        {
            ("warehouse", "all") => [
                "Warehouse/export_warehouse_data_to_lakehouse.py",
                "Warehouse/query_data_from_warehouse.py"
            ],
            ("warehouse", "query") => ["Warehouse/query_data_from_warehouse.py"],
            ("warehouse", "write") => ["Warehouse/export_warehouse_data_to_lakehouse.py"],
            
            ("lakehouse", "all") => [
                "Lakehouse/write_csv_file_in_lakehouse.py",
                "Lakehouse/read_csv_file_from_lakehouse.py",
                "Lakehouse/query_data_from_tables.py"
            ],
            ("lakehouse", "query") => [
                "Lakehouse/read_csv_file_from_lakehouse.py",
                "Lakehouse/query_data_from_tables.py"
            ],
            ("lakehouse", "write") => ["Lakehouse/write_csv_file_in_lakehouse.py"],
            
            ("sqldb", "all") => [
                "SQLDB/write_many_rows_to_sql_db.py",
                "SQLDB/write_one_row_to_sql_db.py",
                "SQLDB/read_from_sql_db.py"
            ],
            ("sqldb", "query") => ["SQLDB/read_from_sql_db.py"],
            ("sqldb", "write") => [
                "SQLDB/write_many_rows_to_sql_db.py",
                "SQLDB/write_one_row_to_sql_db.py"
            ],
            
            ("variablelibrary", "all") => [
                "VariableLibrary/get_variables_from_library.py",
                "VariableLibrary/chat_completion_with_azure_openai.py"
            ],
            
            ("datamanipulation", "all") => [
                "DataManipulation/manipulate_data_with_pandas.py",
                "DataManipulation/transform_data_with_numpy.py"
            ],
            
            ("udfdatatypes", "all") => [
                "UDFDataTypes/use_userdatafunctioncontext.py",
                "UDFDataTypes/raise_userthrownerror.py"
            ],
            
            _ => []
        };
    }

    private async Task<string> FetchFromGitHubAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Fabric-MCP-Server");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
