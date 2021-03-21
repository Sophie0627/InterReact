﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using InterReact.Messages;
using InterReact.Utility;

namespace InterReact.Extensions
{
    public static class ContractDataStrikeEx
    {
        /// <summary>
        /// Selects contract(s) from a list of contracts with varying expiry dates.
        /// The reference price, in combination with the offset, is used to select contract(s) depending on strike.
        /// An offset of 0 returns contract(s) with a strike which is equal of greater to the reference price.
        /// An offset of +1 returns contract(s) with the next greater strike.
        /// Negative Values select contract(s) with strikes below the reference price.
        /// </summary>
        public static IObservable<IReadOnlyList<ContractData>> ContractDataStrikeSelect(
                this IObservable<IReadOnlyList<ContractData>> source, int offset, double referencePrice)
            => ThrowIf.ThrowIfEmpty(source).Select(cds => SelectStrike(cds, offset, referencePrice));

        internal static List<ContractData> SelectStrike(IEnumerable<ContractData> cds, int offset, double reference)
        {
            var groups = cds
                .GroupBy(cd => cd.Contract.Strike)
                .OrderBy(g => g.Key)
                .ToList();

            var strikes = groups.Select(y => y.Key).ToList();

            if (strikes.Any(key => key <= 0))
                throw new InvalidDataException("Invalid strike.");

            var pos = strikes.BinarySearch(reference);
            if (pos < 0)
                pos = ~pos;

            var index = offset + pos;

            if (index < 0 || index > groups.Count - 1) // invalid index
                return new List<ContractData>();

            return groups[index].ToList();
        }

    }
}
