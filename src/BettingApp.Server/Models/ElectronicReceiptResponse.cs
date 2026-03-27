using BettingApp.Domain.Entities;

namespace BettingApp.Server.Models;

public sealed record ElectronicReceiptResponse(
    Guid Id,
    ElectronicReceiptType Type,
    string DocumentNumber,
    string Title,
    string Summary,
    decimal CreditAmount,
    decimal RealMoneyAmount,
    string RealCurrencyCode,
    decimal MoneyToCreditRate,
    decimal CreditToMoneyRate,
    string Reference,
    DateTime IssuedAtUtc);
