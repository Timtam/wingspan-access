using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WingspanAccessInstaller;

internal sealed class AltchaException : Exception
{
    public AltchaException(string message) : base(message)
    {
    }
}

/// <summary>A classic ALTCHA proof-of-work challenge (the ALTCHA widget 2.x, which Static Forms uses).</summary>
internal sealed class AltchaChallenge
{
    public string Algorithm { get; set; }
    public string Challenge { get; set; }
    public string Salt { get; set; }
    public string Signature { get; set; }
    public long MaxNumber { get; set; }
}

internal sealed class AltchaSolution
{
    public long Number { get; set; }
    public long TookMs { get; set; }
}

/// <summary>
/// Solves ALTCHA challenges the way the ALTCHA widget does, so a desktop program can send a form
/// that is protected by it: find the number n (0 to maxnumber) for which the hex digest of
/// hash(salt + n) is the challenge, and send base64(JSON {algorithm, challenge, number, salt,
/// signature, took}). The newer key-derivation challenges ("parameters") are not supported; they
/// are recognized and refused with a clear message.
/// </summary>
internal static class Altcha
{
    /// <summary>The widget's default when a challenge names no maximum.</summary>
    public const long DefaultMaxNumber = 1_000_000;

    public static AltchaChallenge Parse(string json)
    {
        object parsed;
        try
        {
            parsed = new JavaScriptSerializer().DeserializeObject(json ?? string.Empty);
        }
        catch (ArgumentException e)
        {
            throw new AltchaException("The ALTCHA challenge is no JSON: " + e.Message);
        }

        if (parsed is not Dictionary<string, object> map)
            throw new AltchaException("The ALTCHA challenge is no JSON object.");
        if (map.ContainsKey("parameters"))
            throw new AltchaException("The ALTCHA challenge has the newer format (key derivation), which this version does not support.");

        string Text(string name) => map.TryGetValue(name, out object value) && value is string text ? text : null;
        var challenge = new AltchaChallenge
        {
            Algorithm = Text("algorithm") ?? "SHA-256",
            Challenge = Text("challenge"),
            Salt = Text("salt"),
            Signature = Text("signature"),
            MaxNumber = Number(map, "maxNumber") ?? Number(map, "maxnumber") ?? DefaultMaxNumber,
        };
        if (string.IsNullOrEmpty(challenge.Challenge) || challenge.Salt == null || challenge.Signature == null)
            throw new AltchaException("The ALTCHA challenge lacks challenge, salt or signature.");
        if (challenge.MaxNumber < 0)
            throw new AltchaException("The ALTCHA challenge has a negative maxnumber.");
        NewHash(challenge.Algorithm).Dispose();
        return challenge;
    }

    /// <summary>Solves the challenge on all processor cores. Throws AltchaException if no number fits.</summary>
    public static AltchaSolution Solve(AltchaChallenge challenge, CancellationToken token)
    {
        byte[] target = FromHex(challenge.Challenge);
        using (HashAlgorithm probe = NewHash(challenge.Algorithm))
        {
            if (target.Length != probe.HashSize / 8)
                throw new AltchaException($"The ALTCHA challenge is no {challenge.Algorithm} digest.");
        }

        byte[] salt = Encoding.UTF8.GetBytes(challenge.Salt);
        long max = challenge.MaxNumber;
        int threads = (int)Math.Max(1, Math.Min(Math.Min(Environment.ProcessorCount, 8), max / 50_000 + 1));
        long found = -1;
        Stopwatch watch = Stopwatch.StartNew();

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = token }, thread =>
        {
            using HashAlgorithm hash = NewHash(challenge.Algorithm);
            var buffer = new byte[salt.Length + 20];
            Buffer.BlockCopy(salt, 0, buffer, 0, salt.Length);
            for (long n = thread, i = 0; n <= max; n += threads, i++)
            {
                if ((i & 0x3FF) == 0 && (Interlocked.Read(ref found) >= 0 || token.IsCancellationRequested))
                    return;
                int length = salt.Length + WriteDecimal(n, buffer, salt.Length);
                if (Matches(hash.ComputeHash(buffer, 0, length), target))
                {
                    // The lowest number wins if two threads find one (only one can match in practice).
                    long seen;
                    while ((seen = Interlocked.Read(ref found)) < 0 || n < seen)
                    {
                        if (Interlocked.CompareExchange(ref found, n, seen) == seen)
                            break;
                    }

                    return;
                }
            }
        });

        token.ThrowIfCancellationRequested();
        if (found < 0)
            throw new AltchaException("No solution for the ALTCHA challenge was found.");
        return new AltchaSolution { Number = found, TookMs = watch.ElapsedMilliseconds };
    }

    /// <summary>What the form gets: base64 of the JSON the ALTCHA widget sends.</summary>
    public static string Payload(AltchaChallenge challenge, AltchaSolution solution)
    {
        string json = "{\"algorithm\":" + JsonString(challenge.Algorithm) +
                      ",\"challenge\":" + JsonString(challenge.Challenge) +
                      ",\"number\":" + solution.Number.ToString(CultureInfo.InvariantCulture) +
                      ",\"salt\":" + JsonString(challenge.Salt) +
                      ",\"signature\":" + JsonString(challenge.Signature) +
                      ",\"took\":" + solution.TookMs.ToString(CultureInfo.InvariantCulture) + "}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Parses, solves and returns the payload in one go.</summary>
    public static string SolveToPayload(string challengeJson, CancellationToken token)
    {
        AltchaChallenge challenge = Parse(challengeJson);
        return Payload(challenge, Solve(challenge, token));
    }

    /// <summary>The lowercase hex digest of hash(salt + number), as the server computes the challenge.</summary>
    public static string HashHex(string algorithm, string salt, long number)
    {
        using HashAlgorithm hash = NewHash(algorithm);
        byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(salt + number.ToString(CultureInfo.InvariantCulture)));
        var hex = new StringBuilder(digest.Length * 2);
        foreach (byte b in digest)
            hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    // The CNG classes work with the Windows FIPS policy on, where the managed ones throw.
    private static HashAlgorithm NewHash(string algorithm) => (algorithm ?? string.Empty).ToUpperInvariant() switch
    {
        "SHA-1" => new SHA1Cng(),
        "SHA-256" => new SHA256Cng(),
        "SHA-512" => new SHA512Cng(),
        _ => throw new AltchaException($"The ALTCHA challenge uses an unknown algorithm: {algorithm}"),
    };

    private static long? Number(Dictionary<string, object> map, string name)
    {
        if (!map.TryGetValue(name, out object value) || value == null)
            return null;
        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException)
        {
            throw new AltchaException($"The ALTCHA challenge has an invalid {name}: {value}");
        }
    }

    /// <summary>Writes n (not negative) in decimal digits at the offset; returns the digit count.</summary>
    private static int WriteDecimal(long n, byte[] buffer, int offset)
    {
        int digits = 1;
        for (long rest = n / 10; rest > 0; rest /= 10)
            digits++;
        for (int i = offset + digits - 1; i >= offset; i--)
        {
            buffer[i] = (byte)('0' + n % 10);
            n /= 10;
        }

        return digits;
    }

    private static bool Matches(byte[] digest, byte[] target)
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (digest[i] != target[i])
                return false;
        }

        return true;
    }

    private static byte[] FromHex(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new AltchaException("The ALTCHA challenge is no hex digest.");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                throw new AltchaException("The ALTCHA challenge is no hex digest.");
        }

        return bytes;
    }

    internal static string JsonString(string text)
    {
        var json = new StringBuilder("\"");
        foreach (char c in text ?? string.Empty)
        {
            switch (c)
            {
                case '"': json.Append("\\\""); break;
                case '\\': json.Append("\\\\"); break;
                case '\n': json.Append("\\n"); break;
                case '\r': json.Append("\\r"); break;
                case '\t': json.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        json.Append(c);
                    break;
            }
        }

        return json.Append('"').ToString();
    }
}
