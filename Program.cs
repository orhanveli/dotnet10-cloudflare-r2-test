using Amazon.S3;
using Amazon.S3.Model;

// Load variables from a .env file (if present) into the environment.
// Real environment variables take precedence and are never overwritten.
LoadDotEnv(".env");

// --- Configuration (read from environment variables) ---
// R2_ACCOUNT_ID    : your Cloudflare account ID
// R2_ACCESS_KEY    : R2 API token Access Key ID
// R2_SECRET_KEY    : R2 API token Secret Access Key
// R2_BUCKET        : the bucket name you created
// R2_JURISDICTION  : optional bucket jurisdiction — "eu" or "fedramp".
//                    Leave unset for the default (non-jurisdictional) region.
string accountId    = GetRequired("R2_ACCOUNT_ID");
string accessKey    = GetRequired("R2_ACCESS_KEY");
string secretKey    = GetRequired("R2_SECRET_KEY");
string bucket       = GetRequired("R2_BUCKET");
string jurisdiction = Environment.GetEnvironmentVariable("R2_JURISDICTION") ?? "eu";

// File to upload: first CLI arg, or default to test-doc.txt next to the project.
string filePath = args.Length > 0 ? args[0] : "test-doc.txt";
if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {Path.GetFullPath(filePath)}");
    return 1;
}

// Object key in the bucket: second CLI arg, or the file name.
string key = args.Length > 1 ? args[1] : Path.GetFileName(filePath);

// R2's S3-compatible endpoint. Region must be "auto" for R2.
// Jurisdictional buckets (e.g. EU) use a host segment: <account>.eu.r2...
string host = string.IsNullOrWhiteSpace(jurisdiction)
    ? $"{accountId}.r2.cloudflarestorage.com"
    : $"{accountId}.{jurisdiction}.r2.cloudflarestorage.com";

var config = new AmazonS3Config
{
    ServiceURL = $"https://{host}",
    AuthenticationRegion = "auto",
    // R2 requires path-style or virtual-hosted; SDK handles this fine by default,
    // but forcing path style avoids DNS issues with custom bucket names.
    ForcePathStyle = true,
    // R2 does not implement the SDK's default streaming checksum trailer
    // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER). Only send checksums when
    // explicitly required so uploads work against R2.
    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
    ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED,
};

using var client = new AmazonS3Client(accessKey, secretKey, config);

Console.WriteLine($"Uploading {Path.GetFullPath(filePath)} -> r2://{bucket}/{key}");

try
{
    var request = new PutObjectRequest
    {
        BucketName = bucket,
        Key = key,
        FilePath = filePath,
        // R2 does not support the chunked streaming signature
        // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD); send a single signed payload.
        UseChunkEncoding = false,
    };
    await client.PutObjectAsync(request);

    Console.WriteLine("Upload complete.");

    // Confirm by reading back the object's metadata.
    var meta = await client.GetObjectMetadataAsync(bucket, key);
    Console.WriteLine($"Verified: {key} ({meta.ContentLength} bytes, ETag {meta.ETag})");
    return 0;
}
catch (AmazonS3Exception ex)
{
    Console.Error.WriteLine($"R2 error: {ex.StatusCode} - {ex.Message}");
    return 1;
}

// Minimal .env parser: KEY=VALUE per line. Supports comments (#), blank lines,
// optional "export " prefix, and single/double quoted values. Existing
// environment variables are not overwritten.
static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
        return;

    foreach (string raw in File.ReadAllLines(path))
    {
        string line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            continue;

        if (line.StartsWith("export ", StringComparison.Ordinal))
            line = line["export ".Length..].TrimStart();

        int eq = line.IndexOf('=');
        if (eq <= 0)
            continue;

        string keyName = line[..eq].Trim();
        string value = line[(eq + 1)..].Trim();

        // Strip a single matching pair of surrounding quotes.
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        // Don't clobber a value already set in the real environment.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(keyName)))
            Environment.SetEnvironmentVariable(keyName, value);
    }
}

static string GetRequired(string name)
{
    string? value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        Console.Error.WriteLine($"Missing required environment variable: {name}");
        Environment.Exit(1);
    }
    return value!;
}
