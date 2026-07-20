using System.Collections;
using System.Reflection;
using MachineMonitoring.Application.Production.Notifications;

namespace MachineMonitoring.Tests.Production;

public sealed class ProductionNotificationContractTests
{
    [Fact]
    public void ProductionNotificationContracts_DoNotExposeDomainEfDbContextOrInfrastructureTypes()
    {
        Type[] notificationTypes =
        [
            typeof(OperationStatusChangedNotification),
            typeof(OperationProgressChangedNotification),
            typeof(OperationEventAppendedNotification),
            typeof(MachineAlarmRaisedNotification),
            typeof(MachineAlarmAcknowledgedNotification),
            typeof(MachineAlarmResolvedNotification),
            typeof(MachineRuntimeStatusChangedNotification),
            typeof(WorkpieceStatusChangedNotification),
            typeof(ProductionLotStatusChangedNotification),
        ];

        foreach (Type notificationType in notificationTypes)
        {
            PropertyInfo[] properties = notificationType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public
            );

            Assert.DoesNotContain(
                properties,
                property => ContainsForbiddenContractType(property.PropertyType)
            );
        }
    }

    private static bool ContainsForbiddenContractType(Type propertyType)
    {
        Type candidate = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (IsAllowedLeafType(candidate))
        {
            return false;
        }

        if (candidate.IsArray)
        {
            return ContainsForbiddenContractType(candidate.GetElementType()!);
        }

        Type? enumerableArgument = TryGetEnumerableArgument(candidate);
        if (enumerableArgument is not null)
        {
            return ContainsForbiddenContractType(enumerableArgument);
        }

        return true;
    }

    private static bool IsAllowedLeafType(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTimeOffset))
        {
            return true;
        }

        return type.IsEnum;
    }

    private static Type? TryGetEnumerableArgument(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsGenericType)
        {
            Type genericDefinition = type.GetGenericTypeDefinition();
            if (
                genericDefinition == typeof(IEnumerable<>)
                || genericDefinition == typeof(IReadOnlyCollection<>)
                || genericDefinition == typeof(IReadOnlyList<>)
                || genericDefinition == typeof(ICollection<>)
                || genericDefinition == typeof(IList<>)
                || genericDefinition == typeof(List<>)
            )
            {
                return type.GetGenericArguments()[0];
            }
        }

        Type? enumerableInterface = type.GetInterfaces().FirstOrDefault(interfaceType =>
            interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        );

        return enumerableInterface?.GetGenericArguments()[0];
    }
}
