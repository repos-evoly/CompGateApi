using System.Data;
using System.Data.Common;
using System.Reflection;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Notifications;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CompGateApi.Tests;

public sealed class LegacyNotificationQueryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationResolvesRecipientWithEitherSchemaWithoutBankOrNetworkAccess(bool upgraded)
    {
        var commands = new BeneficiaryReader(upgraded);
        await using var db = new CompGateApiDbContext(new DbContextOptionsBuilder<CompGateApiDbContext>()
            .UseSqlServer("Server=localhost;Database=NeverConnected;Integrated Security=true")
            .AddInterceptors(new NoConnection(), commands).Options);
        var bank = DispatchProxy.Create<ITransferRequestRepository, NoBankCalls>();
        var message = await NotificationMessageText.TransferDetailsAsync(new TransferRequest
        {
            CompanyId = 27, ToAccount = "123456", Amount = 1500.125m,
            Currency = new Currency { Code = "LYD" }
        }, db, bank, NullLogger.Instance, CancellationToken.None);
        Assert.Equal("1,500.125 LYD to Recipient", message);
        Assert.Equal(2, commands.Reads);
    }

    public class NoBankCalls : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            throw new Xunit.Sdk.XunitException("A known beneficiary must not require a bank API request.");
    }

    private sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class BeneficiaryReader(bool upgraded) : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Reads++;
            var table = new DataTable();
            if (command.CommandText.Contains("COL_LENGTH"))
            {
                table.Columns.Add("Value", typeof(int));
                table.Rows.Add(upgraded ? 1 : 0);
            }
            else
            {
                Assert.Contains("[CompanyId]", command.CommandText);
                Assert.Contains("[IsDeleted]", command.CommandText);
                Assert.Contains("[AccountNumber]", command.CommandText);
                Assert.DoesNotContain("[RowVersion]", command.CommandText);
                if (upgraded) Assert.Contains("[PaymentRail] = N'Normal'", command.CommandText);
                else Assert.DoesNotContain("[PaymentRail]", command.CommandText);
                table.Columns.Add("Name", typeof(string));
                table.Rows.Add("Recipient");
            }
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }
    }
}
