# CryptoMovingAverageCrossover cBot

This is a **separate strategy** from the TRIX reconstruction. It uses only a fast/slow Simple Moving Average crossover.

## Strategy

- Buy when the fast SMA crosses above the slow SMA.
- Close the buy when the fast SMA crosses below the slow SMA, if `Close On Bearish Cross` is enabled.
- Use broker-side percentage-based stop-loss and take-profit orders.
- One bot position per symbol and label.
- Signals are evaluated only on completed candles to avoid acting on an unfinished candle.

The supplied Python defaults are preserved:

- Fast SMA: 9
- Slow SMA: 21
- Stop loss: 2%
- Take profit: 5%
- Timeframe: choose `1 Hour` in cTrader
- Symbol: choose the broker's crypto symbol, for example BTCUSDT or BTCUSD

## Important cTrader differences

The Python example used `TRADE_VOLUME = 0.001`, which represents base-asset quantity on many crypto exchanges. cTrader's API uses volume in units and broker symbol specifications differ. Enter a volume accepted by your cTrader broker and verify the normalized volume shown in the log.

The Python example checked `fast_ma > slow_ma`, which can repeatedly signal while the averages remain in the same order. This cBot implements a true crossover using both the previous and current completed candles, so it opens only on the crossing event.

The percentage SL/TP is converted to pips at entry and submitted as broker-side protection. Because cTrader may calculate pip size, spread, commission, and execution differently from CCXT/Binance, the backtest will not be numerically identical to the Python script.

## Backtesting

1. Open cTrader Automate and create/import a cBot.
2. Add `CryptoMovingAverageCrossover.cs` to the cBot project or paste it into a new cBot.
3. Select a crypto symbol available from your broker.
4. Select the desired timeframe; use 1 Hour to match the Python default.
5. Set volume according to the symbol's allowed units.
6. Backtest with realistic commission and spread.
7. Compare trade count, entry times, exits, drawdown, and net profit.

Start in backtesting or demo mode. This code does not include exchange API keys and does not connect to Binance directly; cTrader executes through the broker's cTrader symbol and account.
