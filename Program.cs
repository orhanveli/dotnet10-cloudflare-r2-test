using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

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
};

using var client = new AmazonS3Client(accessKey, secretKey, config);

Console.WriteLine($"Uploading {Path.GetFullPath(filePath)} -> r2://{bucket}/{key}");

try
{
    var transfer = new TransferUtility(client);
    await transfer.UploadAsync(filePath, bucket, key);

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
