// ADR-025 D2 — IPostingService<TSourceDoc> contract tests.
//
// Sprint 12.9 PR #2 — Locks the contract that every posting service
// implements. These tests don't exercise the inner posting logic (that
// stays covered by service-specific tests like ApPostingServiceTests);
// they verify the SHAPE of the contract is in place so future Sprint 13
// (Purchasing CC) and Sprint 14 (Maintenance CC) services can't drift.
//
// Failure modes these tests catch:
//   - Someone removes the IPostingService<T> interface declaration
//     from a posting service class signature
//   - Someone reshapes PostingReceipt without updating callers
//   - Someone changes PostAsync's signature

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.AccountsPayable;
using Abs.FixedAssets.Services.Posting;
using Abs.FixedAssets.Services.Receiving;
using Xunit;

namespace Abs.FixedAssets.Tests;

public class PostingContractTests
{
    [Fact]
    public void ApPostingService_implements_IPostingService_of_ApInvoiceApprovalRequest()
    {
        var interfaces = typeof(ApPostingService).GetInterfaces();
        Assert.Contains(interfaces, i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(IPostingService<>) &&
            i.GetGenericArguments()[0] == typeof(ApInvoiceApprovalRequest));
    }

    [Fact]
    public void ReceivingPostingService_implements_IPostingService_of_ReceiveGoodsRequest()
    {
        var interfaces = typeof(ReceivingPostingService).GetInterfaces();
        Assert.Contains(interfaces, i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(IPostingService<>) &&
            i.GetGenericArguments()[0] == typeof(ReceiveGoodsRequest));
    }

    [Fact]
    public void IPostingService_has_exactly_one_PostAsync_with_canonical_signature()
    {
        var iface = typeof(IPostingService<>);
        var methods = iface.GetMethods();
        Assert.Single(methods);

        var postAsync = methods[0];
        Assert.Equal("PostAsync", postAsync.Name);

        // Return type: Task<Result<PostingReceipt>>
        var ret = postAsync.ReturnType;
        Assert.True(ret.IsGenericType, "PostAsync should return a generic type");
        Assert.Equal(typeof(Task<>), ret.GetGenericTypeDefinition());
        var taskInner = ret.GetGenericArguments()[0];
        Assert.True(taskInner.IsGenericType, "Task's inner should be generic (Result<T>)");
        Assert.Equal(typeof(Result<>), taskInner.GetGenericTypeDefinition());
        Assert.Equal(typeof(PostingReceipt), taskInner.GetGenericArguments()[0]);

        // Parameters: TSourceDoc, int actorUserId, Guid idempotencyKey, CancellationToken ct
        var parameters = postAsync.GetParameters();
        Assert.Equal(4, parameters.Length);
        // p0 is the generic TSourceDoc — its Type is the generic-parameter
        // placeholder, not a closed type.
        Assert.True(parameters[0].ParameterType.IsGenericParameter);
        Assert.Equal("source", parameters[0].Name);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.Equal("actorUserId", parameters[1].Name);
        Assert.Equal(typeof(Guid), parameters[2].ParameterType);
        Assert.Equal("idempotencyKey", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[3].ParameterType);
        Assert.Equal("ct", parameters[3].Name);
    }

    [Fact]
    public void PostingReceipt_has_expected_positional_record_fields()
    {
        // Record positional params (in order):
        //   int? JournalEntryId
        //   int LinesPosted
        //   decimal TotalDebits
        //   decimal TotalCredits
        //   bool WasReplay
        //   string? AuditEventId
        var primary = typeof(PostingReceipt).GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var parameters = primary.GetParameters();

        Assert.Equal(6, parameters.Length);
        Assert.Equal(typeof(int?), parameters[0].ParameterType);
        Assert.Equal("JournalEntryId", parameters[0].Name);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.Equal("LinesPosted", parameters[1].Name);
        Assert.Equal(typeof(decimal), parameters[2].ParameterType);
        Assert.Equal("TotalDebits", parameters[2].Name);
        Assert.Equal(typeof(decimal), parameters[3].ParameterType);
        Assert.Equal("TotalCredits", parameters[3].Name);
        Assert.Equal(typeof(bool), parameters[4].ParameterType);
        Assert.Equal("WasReplay", parameters[4].Name);
        Assert.Equal(typeof(string), parameters[5].ParameterType);
        Assert.Equal("AuditEventId", parameters[5].Name);
    }

    [Fact]
    public void PostingReceipt_construction_round_trips()
    {
        var receipt = new PostingReceipt(
            JournalEntryId: 42,
            LinesPosted: 3,
            TotalDebits: 100.50m,
            TotalCredits: 100.50m,
            WasReplay: false,
            AuditEventId: "audit-abc-123");

        Assert.Equal(42, receipt.JournalEntryId);
        Assert.Equal(3, receipt.LinesPosted);
        Assert.Equal(100.50m, receipt.TotalDebits);
        Assert.Equal(100.50m, receipt.TotalCredits);
        Assert.False(receipt.WasReplay);
        Assert.Equal("audit-abc-123", receipt.AuditEventId);
    }

    [Fact]
    public void ApInvoiceApprovalRequest_required_field_is_InvoiceId()
    {
        var request = new ApInvoiceApprovalRequest(InvoiceId: 42);
        Assert.Equal(42, request.InvoiceId);
        Assert.False(request.OverrideMatch);
        Assert.Equal(string.Empty, request.ApproverUsername);

        var withOverride = new ApInvoiceApprovalRequest(
            InvoiceId: 99,
            OverrideMatch: true,
            ApproverUsername: "alice@example.com");
        Assert.Equal(99, withOverride.InvoiceId);
        Assert.True(withOverride.OverrideMatch);
        Assert.Equal("alice@example.com", withOverride.ApproverUsername);
    }

    [Fact]
    public void ReceiveGoodsRequest_round_trips_GoodsReceiptId()
    {
        var request = new ReceiveGoodsRequest(GoodsReceiptId: 1234);
        Assert.Equal(1234, request.GoodsReceiptId);
    }
}
