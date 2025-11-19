// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Fabric.Mcp.Tools.PublicApi.Options.BestPractices;

public static class UserDataFunctionsOptionDefinitions
{
    public const string ResourceName = "resource";

    public static readonly Option<string> Resource = new($"--{ResourceName}")
    {
        Description = "The resource type: 'samples-list', 'warehouse', 'lakehouse', 'sqldb', 'variablelibrary', 'datamanipulation', or 'udfdatatypes'",
        Required = true
    };

    public const string ActionName = "action";

    public static readonly Option<string> Action = new($"--{ActionName}")
    {
        Description = "The action to perform: 'all', 'query', 'write', or 'specific'. Required for code resource types.",
        Required = false
    };

    public const string FileNameName = "filename";

    public static readonly Option<string> FileName = new($"--{FileNameName}")
    {
        Description = "Specific file path when action is 'specific' (e.g., 'Warehouse/query_data_from_warehouse.py')",
        Required = false
    };
}
