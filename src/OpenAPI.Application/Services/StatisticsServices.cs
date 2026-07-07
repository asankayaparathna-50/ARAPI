using OpenAPI.Domain.Entities.Statistics;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Application.Services
{
    public class StatisticsServices
    {

        private readonly IStatisticsRepository _repo;

        public StatisticsServices(IStatisticsRepository repo)
        {
            _repo = repo;
        }

        public async Task<PriceIndices> GetPriceIndices(string type, string? period,  CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetPriceIndices(type ,period, cancellationToken);

            if (result == null)
                throw new InvalidOperationException("No price indices found.");

            return result;
        }

        public async Task<MarketPrice> GetPrices(string period, string market, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetPrices(period, market, cancellationToken);

            if (result == null)
                throw new InvalidOperationException("No price indices found.");

            return result;
        }

        public async Task<List<GdpItem>> GetGdpGrowth(string frequency, string? period, string? year, string? quarter, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetGdpGrowth(frequency, period, year, quarter, cancellationToken);

            if (result == null || result.Count == 0)
                throw new InvalidOperationException("No GDP growth data found.");

            return result;
        }


        public async Task<List<ModalItem>> GetAgriculturalProduction(string period, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetAgriculturalProduction(period, cancellationToken);

            if (result == null)
                throw new InvalidOperationException("No agricultural production data found.");

            return result;
        }

        public async Task<List<ModalItem>> GetIndustrialProduction(string period, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetIndustrialProduction(period, cancellationToken);

            if (result == null)
                throw new InvalidOperationException("No industrial production data found.");

            return result;
        }

        public async Task<List<ModalItem>> GetPMI(string period, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetPMI(period, cancellationToken);

            if (result == null || result.Count == 0)
                throw new InvalidOperationException("No PMI data found.");

            return result;
        }

        public async Task<List<ModalItem>> GetEmployment(string period, string frequency, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetEmployment(period, frequency, cancellationToken);
            if (result == null || result.Count == 0)
                throw new InvalidOperationException("No employment data found.");
            return result;
        }

        public async Task<object> GetWageRateIndices(string period, string frequency, CancellationToken cancellationToken = default)
        {
            var item = await _repo.GetWageRateIndices(period, frequency, cancellationToken);

            
            var result = new
                {
                    period = period,
                    frequency = frequency,
                    data = new {
                        WageRate = item.Where(d => d.ItemName?.Contains("Y-o-Y Change", StringComparison.OrdinalIgnoreCase) == false)
                                            .ToDictionary(d => d.ItemName!, d => d.CurrentValue),
                        Change = item.Where(d => d.ItemName?.Contains("Y-o-Y Change", StringComparison.OrdinalIgnoreCase) == true)
                            .ToDictionary(d => d.ItemName!, d => d.CurrentValue)
                    }
                };
            if (result == null || (result.data.WageRate.Count == 0 && result.data.Change.Count == 0))
                throw new InvalidOperationException("No wage rate indices data found.");

            return result;
        }

        public async Task<object> GetCrudeOilPrices(string period, CancellationToken cancellationToken = default)
        {
            var monthlyItem = await _repo.GetCrudeOilPrices(period, "M", cancellationToken);
            var dailyItem = await _repo.GetCrudeOilPrices(period, "D", cancellationToken);

            if (monthlyItem.Count == 0 && dailyItem.Count == 0)
                throw new InvalidOperationException("No crude oil price data found.");

            var result = new
            {
                Period = period,
                Data = new
                {
                    MonthlyPrices = BuildData(monthlyItem), 
                    DailyPrices = BuildData(dailyItem)
                }
            };
            return result;
        }

        public async Task<object> GetDailyElectricityGeneration(string period, CancellationToken cancellationToken = default)
        {
            string frequency = "D";
            var items = await _repo.GetDailyElectricityGeneration(period, frequency, cancellationToken);
            if (items == null || items.Count == 0)
                throw new InvalidOperationException("No daily electricity generation data found.");
            
            var result = new
            {
                Period = period,
                Frequency = frequency,
                Data = BuildData(items)
            };
            return result;
        }

        //2.1
        public async Task<Modal> GetInterestRates(string period, CancellationToken cancellationToken = default)
        {
            var PolicyInterestRate = await _repo.GetInterestRates(period, "D", "PolicyInterestRate", cancellationToken);
            var StandingFacilityRates = await _repo.GetInterestRates(period, "D", "StandingFacilityRates", cancellationToken);
            var CallMoneyMarket = await _repo.GetInterestRates(period, "D", "CallMoneyMarket", cancellationToken);
            var TreasuryBillYield = await _repo.GetInterestRates(period, "D", "TreasuryBillYield", cancellationToken);
            var LendingDepositRates = await _repo.GetInterestRates(period, "M", "LendingDepositRates", cancellationToken);
            var NSB = await _repo.GetInterestRates(period, "M", "NSB", cancellationToken);
            var BankwiseAWPR = await _repo.GetInterestRates(period, "D", "BankwiseAWPR", cancellationToken);

            var result = new Modal
            {
                Period = period,
                Frequency = "",
                Data = new
                {
                    PolicyInterestRate = BuildData(PolicyInterestRate),
                    StandingFacilityRates = BuildData(StandingFacilityRates),
                    CallMoneyMarket = BuildData(CallMoneyMarket),
                    TreasuryBillYield = BuildData(TreasuryBillYield),
                    LendingDepositRates = BuildData(LendingDepositRates),
                    NSB = BuildData(NSB),
                    BankwiseAWPR = BuildData(BankwiseAWPR)
                }
            };

            return result;
        }

        //2.2
        public async Task<Modal> GetMoneySupply(string period, CancellationToken cancellationToken = default)
        {
            var moneySupply = await _repo.GetMoneySupply(period, "M", cancellationToken);
           
            var result = new Modal
            {
                Period = period,
                Frequency = "",
                Data = BuildData(moneySupply)
            };

            return result;
        }

        //2.3
        public async Task<Modal> GetReserveMoney(string period, CancellationToken cancellationToken = default)
        {
            var reserveMoney = await _repo.GetReserveMoney(period, "M", cancellationToken);
           
            var result = new Modal
            {
                Period = period,
                Frequency = "M",
                Data = BuildData(reserveMoney)
            };

            return result;
        }

        //2.4
        public async Task<Modal> GetMoneyMarketActivity(string period, CancellationToken cancellationToken = default)
        {
            var callMoneyMarket = await _repo.GetMoneyMarketActivity(period, "D", "CallMoneyMarket", cancellationToken);
            var repoMarket = await _repo.GetMoneyMarketActivity(period, "D", "RepoMarket", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "D",
                Data = new
                {
                    CallMoneyMarket = BuildData(callMoneyMarket),
                    RepoMarket = BuildData(repoMarket)
                }
            };

            return result;
        }

        //2.5
        public async Task<Modal> GetCbslSecuritiesPortfolio(string period, CancellationToken cancellationToken = default)
        {
            var cbslSecuritiesPortfolio = await _repo.GetCbslSecuritiesPortfolio(period, "D", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "D",
                Data = BuildData(cbslSecuritiesPortfolio)
            };

            return result;
        }

        //2.6

        //2.71
        public async Task<Modal> GetCreditCards(string period, CancellationToken cancellationToken = default)
        {
            var creditCards = await _repo.GetCreditCards(period, "D", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "D",
                Data = BuildData(creditCards)
            };

            return result;
        }

        //2.72
        public async Task<Modal> GetCommercialPaperIssue(string period, CancellationToken cancellationToken = default)
        {
            var commercialPaperIssue = await _repo.GetCommercialPaperIssue(period, "M", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "M",
                Data = BuildData(commercialPaperIssue)
            };

            return result;
        }

        //2.8
        public async Task<Modal> GetShareMarket(string period, CancellationToken cancellationToken = default)
        {
            var shareMarket = await _repo.GetShareMarket(period, "M", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "M",
                Data = BuildData(shareMarket)
            };

            return result;
        }

        //3.1
        public async Task<Modal> GetGovernmentFinance(string period, CancellationToken cancellationToken = default)
        {
            var governmentFinance = await _repo.GetGovernmentFinance(period, "M", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "M",
                Data = BuildData(governmentFinance)
            };

            return result;
        }

        //3.2
        public async Task<Modal> GetOutstandingCentralGovernmentDebt(string period, CancellationToken cancellationToken = default)
        {
            var outstandingCentralGovernmentDebt = await _repo.GetOutstandingCentralGovernmentDebt(period, "M", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "M",
                Data = BuildData(outstandingCentralGovernmentDebt)
            };

            return result;
        }

        //3.3.1
        public async Task<Modal> GetGovernmentSecurities(string period, CancellationToken cancellationToken = default)
        {
            var primaryTreasuryBills = await _repo.GetGovernmentSecurities(period, "D", "primaryTreasuryBills", cancellationToken);
            var primaryTreasuryBonds = await _repo.GetGovernmentSecurities(period, "D", "primaryTreasuryBonds", cancellationToken);
            var secondaryMarketBuyingTreasuryBills = await _repo.GetGovernmentSecurities(period, "D", "SecondaryMarketBuyingTreasuryBills", cancellationToken);
            var secondaryMarketBuyingTreasuryBonds = await _repo.GetGovernmentSecurities(period, "D", "SecondaryMarketBuyingTreasuryBonds", cancellationToken);
            var secondaryMarketSellingTreasuryBills = await _repo.GetGovernmentSecurities(period, "D", "SecondaryMarketSellingTreasuryBills", cancellationToken);
            var secondaryMarketSellingTreasuryBonds = await _repo.GetGovernmentSecurities(period, "D", "SecondaryMarketSellingTreasuryBonds", cancellationToken);
            var secondaryMarketAverageTreasuryBills = await _repo.GetGovernmentSecurities(period, "D", "SecondaryMarketAverageTreasuryBills", cancellationToken);
            var secondaryMarketAverageTreasuryBonds = await _repo.GetGovernmentSecurities(period, "D", "SecondaryMarketAverageTreasuryBonds", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "D",
                Data = new
                {
                    PrimaryMarket = new {
                        TreasuryBills = BuildData(primaryTreasuryBills),
                        TreasuryBonds = BuildData(primaryTreasuryBonds)
                    },
                    SecondaryMarketBuying = new {
                        TreasuryBills = BuildData(secondaryMarketBuyingTreasuryBills),
                        TreasuryBonds = BuildData(secondaryMarketBuyingTreasuryBonds)
                    },
                    SecondaryMarketSelling = new {
                        TreasuryBills = BuildData(secondaryMarketSellingTreasuryBills),
                        TreasuryBonds = BuildData(secondaryMarketSellingTreasuryBonds)
                    },
                    SecondaryMarketAverage = new {
                        TreasuryBills = BuildData(secondaryMarketAverageTreasuryBills),
                        TreasuryBonds = BuildData(secondaryMarketAverageTreasuryBonds)
                    }
                }
            };

            return result;
        }

        //3.3.2
        public async Task<Modal> GetInternationalSovereignBonds(string period, CancellationToken cancellationToken = default)
        {
            var internationalSovereignBonds = await _repo.GetInternationalSovereignBonds(period, "D", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "D",
                Data = BuildData(internationalSovereignBonds)
            };

            return result;
        }

        //3.4
        public async Task<Modal> GetPrimarySecondaryMarketTransactions(string period, CancellationToken cancellationToken = default)
        {
            var outstandingStock = await _repo.GetPrimarySecondaryMarketTransactions(period, "D", "outstandingStock", cancellationToken);
            var primaryMarketActivityTreasuryBills = await _repo.GetPrimarySecondaryMarketTransactions(period, "D", "primaryMarketActivityTreasuryBills", cancellationToken);
            var primaryMarketActivityNonCompetitiveAllocation = await _repo.GetPrimarySecondaryMarketTransactions(period, "D", "primaryMarketActivityNonCompetitiveAllocation", cancellationToken);
            var primaryMarketActivityTreasuryBonds = await _repo.GetPrimarySecondaryMarketTransactions(period, "D", "primaryMarketActivityTreasuryBonds", cancellationToken);
            var primaryMarketActivityDirectIssuanceWindow = await _repo.GetPrimarySecondaryMarketTransactions(period, "D", "primaryMarketActivityDirectIssuanceWindow", cancellationToken);
            var secondaryMarketActivityTreasuryBills = await _repo.GetPrimarySecondaryMarketTransactions(period, "D", "secondaryMarketActivityTreasuryBills", cancellationToken);
            var secondaryMarketActivityTreasuryBonds = await _repo.GetPrimarySecondaryMarketTransactions(period, "D", "secondaryMarketActivityTreasuryBonds", cancellationToken);
            
            var result = new Modal
            {
                Period = period,
                Frequency = "D",
                Data = new
                {
                    OutstandingStockOfGovernmentSecurities = BuildData(outstandingStock),
                    PrimaryMarketActivities = new {
                        OutstandingStock = BuildData(outstandingStock),
                        PrimaryMarketActivityTreasuryBills = BuildData(primaryMarketActivityTreasuryBills),
                        PrimaryMarketActivityNonCompetitiveAllocation = BuildData(primaryMarketActivityNonCompetitiveAllocation),
                        TreasuryBonds = BuildData(primaryMarketActivityTreasuryBonds),
                        DirectIssuanceWindow = BuildData(primaryMarketActivityDirectIssuanceWindow)
                    },
                    SecondaryMarketActivities = new {
                        TreasuryBills = BuildData(secondaryMarketActivityTreasuryBills),
                        TreasuryBonds = BuildData(secondaryMarketActivityTreasuryBonds)
                    }
                }
            };

            return result;
        }

        //3.5

        //3.6

        //3.7

        //4.1
        public async Task<Modal> GetExchangeRates(string period, string? type, CancellationToken cancellationToken = default)
        {
            var result = new Modal { Period = period, Frequency = "D", Data = new {} };

            if(!string.IsNullOrWhiteSpace(type))
            {
                var exchangeRate = await _repo.GetExchangeRates(period, "D", type, cancellationToken);
                result.Data = BuildData(exchangeRate);
                return result;
            }

            var exchangeRate_BuyingRate = await _repo.GetExchangeRates(period, "D", "ExchangeRate_BuyingRate", cancellationToken);
            var exchangeRate_SellingRate = await _repo.GetExchangeRates(period, "D", "ExchangeRate_SellingRate", cancellationToken);
            var exchangeRate_AverageRate = await _repo.GetExchangeRates(period, "D", "ExchangeRate_AverageRate", cancellationToken);
            var centralBankPurchesesAndSales = await _repo.GetExchangeRates(period, "D", "CentralBankPurchesesAndSales(USD mn)", cancellationToken);
            var forwardRates = await _repo.GetExchangeRates(period, "D", "ForwardRates(Rs per USD)", cancellationToken);
            
            result.Data = new
                {
                    ExchangeRate_BuyingRate = BuildData(exchangeRate_BuyingRate),
                    ExchangeRate_SellingRate = BuildData(exchangeRate_SellingRate),
                    ExchangeRate_AverageRate = BuildData(exchangeRate_AverageRate),
                    CentralBankPurchesesAndSales = BuildData(centralBankPurchesesAndSales),
                    ForwardRates = BuildData(forwardRates)
                };
            

            return result;
        }
        
        //4.2
        public async Task<Modal> GetTourismAndWorkersRemittance(string period, CancellationToken cancellationToken = default)
        {
            var touristArrivals = await _repo.GetTourismAndWorkersRemittance(period, "D", "TouristArrivals", cancellationToken);
            var earningFromTourism = await _repo.GetTourismAndWorkersRemittance(period, "D", "EarningFromTourism", cancellationToken);
            var workersRemittancesInflows = await _repo.GetTourismAndWorkersRemittance(period, "D", "WorkersRemittancesInflows", cancellationToken);
            
            var result = new Modal 
            { 
                Period = period, 
                Frequency = "D", 
                Data = new
                {
                    TouristArrivals = BuildData(touristArrivals),
                    EarningFromTourism = BuildData(earningFromTourism),
                    WorkersRemittancesInflows = BuildData(workersRemittancesInflows)
                }
            };

            return result;
        }
        
        //4.3

        //4.4

        //4.5
        public async Task<Modal> GetExternalTrade(string period, CancellationToken cancellationToken = default)
        {
            var item = await _repo.GetExternalTrade(period, "M", cancellationToken);
            var result = new Modal 
            { 
                Period = period, 
                Frequency = "D", 
                Data = BuildData(item)
            };

            return result;
        }

        //4.6
        public async Task<Modal> GetTradeIndices(string period, CancellationToken cancellationToken = default)
        {
            var item = await _repo.GetTradeIndices(period, "M", cancellationToken);
            var result = new Modal 
            { 
                Period = period, 
                Frequency = "D", 
                Data = BuildData(item)
            };

            return result;
        }
        
        //4.7
        public async Task<Modal> GetCommodityPrice(string period, CancellationToken cancellationToken = default)
        {
            var colomboTeaAuctionUSD = await _repo.GetCommodityPrice(period, "M", "ColomboTeaAuctionUSD", cancellationToken);
            var colomboTeaAuctionLKR = await _repo.GetCommodityPrice(period, "M", "ColomboTeaAuctionLKR", cancellationToken);
            var importsCIFUSD = await _repo.GetCommodityPrice(period, "M", "Imports(CIF)USD", cancellationToken);
            var importsCIFLKR = await _repo.GetCommodityPrice(period, "M", "Imports(CIF)LKR", cancellationToken);
       

            var result = new Modal 
            { 
                Period = period, 
                Frequency = "M", 
                Data = new
                {
                    ColomboTeaAuction = new {
                        USD = BuildData(colomboTeaAuctionUSD),
                        LKR = BuildData(colomboTeaAuctionLKR)
                    },
                    ImportsCIF = new {
                        USD = BuildData(importsCIFUSD),
                        LKR = BuildData(importsCIFLKR)
                    }
                }
            };

            return result;
        }
        


        private static Dictionary<string, decimal?> BuildData(IEnumerable<ModalItem> items)
        {
            var data = new Dictionary<string, decimal?>();

            if (items == null)
                return data;

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ItemName))
                    continue;

                data[item.ItemName] = item.CurrentValue;
            }

            return data;
        }


        //Daily EI

        //d.1
        public async Task<Modal> GetRealGdpGrowth(string period, CancellationToken cancellationToken = default)
        {
            var item = await _repo.GetRealGdpGrowth_ByPeriod(period, "Q", cancellationToken);
            var result = new Modal
            {
                Period = period,
                Frequency = "Q",
                Data = BuildData(item)
            };

            return result;
        }

        //d.2
        public async Task<Modal> GetYoyGrowth(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "M";
            var item = await _repo.GetPricesAndIndices_ByPeriod(period, frequencyCode, cancellationToken);
            var result = new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(item)
            };

            return result;
        }

        //d.3
        public async Task<Modal> GetTTRate(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var item = await _repo.GetTTRate_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(item)
            };
        }

        //d.4
        public async Task<Modal> GetMonySupply(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var item = await _repo.GetMonySupply_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(item)
            };
        }
        
        //d.5
        public async Task<Modal> GetUsdSpotRate(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetOpenMarketOperations_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items.Where(i => i.ItemName?.Contains("USD", StringComparison.OrdinalIgnoreCase) == true))
            };
        }
        
        //d.6
        public async Task<Modal> GetPolicyRates(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetPolicyRates_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items)
            };
        }

        //d.7
        public async Task<Modal> GetAwpr(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetAwpr(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items)
            };
        }

        //d.8
        public async Task<Modal> GetOvernightLiquidity(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetOvernightLiquidity_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items.Where(i => i.ItemName?.Contains("Overnight", StringComparison.OrdinalIgnoreCase) == true))
            };
        }
        
        //d.9
        public async Task<Modal> GetTreasuryBillYield(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetTreasuryBillYield_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items)
            };
        }
        
        //d.10
        public async Task<Modal> GetAllSharePriceIndex(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetAllSharePriceIndex_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items)
            };
        }

        //d.11
        public async Task<Modal> GetPetroleum(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetPetroleum_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items)
            };
        }

        //d.12
        public async Task<Modal> GetElectricity(string period, CancellationToken cancellationToken = default)
        {
            var frequencyCode = "D";
            var items = await _repo.GetElectricity_ByPeriod(period, frequencyCode, cancellationToken);
            return new Modal
            {
                Period = period,
                Frequency = frequencyCode,
                Data = BuildData(items)
            };
        }

    }
}
