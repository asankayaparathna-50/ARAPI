using Dapper;
using OpenAPI.Domain.Entities;
using OpenAPI.Domain.Entities.Statistics;
using OpenAPI.Domain.Interfaces;
using System.Data;
using System.Text;

namespace OpenAPI.Infrastructure.Repositories
{
    public class StatisticsRepository : IStatisticsRepository
    {
        private readonly ISqlQueryRepository _sqlRepo;

        public StatisticsRepository(ISqlQueryRepository sqlRepo)
        {
            _sqlRepo = sqlRepo ?? throw new ArgumentNullException(nameof(sqlRepo));
        }

        public async Task<PriceIndices> GetPriceIndices(string type, string? period, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPriceIndices_ByPeriod";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var parameters = new { CurrentPeriod = (string.IsNullOrWhiteSpace(period) ? null : period), Type = type };
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var rawData = (await conn.QueryAsync<PriceIndexRawData>(cmd)).ToList();

            var result = new PriceIndices
            {
                priceIndices = TransformPriceIndexData(rawData)
            };

            return result;
        }

        private static List<Dictionary<string, object>> TransformPriceIndexData(List<PriceIndexRawData> rawData)
        {
            var result = new List<Dictionary<string, object>>();

            foreach (var item in rawData)
            {
                var dict = new Dictionary<string, object>();

                // Map ItemName to friendly key
                dict["key"] = MapItemNameToFriendlyName(item.ItemName);
                dict["periodId"] = item.PeriodID ?? string.Empty;
                dict["value"] = item.CurrentValue ?? 0;

                result.Add(dict);
            }

            return result;
        }

        private static string MapItemNameToFriendlyName(string itemName)
        {
            // Map database item names to display-friendly names
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // NCPI mappings
                { "NCPI(2021=100)(Headline)", "National Consumer Price Index (NCPI) - Headline" },
                { "NCPI(2021=100)(Headline)(Monthly %)", "Monthly Change %" },
                { "NCPI(2021=100)(Headline)(Annual Average %)", "Annual Average Change %" },
                { "NCPI(2021=100)(Headline)(Year-on-Year %)", "Year-on-Year Change %" },
                { "NCPI(2021=100)(CORE)", "National Consumer Price Index (NCPI) - Core" },
                { "NCPI(2021=100)(CORE)(Annual Average %)", "Annual Average Change % (Core)" },
                { "NCPI(2021=100)(CORE)(Year-on-Year %)", "Year-on-Year Change % (Core)" },
                
                // CCPI mappings
                { "CCPI(2021=100)(Headline)", "Colombo Consumer Price Index (CCPI) - Headline" },
                { "CCPI(2021=100)(Headline)(Monthly %)", "Monthly Change %" },
                { "CCPI(2021=100)(Headline)(Annual Average %)", "Annual Average Change %" },
                { "CCPI(2021=100)(Headline)(Year-on-Year %)", "Year-on-Year Change %" },
                { "CCPI(2021=100)(CORE)", "Colombo Consumer Price Index (CCPI) - Core" },
                { "CCPI(2021=100)(CORE)(Annual Average %)", "Annual Average Change % (Core)" },
                { "CCPI(2021=100)(CORE)(Year-on-Year %)", "Year-on-Year Change % (Core)" }
            };

            return mappings.TryGetValue(itemName, out var friendlyName) ? friendlyName : itemName;
        }

        public async Task<MarketPrice> GetPrices(string? period = null, string? market = null, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPrices_ByPeriod";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var parameters = new { CurrentPeriod = (string.IsNullOrWhiteSpace(period) ? null : period), Market = market };
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var rawData = (await conn.QueryAsync<MarketPriceRawData>(cmd)).ToList();

            var result = new MarketPrice
            {
                prices = TransformMarketPriceData(rawData)
            };

            return result;
        }

        private static List<Dictionary<string, object>> TransformMarketPriceData(List<MarketPriceRawData> rawData)
        {
            var result = new List<Dictionary<string, object>>();

            foreach (var item in rawData)
            {
                var dict = new Dictionary<string, object>();

                // Extract the last segment as the item name
                dict["key"] = ExtractItemFromFullName(item.ItemName);
                dict["periodId"] = item.PeriodID ?? string.Empty;
                dict["value"] = item.CurrentValue ?? 0;

                result.Add(dict);
            }

            return result;
        }

        private static string ExtractItemFromFullName(string? itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return string.Empty;

            // Split by dash and take the last part
            var parts = itemName.Split('-', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[^2].Trim() + "-"+ parts[^1].Trim() : itemName;
        }

        public async Task<List<GdpItem>> GetGdpGrowth(string frequency, string? period, string? year, string? quarter, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetGdpGrowth_ByPeriod";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, YearCode = year, QuarterCode = quarter };
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var result = (await conn.QueryAsync<GdpItem>(cmd)).ToList();
            return result;
        }

        public async Task<List<ModalItem>> GetAgriculturalProduction(string period, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetAgriculturalProduction_ByPeriod";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var parameters = new { CurrentPeriod = period, FrequencyCode = "M" };
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );
            var items = (await conn.QueryAsync<ModalItem>(cmd)).ToList();
            return items;
        }

        public async Task<List<ModalItem>> GetIndustrialProduction(string period, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetIndustrialProduction_ByPeriod";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var parameters = new { CurrentPeriod = period, FrequencyCode = "M" };
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var items = (await conn.QueryAsync<ModalItem>(cmd)).ToList();
            return items;
        }

        public async Task<List<ModalItem>> GetPMI(string period, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPMI_ByPeriod";

            var parameters = new { CurrentPeriod = period, FrequencyCode = "M" };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        private async Task<List<ModalItem>> GetModal(string spName, object ? parameters, CancellationToken cancellationToken = default)
        {
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );
            var items = (await conn.QueryAsync<ModalItem>(cmd)).ToList();
            return items;
        }

        public async Task<List<ModalItem>> GetEmployment(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetEmployment_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        public async Task<List<ModalItem>> GetWageRateIndices(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetWageRateIndices_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        public async Task<List<ModalItem>> GetCrudeOilPrices(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetCrudeOilPrices_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        public async Task<List<ModalItem>> GetDailyElectricityGeneration(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetDailyElectricityGeneration";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        public async Task<PolicyInterestRateDetails> GetPolicyInterestRate(string period, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPolicyInterestRate_ByPeriod";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var opr_parameters = new { CurrentPeriod = period };
            var opr_cmd = new CommandDefinition(
                spName,
                parameters: opr_parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );
            var result = await conn.QueryAsync<PolicyInterestRateDetails>(opr_cmd);

            return result.FirstOrDefault() ?? new PolicyInterestRateDetails();
        }

        //2.1
        public async Task<List<ModalItem>> GetInterestRates(string period, string frequency, string type, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPolicyInterestRate_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, TypeCode = type };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }
       
        //2.2
        public async Task<List<ModalItem>> GetMoneySupply(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetMoneySupply_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //2.3
        public async Task<List<ModalItem>> GetReserveMoney(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetReserveMoney_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //2.4
        public async Task<List<ModalItem>> GetMoneyMarketActivity(string period, string frequency, string type, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetMoneyMarketActivity_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, TypeCode = type };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //2.5
        public async Task<List<ModalItem>> GetCbslSecuritiesPortfolio(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetCbslSecuritiesPortfolio_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //2.6

        //2.71
        public async Task<List<ModalItem>> GetCreditCards(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetCreditCards_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //2.72
        public async Task<List<ModalItem>> GetCommercialPaperIssue(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetCommercialPaperIssue_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //2.8
        public async Task<List<ModalItem>> GetShareMarket(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetShareMarket_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }
        
        //3.1
        public async Task<List<ModalItem>> GetGovernmentFinance(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetGovernmentFinance_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }
        
        //3.2
        public async Task<List<ModalItem>> GetOutstandingCentralGovernmentDebt(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetOutstandingCentralGovernmentDebt_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //3.3.1
        public async Task<List<ModalItem>> GetGovernmentSecurities(string period, string frequency, string type, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetGovernmentSecurities_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, TypeCode = type };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //3.3.2
        public async Task<List<ModalItem>> GetInternationalSovereignBonds(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetInternationalSovereignBonds_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //3.4
        public async Task<List<ModalItem>> GetPrimarySecondaryMarketTransactions(string period, string frequency, string type, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPrimarySecondaryMarketTransactions_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, TypeCode = type };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //3.5

        //3.6

        //3.7

        //4.1
        public async Task<List<ModalItem>> GetExchangeRates(string period, string frequency, string type, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetExchangeRates_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, TypeCode = type };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //4.2
        public async Task<List<ModalItem>> GetTourismAndWorkersRemittance(string period, string frequency, string type, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetTourismAndWorkersRemittance_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, TypeCode = type };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //4.3

        //4.4

        //4.5
        public async Task<List<ModalItem>> GetExternalTrade(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetExternalTrade_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }


        //4.6
        public async Task<List<ModalItem>> GetTradeIndices(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetTradeIndices_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //4.7
        public async Task<List<ModalItem>> GetCommodityPrice(string period, string frequency, string type, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetCommodityPrice_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency, TypeCode = type };
            var result = await GetModal(spName, parameters, cancellationToken); 
            return result;
        }

        //Daily EI -d.1
        public async Task<List<ModalItem>> GetRealGdpGrowth_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetRealGdpGrowth_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.2
        public async Task<List<ModalItem>> GetPricesAndIndices_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPricesAndIndices_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.3
        public async Task<List<ModalItem>> GetTTRate_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetTTRate_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.4
        public async Task<List<ModalItem>> GetMonySupply_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetMonySupply_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.5
        public async Task<List<ModalItem>> GetOpenMarketOperations_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetOpenMarketOperations_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.6
        public async Task<List<ModalItem>> GetPolicyRates_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPolicyRates_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.7
        public async Task<List<ModalItem>> GetAwpr(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetAwpr_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.8
        public async Task<List<ModalItem>> GetOvernightLiquidity_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetOvernightLiquidity_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.9
        public async Task<List<ModalItem>> GetTreasuryBillYield_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetTreasuryBillYield_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.10
        public async Task<List<ModalItem>> GetAllSharePriceIndex_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetAllSharePriceIndex_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.11
        public async Task<List<ModalItem>> GetPetroleum_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetPetroleum_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

        //d.12
        public async Task<List<ModalItem>> GetElectricity_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetElectricity_ByPeriod";
            var parameters = new { CurrentPeriod = period, FrequencyCode = frequency };
            var result = await GetModal(spName, parameters, cancellationToken);
            return result;
        }

    }
}
