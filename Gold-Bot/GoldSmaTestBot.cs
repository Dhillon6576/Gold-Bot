using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum GoldTestPreset
    {
        Manual,
        M2_Fast_50_200,
        M15_20_50,
        H1_20_50,
        H1_20_100
    }

    // Gold-only SMA crossover test bot. Separate from the crypto bot and TRIX bot.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GoldSmaTestBot : Robot
    {
        [Parameter("Preset", Group = "01 Preset", DefaultValue = GoldTestPreset.Manual)]
        public GoldTestPreset Preset { get; set; }

        [Parameter("Fast SMA", Group = "02 Strategy", DefaultValue = 20, MinValue = 1)]
        public int FastSma { get; set; }

        [Parameter("Slow SMA", Group = "02 Strategy", DefaultValue = 50, MinValue = 2)]
        public int SlowSma { get; set; }

        [Parameter("Use 200 SMA Trend Filter", Group = "02 Strategy", DefaultValue = true)]
        public bool UseTrendFilter { get; set; }

        [Parameter("Trend SMA Period", Group = "02 Strategy", DefaultValue = 200, MinValue = 2)]
        public int TrendSmaPeriod { get; set; }

        [Parameter("Stop Loss (%)", Group = "03 Risk", DefaultValue = 0.5, MinValue = 0.01, Step = 0.01)]
        public double StopLossPercent { get; set; }

        [Parameter("Take Profit (%)", Group = "03 Risk", DefaultValue = 1.0, MinValue = 0.01, Step = 0.01)]
        public double TakeProfitPercent { get; set; }

        [Parameter("Volume (units)", Group = "03 Risk", DefaultValue = 1, MinValue = 0.01, Step = 0.01)]
        public double Volume { get; set; }

        [Parameter("Maximum Positions", Group = "03 Risk", DefaultValue = 1, MinValue = 1, MaxValue = 5)]
        public int MaximumPositions { get; set; }

        [Parameter("Close On Bearish Cross", Group = "04 Exit", DefaultValue = true)]
        public bool CloseOnBearishCross { get; set; }

        [Parameter("Use Session Filter", Group = "05 Filters", DefaultValue = false)]
        public bool UseSessionFilter { get; set; }

        [Parameter("Session Start UTC", Group = "05 Filters", DefaultValue = 7, MinValue = 0, MaxValue = 23)]
        public int SessionStartUtc { get; set; }

        [Parameter("Session End UTC", Group = "05 Filters", DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int SessionEndUtc { get; set; }

        [Parameter("Cooldown Bars", Group = "05 Filters", DefaultValue = 0, MinValue = 0)]
        public int CooldownBars { get; set; }

        [Parameter("Maximum Spread (pips)", Group = "05 Filters", DefaultValue = 0, MinValue = 0)]
        public double MaximumSpreadPips { get; set; }

        [Parameter("Label", Group = "06 General", DefaultValue = "GOLD_SMA_TEST")]
        public string Label { get; set; }

        private SimpleMovingAverage _fast;
        private SimpleMovingAverage _slow;
        private SimpleMovingAverage _trend;
        private double _volume;
        private int _barsSinceTrade = 1000000;
        private int _entries;

        protected override void OnStart()
        {
            ApplyPreset();

            if (!IsGold(SymbolName)) { Print("Use an XAU/GOLD symbol. Current symbol: {0}", SymbolName); Stop(); return; }
            if (FastSma >= SlowSma) { Print("Fast SMA must be smaller than Slow SMA."); Stop(); return; }
            if (UseTrendFilter && TrendSmaPeriod < SlowSma) { Print("Trend SMA should normally be >= Slow SMA."); Stop(); return; }

            _volume = Symbol.NormalizeVolumeInUnits(Volume, RoundingMode.Down);
            if (_volume < Symbol.VolumeInUnitsMin || _volume > Symbol.VolumeInUnitsMax)
            {
                Print("Invalid volume. Requested={0}; broker range={1}-{2}; step={3}", Volume, Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsMax, Symbol.VolumeInUnitsStep);
                Stop(); return;
            }

            _fast = Indicators.SimpleMovingAverage(Bars.ClosePrices, FastSma);
            _slow = Indicators.SimpleMovingAverage(Bars.ClosePrices, SlowSma);
            _trend = Indicators.SimpleMovingAverage(Bars.ClosePrices, TrendSmaPeriod);

            Print("GOLD TEST | preset={0} symbol={1} timeframe={2} volume={3}", Preset, SymbolName, TimeFrame, _volume);
            Print("Settings | fast={0} slow={1} trendFilter={2} trend={3} SL={4}% TP={5}% maxPos={6}", FastSma, SlowSma, UseTrendFilter, TrendSmaPeriod, StopLossPercent, TakeProfitPercent, MaximumPositions);
            Print("Filters | session={0} {1:00}:00-{2:00}:00 UTC cooldown={3} bars maxSpread={4} pips", UseSessionFilter, SessionStartUtc, SessionEndUtc, CooldownBars, MaximumSpreadPips);
        }

        protected override void OnBar()
        {
            _barsSinceTrade++;
            if (Bars.Count < Math.Max(TrendSmaPeriod, SlowSma) + 3) return;

            double f0 = _fast.Result.Last(1), s0 = _slow.Result.Last(1);
            double f1 = _fast.Result.Last(2), s1 = _slow.Result.Last(2);
            bool bullish = f1 <= s1 && f0 > s0;
            bool bearish = f1 >= s1 && f0 < s0;

            var positions = Positions.FindAll(Label, SymbolName, TradeType.Buy);
            if (bearish && CloseOnBearishCross)
            {
                foreach (var p in positions) ClosePosition(p);
                return;
            }
            if (!bullish || positions.Length >= MaximumPositions || _barsSinceTrade < CooldownBars) return;
            if (UseTrendFilter && Bars.ClosePrices.Last(1) <= _trend.Result.Last(1)) return;
            if (UseSessionFilter && !InSession(Bars.OpenTimes.Last(1).Hour)) return;
            if (MaximumSpreadPips > 0 && Symbol.Spread / Symbol.PipSize > MaximumSpreadPips) return;

            double entry = Symbol.Ask;
            double slPips = entry * StopLossPercent / 100.0 / Symbol.PipSize;
            double tpPips = entry * TakeProfitPercent / 100.0 / Symbol.PipSize;
            var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, _volume, Label, slPips, tpPips, "Gold SMA bullish cross");
            if (result.IsSuccessful) { _entries++; _barsSinceTrade = 0; Print("BUY #{0} entry={1} SL={2:F1} pips TP={3:F1} pips", result.Position.Id, result.Position.EntryPrice, slPips, tpPips); }
            else Print("BUY FAILED: {0}", result.Error);
        }

        private void ApplyPreset()
        {
            switch (Preset)
            {
                case GoldTestPreset.M2_Fast_50_200: FastSma = 50; SlowSma = 200; UseTrendFilter = false; TrendSmaPeriod = 200; StopLossPercent = 0.30; TakeProfitPercent = 0.60; MaximumPositions = 1; UseSessionFilter = true; SessionStartUtc = 7; SessionEndUtc = 20; CooldownBars = 10; break;
                case GoldTestPreset.M15_20_50: FastSma = 20; SlowSma = 50; UseTrendFilter = true; TrendSmaPeriod = 200; StopLossPercent = 0.50; TakeProfitPercent = 1.00; MaximumPositions = 1; UseSessionFilter = true; SessionStartUtc = 7; SessionEndUtc = 20; CooldownBars = 2; break;
                case GoldTestPreset.H1_20_50: FastSma = 20; SlowSma = 50; UseTrendFilter = true; TrendSmaPeriod = 200; StopLossPercent = 0.50; TakeProfitPercent = 1.00; MaximumPositions = 1; UseSessionFilter = false; CooldownBars = 0; break;
                case GoldTestPreset.H1_20_100: FastSma = 20; SlowSma = 100; UseTrendFilter = true; TrendSmaPeriod = 200; StopLossPercent = 0.75; TakeProfitPercent = 1.50; MaximumPositions = 1; UseSessionFilter = false; CooldownBars = 0; break;
            }
        }

        private bool InSession(int hour)
        {
            if (SessionStartUtc == SessionEndUtc) return true;
            return SessionStartUtc < SessionEndUtc ? hour >= SessionStartUtc && hour < SessionEndUtc : hour >= SessionStartUtc || hour < SessionEndUtc;
        }

        private static bool IsGold(string name)
        {
            string s = name.ToUpperInvariant();
            return s.Contains("XAU") || s.Contains("GOLD");
        }

        protected override void OnStop() { Print("Stopped. Entries={0}", _entries); }
    }
}
