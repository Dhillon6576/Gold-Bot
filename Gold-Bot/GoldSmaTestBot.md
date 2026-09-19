# GoldSmaTestBot test matrix

This is a separate gold SMA bot. It does not use TRIX and does not replace the crypto SMA bot.

Use the same `GoldSmaTestBot.cs` and select one of these presets. Attach it to the exact timeframe shown:

| Preset | Timeframe | Fast/slow | Trend filter | SL | TP | Session | Cooldown |
|---|---|---:|---|---:|---:|---|---:|
| `M2_Fast_50_200` | M2 | 50/200 | off | 0.30% | 0.60% | 07:00–20:00 UTC | 10 bars |
| `M15_20_50` | M15 | 20/50 | price above 200 SMA | 0.50% | 1.00% | 07:00–20:00 UTC | 2 bars |
| `H1_20_50` | H1 | 20/50 | price above 200 SMA | 0.50% | 1.00% | off | 0 |
| `H1_20_100` | H1 | 20/100 | price above 200 SMA | 0.75% | 1.50% | off | 0 |

## Exact test order

1. Run `H1_20_50` on XAUUSD for the same three-year period.
2. Run `H1_20_100`.
3. Run `M15_20_50`.
4. Run `M2_Fast_50_200` only as an experimental comparison against your previous M2 test.

Use tick data, realistic spread, actual commission, and the smallest broker-valid volume. Keep `Maximum Positions = 1` and `Close On Bearish Cross = true` for every comparison.

Do not select `Manual` until you deliberately want to change individual settings. Presets are applied when the bot starts and are printed in the backtest log.

The percentage exits are converted into pips and submitted as broker-side SL/TP. Results will depend on the broker's gold contract, pip size, spread, commission, and data quality.
