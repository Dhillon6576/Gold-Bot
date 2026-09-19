using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    // Independent strategy: fast/slow SMA crossover with percentage-based SL/TP.
    // This bot is separate from the TRIX reconstruction and does not use TRIX.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CryptoMovingAverageCrossover : Robot
    {
        [Parameter("Volume (units)", Group = "Trade", DefaultValue = 0.001, MinValue = 0.00000001, Step = 0.00000001)]
        public double VolumeInUnits { get; set; }

        [Parameter("Fast SMA Period", Group = "Strategy", DefaultValue = 9, MinValue = 1)]
        public int FastPeriod { get; set; }

        [Parameter("Slow SMA Period", Group = "Strategy", DefaultValue = 21, MinValue = 2)]
        public int SlowPeriod { get; set; }

        [Parameter("Stop Loss (%)", Group = "Risk", DefaultValue = 2.0, MinValue = 0.01, MaxValue = 100.0, Step = 0.01)]
        public double StopLossPercent { get; set; }

        [Parameter("Take Profit (%)", Group = "Risk", DefaultValue = 5.0, MinValue = 0.01, MaxValue = 500.0, Step = 0.01)]
        public double TakeProfitPercent { get; set; }

        [Parameter("Label", Group = "Trade", DefaultValue = "MA_CROSS_CRYPTO")]
        public string Label { get; set; }

        [Parameter("Allow New Entries", Group = "Trade", DefaultValue = true)]
        public bool AllowNewEntries { get; set; }

        [Parameter("Close On Bearish Cross", Group = "Trade", DefaultValue = true)]
        public bool CloseOnBearishCross { get; set; }

        [Parameter("Maximum Spread (pips)", Group = "Protection", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaximumSpreadPips { get; set; }

        private SimpleMovingAverage _fastSma;
        private SimpleMovingAverage _slowSma;
        private double _volume;
        private DateTime _lastProcessedBar;

        protected override void OnStart()
        {
            if (FastPeriod >= SlowPeriod)
            {
                Print("Fast SMA Period must be smaller than Slow SMA Period.");
                Stop();
                return;
            }

            if (StopLossPercent <= 0 || TakeProfitPercent <= 0)
            {
                Print("Stop Loss and Take Profit percentages must be greater than zero.");
                Stop();
                return;
            }

            _volume = Symbol.NormalizeVolumeInUnits(VolumeInUnits, RoundingMode.Down);
            if (_volume < Symbol.VolumeInUnitsMin || _volume > Symbol.VolumeInUnitsMax)
            {
                Print("Volume is outside broker limits. Requested={0}, normalized={1}, minimum={2}, maximum={3}",
                    VolumeInUnits, _volume, Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsMax);
                Stop();
                return;
            }

            _fastSma = Indicators.SimpleMovingAverage(Bars.ClosePrices, FastPeriod);
            _slowSma = Indicators.SimpleMovingAverage(Bars.ClosePrices, SlowPeriod);
            _lastProcessedBar = Bars.OpenTimes.LastValue;

            Print("Started {0}: {1}, timeframe={2}, fast SMA={3}, slow SMA={4}, volume={5}",
                Label, SymbolName, TimeFrame, FastPeriod, SlowPeriod, _volume);
            Print("Risk: SL={0}% and TP={1}%. Signals use completed candles.", StopLossPercent, TakeProfitPercent);
        }

        protected override void OnBar()
        {
            if (Bars.Count < SlowPeriod + 2)
                return;

            // OnBar is called as a new candle begins. Last(1) and Last(2)
            // therefore refer to the current and previous completed candles.
            double currentFast = _fastSma.Result.Last(1);
            double currentSlow = _slowSma.Result.Last(1);
            double previousFast = _fastSma.Result.Last(2);
            double previousSlow = _slowSma.Result.Last(2);

            bool bullishCross = previousFast <= previousSlow && currentFast > currentSlow;
            bool bearishCross = previousFast >= previousSlow && currentFast < currentSlow;

            Print("[{0:yyyy-MM-dd HH:mm}] SMA fast={1:F8}, slow={2:F8}, bullish={3}, bearish={4}",
                Bars.OpenTimes.Last(1), currentFast, currentSlow, bullishCross, bearishCross);

            var positions = Positions.FindAll(Label, SymbolName, TradeType.Buy);

            if (bearishCross && CloseOnBearishCross)
            {
                foreach (var position in positions)
                {
                    var result = ClosePosition(position);
                    if (!result.IsSuccessful)
                        Print("Failed to close position {0}: {1}", position.Id, result.Error);
                    else
                        Print("Bearish SMA cross closed position {0} at approximately {1}", position.Id, Symbol.Bid);
                }
                return;
            }

            if (!bullishCross || !AllowNewEntries || positions.Length > 0)
                return;

            if (MaximumSpreadPips > 0 && Symbol.Spread / Symbol.PipSize > MaximumSpreadPips)
            {
                Print("Buy skipped: spread {0:F2} pips exceeds limit {1:F2} pips.",
                    Symbol.Spread / Symbol.PipSize, MaximumSpreadPips);
                return;
            }

            double referencePrice = Symbol.Ask;
            double stopPrice = referencePrice * (1.0 - StopLossPercent / 100.0);
            double targetPrice = referencePrice * (1.0 + TakeProfitPercent / 100.0);
            double stopLossPips = (referencePrice - stopPrice) / Symbol.PipSize;
            double takeProfitPips = (targetPrice - referencePrice) / Symbol.PipSize;

            var order = ExecuteMarketOrder(
                TradeType.Buy,
                SymbolName,
                _volume,
                Label,
                stopLossPips,
                takeProfitPips,
                "Bullish SMA crossover");

            if (!order.IsSuccessful)
            {
                Print("Buy order failed: {0}", order.Error);
                return;
            }

            Print("BUY opened: position={0}, entry={1}, SL={2:F2} pips ({3:P2}), TP={4:F2} pips ({5:P2})",
                order.Position.Id, order.Position.EntryPrice, stopLossPips, StopLossPercent / 100.0,
                takeProfitPips, TakeProfitPercent / 100.0);
        }

        protected override void OnStop()
        {
            Print("{0} stopped. Open bot positions remaining: {1}",
                Label, Positions.FindAll(Label, SymbolName).Length);
        }
    }
}
