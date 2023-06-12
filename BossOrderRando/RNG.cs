using Crypto = System.Security.Cryptography;
using Text = System.Text;
using Num = System.Numerics;

namespace Haiku.BossOrderRando;

internal class RNG
{
    public RNG(string seed)
    {
        var encSeed = new Text.UTF8Encoding().GetBytes(seed);
        using var sha = Crypto.SHA256.Create();
        var h = sha.ComputeHash(encSeed);
        s0 = h[0] | ((ulong)h[1] << 8) | ((ulong)h[2] << 16) | ((ulong)h[3] << 24) | 
            ((ulong)h[4] << 32) | ((ulong)h[5] << 40) | ((ulong)h[6] << 48) |
            ((ulong)h[7] << 56);
        s1 = h[8] | ((ulong)h[9] << 8) | ((ulong)h[10] << 16) | ((ulong)h[11] << 24) |
            ((ulong)h[12] << 32) | ((ulong)h[13] << 40) | ((ulong)h[14] << 48) |
            ((ulong)h[15] << 56);
        s2 = h[16] | ((ulong)h[17] << 8) | ((ulong)h[18] << 16) | ((ulong)h[19] << 24) |
            ((ulong)h[20] << 32) | ((ulong)h[21] << 40) | ((ulong)h[22] << 48) |
            ((ulong)h[23] << 56);
        s3 = h[24] | ((ulong)h[25] << 8) | ((ulong)h[26] << 16) | ((ulong)h[27] << 24) |
            ((ulong)h[28] << 32) | ((ulong)h[29] << 40) | ((ulong)h[30] << 48) |
            ((ulong)h[31] << 56);
    }

    // Blackman and Vigna's xoshiro256** PRNG,
    // based on the C implementation found at https://prng.di.unimi.it/xoshiro256starstar.c
    private ulong s0;
    private ulong s1;
    private ulong s2;
    private ulong s3;

    private ulong Next()
    {
        var res = s1 * 5;
        res = ((res << 7) | (res >> 57)) * 9;

        var t = s1 << 17;

        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;

        s2 ^= t;

        s3 = (s3 << 45) | (s3 >> 19);

        return res;
    }

    private static ulong Smear(ulong x)
    {
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x |= x >> 32;
        return x;
    }

    public ulong NextBounded(ulong bound)
    {
        var mask = Smear(bound);
        ulong res;
        do
        {
            res = Next() & mask;
        }
        while (res >= bound);
        return res;
    }
}