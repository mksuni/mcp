// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure.Mcp.Core.Models.Command;
using Fabric.Mcp.Tools.PublicApi.Commands.BestPractices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fabric.Mcp.Tools.PublicApi.Tests.Commands;

public class UserDataFunctionsCodeGenGetCommandTests
{
    [Fact]
    public void UserDataFunctionsCodeGenGetCommand_HasCorrectProperties()
    {
        var logger = LoggerFactory.Create(builder => { }).CreateLogger<UserDataFunctionsCodeGenGetCommand>();
        var command = new UserDataFunctionsCodeGenGetCommand(logger);

        Assert.Equal("get", command.Name);
        Assert.NotEmpty(command.Description);
        Assert.Equal("Get User Data Functions CodeGen Instructions", command.Title);
        Assert.True(command.Metadata.ReadOnly);
        Assert.False(command.Metadata.Destructive);
    }

    [Fact]
    public void UserDataFunctionsCodeGenGetCommand_GetCommand_ReturnsValidCommand()
    {
        var logger = LoggerFactory.Create(builder => { }).CreateLogger<UserDataFunctionsCodeGenGetCommand>();
        var command = new UserDataFunctionsCodeGenGetCommand(logger);

        var systemCommand = command.GetCommand();

        Assert.NotNull(systemCommand);
        Assert.Equal("get", systemCommand.Name);
    }

    [Fact]
    public async Task UserDataFunctionsCodeGenGetCommand_ExecuteAsync_ReturnsInstructions()
    {
        var logger = LoggerFactory.Create(builder => { }).CreateLogger<UserDataFunctionsCodeGenGetCommand>();
        var command = new UserDataFunctionsCodeGenGetCommand(logger);

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var context = new CommandContext(serviceProvider);
        var parseResult = command.GetCommand().Parse(Array.Empty<string>());

        var response = await command.ExecuteAsync(context, parseResult, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull(response.Results);
        var resultList = response.Results!.Value as IEnumerable<string>;
        Assert.NotNull(resultList);
        Assert.Contains(resultList!, s => s.Contains("Primary objective: Generate correct Microsoft Fabric User Data Functions"));
    }
}