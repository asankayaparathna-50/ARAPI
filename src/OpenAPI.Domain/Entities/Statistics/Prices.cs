using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAPI.Domain.Entities.Statistics
{
    public class MarketPrice
    {
        public List<Dictionary<string, object>>? prices { get; set; }
    }

    public class MarketPriceRawData
    {
        public string? ItemName { get; set; }
        public int DataCodeID { get; set; }
        public string? PeriodID { get; set; }
        public decimal? CurrentValue { get; set; }
    }

}