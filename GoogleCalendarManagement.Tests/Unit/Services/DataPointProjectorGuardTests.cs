using System.Reflection;
using FluentAssertions;
using GoogleCalendarManagement.Services;
using Xunit;

namespace GoogleCalendarManagement.Tests.Unit.Services;

/// <summary>
/// CI tripwire: every concrete <see cref="IDataSourceImportHandler"/> must override
/// <see cref="IDataSourceImportHandler.GetProjector"/> and return a real
/// <see cref="IDataPointProjector"/>. This test is EXPECTED TO FAIL until Story 8.9 adds
/// projector overrides to all handlers; once green it prevents new handlers from shipping
/// without a projector.
/// </summary>
public sealed class DataPointProjectorGuardTests
{
    [Fact]
    public void AllConcreteHandlers_MustOverride_GetProjector()
    {
        var mainAssembly = typeof(IDataSourceImportHandler).Assembly;

        var handlerTypes = mainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition
                        && typeof(IDataSourceImportHandler).IsAssignableFrom(t))
            .ToList();

        handlerTypes.Should().NotBeEmpty("the assembly must contain concrete import handlers");

        var handlersWithoutOverride = handlerTypes
            .Where(t =>
            {
                var method = t.GetMethod(nameof(IDataSourceImportHandler.GetProjector),
                    BindingFlags.Instance | BindingFlags.Public);
                // If DeclaringType is the interface (or null), the handler uses the default null return.
                return method is null || method.DeclaringType != t;
            })
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        handlersWithoutOverride.Should().BeEmpty(
            "every import handler must override GetProjector() and return a real IDataPointProjector. " +
            $"Missing: {string.Join(", ", handlersWithoutOverride)}");
    }
}
