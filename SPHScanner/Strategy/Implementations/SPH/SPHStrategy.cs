﻿using System;
using System.Collections.Generic;
using ExchangeSharp;

namespace SPHScanner
{
    public class SPHStrategy
    {
        public SPHStrategy()
        {
        }

        /// <summary>
        /// Search for SPH's in the candle list
        /// </summary>
        /// <returns>List of SPH's found</returns>
        /// <param name="symbol">Symbol.</param>
        /// <param name="candles">Candles list</param>
        internal List<SPH> Find(string symbol, List<MarketCandle> candles)
        {
            var result = new List<SPH>();

            for (var i = candles.Count - 1; i > 0; i--)
            {
                // Find panic....
                var candleIndex = i;
                var totalPanic  = 0M;
                var candleCount = 0M;
                while (candleIndex > 0)
                {
                    var candle = candles[candleIndex];
                    if (!candle.IsRedCandle()) break;
                    totalPanic += candle.BodyPercentage();
                    candleIndex--;
                    candleCount++;
                }

                if (candleCount > 0) 
                {
                    var panicPerCandle = totalPanic /  candleCount;
                    if (panicPerCandle < 5m && candleCount > 1)
                    {
                        // perhaps the start candle is part of the stability phase and not the panic phase.
                        candleCount--;
                        candleIndex++;
                        var candle = candles[candleIndex];
                        var candlePercentage = candle.BodyPercentage();
                        if (candlePercentage < 5m)
                        {
                            totalPanic = totalPanic - candlePercentage;
                            panicPerCandle = totalPanic / candleCount;
                        }
                    }
                    if (panicPerCandle >= 5m)
                    {
                        // we found panic.. 
                        var startCandleIndex = i - (int)(candleCount) + 1;
                        var endCandleIndex = i;

                        var startPrice = candles[startCandleIndex].OpenPrice;
                        var panicPrice = candles[endCandleIndex].ClosePrice;

                        // Now check for stability before the panic appeared
                        if (StabilityFound(candles, startCandleIndex, startPrice))
                        {
                            // Stability found
                            // Now check if price retraces back to opening price quickly
                            if (PriceRetracesTo(candles, startPrice, endCandleIndex + 1, (int)(candleCount * 2)))
                            {
                                // found fast retracement, check if SPH is still valid
                                if (!PriceWentBelow(candles, panicPrice, endCandleIndex))
                                {
                                    // SPH is still valid, add it to the result list.
                                    var sph = new SPH();
                                    sph.Symbol = candles[startCandleIndex].Name;
                                    sph.Price = candles[endCandleIndex].ClosePrice;
                                    sph.Date = candles[endCandleIndex].Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                                    result.Add(sph);
                                }
                            }
                        }

                    }
                }

            }
            return result;
        }

        /// <summary>
        /// Returns if price went below the panic price
        /// </summary>
        /// <returns><c>true</c>, if price went below panic price, <c>false</c> otherwise.</returns>
        /// <param name="candles">list of candles</param>
        /// <param name="panicPrice">Panic price.</param>
        /// <param name="candleIndex">candle to look from.</param>
        private bool PriceWentBelow(List<MarketCandle> candles, decimal panicPrice, int candleIndex)
        {
            for (int i = candles.Count-1; i > candleIndex; i--)
            {
                var candle = candles[i];
                var minPrice = Math.Min(candle.OpenPrice, candle.ClosePrice);
                if (minPrice < panicPrice) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if price retraces within a few candles
        /// </summary>
        /// <returns><c>true</c>, if price retraced, <c>false</c> otherwise.</returns>
        /// <param name="candles">Candles list</param>
        /// <param name="price">price to which current price should retrace</param>
        /// <param name="startIndex">start candle.</param>
        /// <param name="maxCandles">Max candles in which price must have retraced back</param>
        private bool PriceRetracesTo(List<MarketCandle> candles, decimal price, int startIndex, int maxCandles)
        {
            for (int i = startIndex; i <= startIndex + maxCandles; ++i)
            {
                var candle = candles[i];
                if (candle.ClosePrice >= price) return true;
            }
            return false;
        }


        /// <summary>
        /// Checks if there is a region of stability around the average price
        /// </summary>
        /// <returns><c>true</c>, if stability was found, <c>false</c> otherwise.</returns>
        /// <param name="candles">Candles list</param>
        /// <param name="startIndex">Start candle</param>
        /// <param name="averagePrice">Average price.</param>
        private bool StabilityFound(List<MarketCandle> candles, int startIndex, decimal averagePrice)
        {
            // allow price to fluctuate +- 3.5% around the average price
            var priceRangeLow  = (averagePrice / 100.0m) * (100m-3.5m);
            var priceRangeHigh = (averagePrice / 100.0m) * (100m+3.5m);

            var stabilityCandles = 0;
            for (int i = startIndex - 1; i > 0; i--)
            {
                var candle = candles[i];
                var candleBodyLow = Math.Min(candle.OpenPrice, candle.ClosePrice);
                var candleBodyHigh = Math.Max(candle.OpenPrice, candle.ClosePrice);
                if (candleBodyLow >= priceRangeLow && candleBodyHigh <= priceRangeHigh)
                {
                    stabilityCandles++;
                }
                else
                {
                    break;
                }
            }

            return stabilityCandles >= 4;
        }
    }
}