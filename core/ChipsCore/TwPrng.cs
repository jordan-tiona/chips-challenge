namespace ChipsCore;

/// <summary>
/// Tile World's random-number generator, reproduced bit-for-bit (see
/// tworld random.c). Solutions can only replay correctly if walkers,
/// blobs, and random force floors consume the exact same sequence the
/// original game produced from the recorded seed.
/// </summary>
public sealed class TwPrng
{
    private uint _value;

    public TwPrng(uint seed)
    {
        _value = seed & 0x7FFFFFFF;
    }

    private uint Next()
    {
        _value = (_value * 1103515245u + 12345u) & 0x7FFFFFFF;
        return _value;
    }

    /// <summary>Top two bits: uniform 0..3.</summary>
    public int Random4() => (int)(Next() >> 29);

    /// <summary>Permute elements 1 and 2 of a 3-element window (index 0 is
    /// left alone by TW's randomp3, which permutes all three positions of
    /// the slice it's given — callers pass the slice, as TW does).</summary>
    public void Permute3(Direction[] a)
    {
        var v = Next();
        var n = (int)(v >> 30);
        (a[n], a[1]) = (a[1], a[n]);
        n = (int)(3.0 * (v & 0x3FFFFFFF) / 0x40000000);
        (a[n], a[2]) = (a[2], a[n]);
    }

    public void Permute4(Direction[] a)
    {
        var v = Next();
        var n = (int)(v >> 30);
        (a[n], a[1]) = (a[1], a[n]);
        n = (int)(3.0 * (v & 0x0FFFFFFF) / 0x10000000);
        (a[n], a[2]) = (a[2], a[n]);
        n = (int)((v >> 28) & 3);
        (a[n], a[3]) = (a[3], a[n]);
    }
}
