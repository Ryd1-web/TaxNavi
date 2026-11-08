namespace TaxNavi.Domain.Models;

/// <summary>Naira ⇄ kobo conversion. All provider comparisons happen in integer kobo.</summary>
public static class Money
{
    public static long ToKobo(decimal naira) => checked((long)decimal.Round(naira * 100m, 0, MidpointRounding.AwayFromZero));

    public static decimal FromKobo(long kobo) => kobo / 100m;
}
