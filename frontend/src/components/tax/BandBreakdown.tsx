import { formatCurrency, formatPercent } from '@/lib/formatters';
import { TaxBandCharge } from '@/types';

/** Tax charged in each band: a table whose cells carry a single-hue magnitude bar. */
export default function BandBreakdown({ bands }: { bands: TaxBandCharge[] }) {
  const max = Math.max(...bands.map((band) => band.taxAmount), 1);

  return (
    <div className="-mx-1 overflow-x-auto">
      <table className="w-full min-w-[480px] text-sm">
        <caption className="sr-only">Tax charged in each income band</caption>
        <thead>
          <tr className="text-left text-[13px] text-ink-subtle">
            <th scope="col" className="px-1 pb-2 font-medium">Band</th>
            <th scope="col" className="px-1 pb-2 text-right font-medium">Rate</th>
            <th scope="col" className="px-1 pb-2 text-right font-medium">Income in band</th>
            <th scope="col" className="w-[34%] px-1 pb-2 font-medium">
              <span className="sr-only">Tax charged</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {bands.map((band) => (
            <tr key={band.label} className="border-t border-line">
              <th scope="row" className="figure px-1 py-2.5 text-left font-normal text-ink-muted">
                {band.upperLimit ? `${formatCurrency(band.lowerLimit)} – ${formatCurrency(band.upperLimit)}` : `Above ${formatCurrency(band.lowerLimit)}`}
              </th>
              <td className="figure px-1 py-2.5 text-right font-medium">{formatPercent(band.rate, 0)}</td>
              <td className="figure px-1 py-2.5 text-right">{formatCurrency(band.taxableAmount)}</td>
              <td className="px-1 py-2.5">
                <div className="flex items-center gap-2">
                  <div className="h-3 flex-1">
                    {band.taxAmount > 0 && (
                      <div className="h-3 rounded-r-[4px] bg-chart-kept" style={{ width: `${Math.max(2, (band.taxAmount / max) * 100)}%` }} />
                    )}
                  </div>
                  <span className="figure w-24 shrink-0 text-right font-semibold">{formatCurrency(band.taxAmount)}</span>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
