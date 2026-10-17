namespace CodexBarWin.Core.Models;

public sealed record Meter(string Label, double Used, double Limit, string Unit)
{
    public double Fraction
    {
        get
        {
            if (!(Limit > 0.0))
            {
                return 0.0;
            }
            return Used / Limit;
        }
    }

    public override string ToString()
    {
        return $"{Label}: {Used:0.#}{Unit}/{Limit:0.#}{Unit} ({Fraction:P0})";
    }
}
