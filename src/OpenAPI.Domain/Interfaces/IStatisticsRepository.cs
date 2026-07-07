using OpenAPI.Domain.Entities.Statistics;

namespace OpenAPI.Domain.Interfaces
{
    public interface IStatisticsRepository
    {
        Task<PriceIndices> GetPriceIndices(string type, string? period,  CancellationToken cancellationToken = default);
        Task<MarketPrice> GetPrices(string? period = null, string? market = null, CancellationToken cancellationToken = default);
        Task<List<GdpItem>> GetGdpGrowth(string frequency, string? period, string? year, string? quarter, CancellationToken cancellationToken = default);
        Task<List<ModalItem>> GetAgriculturalProduction(string period, CancellationToken cancellationToken = default);
        Task<List<ModalItem>> GetIndustrialProduction(string period, CancellationToken cancellationToken = default);
        Task<List<ModalItem>> GetPMI(string period, CancellationToken cancellationToken = default);
        Task<List<ModalItem>> GetEmployment(string period, string frequency, CancellationToken cancellationToken = default);
        Task<List<ModalItem>> GetWageRateIndices(string period, string frequency, CancellationToken cancellationToken = default);
        Task<List<ModalItem>> GetCrudeOilPrices(string period, string frequency, CancellationToken cancellationToken = default);
        Task<List<ModalItem>> GetDailyElectricityGeneration(string period, string frequency, CancellationToken cancellationToken = default);
        
 
        Task<PolicyInterestRateDetails> GetPolicyInterestRate(string period, CancellationToken cancellationToken = default);
        
        //2.1
        Task<List<ModalItem>> GetInterestRates(string period, string frequency, string type, CancellationToken cancellationToken = default);
        //2.2
        Task<List<ModalItem>> GetMoneySupply(string period, string frequency,CancellationToken cancellationToken = default);
        //2.3
        Task<List<ModalItem>> GetReserveMoney(string period, string frequency, CancellationToken cancellationToken = default);
        //2.4
        Task<List<ModalItem>> GetMoneyMarketActivity(string period, string frequency, string type, CancellationToken cancellationToken = default);
        //2.5
        Task<List<ModalItem>> GetCbslSecuritiesPortfolio(string period, string frequency, CancellationToken cancellationToken = default);
        //2.6

        //2.71
        Task<List<ModalItem>> GetCreditCards(string period, string frequency, CancellationToken cancellationToken = default);
        //2.72
        Task<List<ModalItem>> GetCommercialPaperIssue(string period, string frequency, CancellationToken cancellationToken = default);
        //2.8
        Task<List<ModalItem>> GetShareMarket(string period, string frequency, CancellationToken cancellationToken = default);
        //3.1
        Task<List<ModalItem>> GetGovernmentFinance(string period, string frequency, CancellationToken cancellationToken = default);
        //3.2
        Task<List<ModalItem>> GetOutstandingCentralGovernmentDebt(string period, string frequency, CancellationToken cancellationToken = default);
        //3.3.1
        Task<List<ModalItem>> GetGovernmentSecurities(string period, string frequency, string type, CancellationToken cancellationToken = default);
        //3.3.2
        Task<List<ModalItem>> GetInternationalSovereignBonds(string period, string frequency, CancellationToken cancellationToken = default);
        //3.4
        Task<List<ModalItem>> GetPrimarySecondaryMarketTransactions(string period, string frequency, string type, CancellationToken cancellationToken = default);
        //3.5

        //3.6

        //3.7

        //4.1
        Task<List<ModalItem>> GetExchangeRates(string period, string frequency, string type, CancellationToken cancellationToken = default);
        //4.2
        Task<List<ModalItem>> GetTourismAndWorkersRemittance(string period, string frequency, string type, CancellationToken cancellationToken = default);
        //4.3

        //4.4

        //4.5
        Task<List<ModalItem>> GetExternalTrade(string period, string frequency, CancellationToken cancellationToken = default);
        //4.6
        Task<List<ModalItem>> GetTradeIndices(string period, string frequency, CancellationToken cancellationToken = default);
        //4.7
        Task<List<ModalItem>> GetCommodityPrice(string period, string frequency, string type, CancellationToken cancellationToken = default);


        //Daily EI
        //d.1
        Task<List<ModalItem>> GetRealGdpGrowth_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default); 
        //d.2
        Task<List<ModalItem>> GetPricesAndIndices_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);     
        //d.3
        Task<List<ModalItem>> GetTTRate_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);   
        //d.4
        Task<List<ModalItem>> GetMonySupply_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);  
        //d.5
        Task<List<ModalItem>> GetOpenMarketOperations_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);  
        //d.6
        Task<List<ModalItem>> GetPolicyRates_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);     
        //d.7
        Task<List<ModalItem>> GetAwpr(string period, string frequency, CancellationToken cancellationToken = default);   
        //d.8
        Task<List<ModalItem>> GetOvernightLiquidity_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);   
        //d.9
        Task<List<ModalItem>> GetTreasuryBillYield_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);    
        //d.10
        Task<List<ModalItem>> GetAllSharePriceIndex_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);
        //d.11
        Task<List<ModalItem>> GetPetroleum_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default); 
        //d.12
        Task<List<ModalItem>> GetElectricity_ByPeriod(string period, string frequency, CancellationToken cancellationToken = default);

    }
}
