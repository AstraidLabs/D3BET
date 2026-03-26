using System.Text.Json;
using BettingApp.Server.Configuration;
using BettingApp.Server.Models;
using Microsoft.Extensions.Options;

namespace BettingApp.Server.Services;

public sealed class D3CreditAdminSettingsStore(IOptions<D3CreditOptions> options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly D3CreditOptions defaults = options.Value;
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BettingApp",
        "server-d3credit-settings.json");

    public async Task<D3CreditAdminSettingsResponse> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return CreateDefault();
        }

        await using var stream = File.OpenRead(settingsPath);
        var persisted = await JsonSerializer.DeserializeAsync<D3CreditAdminSettingsResponse>(stream, SerializerOptions, cancellationToken);
        return Normalize(persisted ?? CreateDefault());
    }

    public async Task<D3CreditAdminSettingsResponse> SaveAsync(UpdateD3CreditAdminSettingsRequest request, CancellationToken cancellationToken)
    {
        var normalized = Normalize(new D3CreditAdminSettingsResponse(
            string.IsNullOrWhiteSpace(request.CreditCode) ? defaults.CreditCode : request.CreditCode.Trim(),
            string.IsNullOrWhiteSpace(request.BaseCurrencyCode) ? defaults.BaseCurrencyCode : request.BaseCurrencyCode.Trim().ToUpperInvariant(),
            request.BaseCreditsPerCurrencyUnit,
            request.BaseCurrencyUnitsPerCredit,
            request.LowParticipationThreshold,
            request.LowParticipationBoostPercent,
            request.HighParticipationThreshold,
            request.HighParticipationReductionPercent,
            request.TotalStakePressureDivisor,
            request.MaxPressureReductionPercent,
            request.OddsVolatilityWeightPercent,
            request.EnableTestTopUpGateway,
            request.EnableManualCreditAdjustments,
            request.EnableManualBetRefunds,
            request.DefaultTopUpAmount,
            request.MarketRules?.ToArray() ?? []));

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, normalized, SerializerOptions, cancellationToken);
        return normalized;
    }

    private D3CreditAdminSettingsResponse CreateDefault()
    {
        return new D3CreditAdminSettingsResponse(
            defaults.CreditCode,
            defaults.BaseCurrencyCode,
            defaults.BaseCreditsPerCurrencyUnit,
            defaults.BaseCurrencyUnitsPerCredit,
            defaults.LowParticipationThreshold,
            defaults.LowParticipationBoostPercent,
            defaults.HighParticipationThreshold,
            defaults.HighParticipationReductionPercent,
            defaults.TotalStakePressureDivisor,
            defaults.MaxPressureReductionPercent,
            defaults.OddsVolatilityWeightPercent,
            defaults.EnableTestTopUpGateway,
            EnableManualCreditAdjustments: true,
            EnableManualBetRefunds: true,
            DefaultTopUpAmount: 500m,
            MarketRules: []);
    }

    private static D3CreditAdminSettingsResponse Normalize(D3CreditAdminSettingsResponse source)
    {
        var moneyToCredit = source.BaseCreditsPerCurrencyUnit > 0m
            ? source.BaseCreditsPerCurrencyUnit
            : 10m;
        var creditToMoney = source.BaseCurrencyUnitsPerCredit > 0m
            ? source.BaseCurrencyUnitsPerCredit
            : Math.Round(1m / moneyToCredit, 4, MidpointRounding.AwayFromZero);

        return source with
        {
            CreditCode = string.IsNullOrWhiteSpace(source.CreditCode) ? "D3Kredit" : source.CreditCode.Trim(),
            BaseCurrencyCode = string.IsNullOrWhiteSpace(source.BaseCurrencyCode) ? "CZK" : source.BaseCurrencyCode.Trim().ToUpperInvariant(),
            BaseCreditsPerCurrencyUnit = moneyToCredit,
            BaseCurrencyUnitsPerCredit = creditToMoney,
            LowParticipationThreshold = Math.Max(0, source.LowParticipationThreshold),
            HighParticipationThreshold = Math.Max(0, source.HighParticipationThreshold),
            TotalStakePressureDivisor = source.TotalStakePressureDivisor <= 0m ? 500m : source.TotalStakePressureDivisor,
            MaxPressureReductionPercent = Math.Max(0m, source.MaxPressureReductionPercent),
            DefaultTopUpAmount = source.DefaultTopUpAmount <= 0m ? 500m : source.DefaultTopUpAmount,
            MarketRules = (source.MarketRules ?? [])
                .Where(rule => rule.MarketId != Guid.Empty)
                .GroupBy(rule => rule.MarketId)
                .Select(group =>
                {
                    var rule = group.Last();
                    return new D3CreditMarketAdminRuleResponse(
                        rule.MarketId,
                        rule.IsEnabled,
                        rule.AdditionalMultiplierPercent,
                        rule.OverrideMoneyToCreditRate is > 0m ? rule.OverrideMoneyToCreditRate : null,
                        rule.OverrideCreditToMoneyRate is > 0m ? rule.OverrideCreditToMoneyRate : null,
                        string.IsNullOrWhiteSpace(rule.Note) ? null : rule.Note.Trim());
                })
                .OrderBy(rule => rule.MarketId)
                .ToArray()
        };
    }
}
