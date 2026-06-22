# Cloudflare R2 Uploader

A simple .NET 10 console app that uploads a file to a Cloudflare R2 bucket
using the S3-compatible API (`AWSSDK.S3`).

## Configuration

The app reads credentials from environment variables so nothing secret is
committed to source:

| Variable          | Description                                  |
| ----------------- | -------------------------------------------- |
| `R2_ACCOUNT_ID`   | Your Cloudflare account ID                   |
| `R2_ACCESS_KEY`   | R2 API token **Access Key ID**               |
| `R2_SECRET_KEY`   | R2 API token **Secret Access Key**           |
| `R2_BUCKET`       | The bucket name you created                  |
| `R2_JURISDICTION` | Optional: `eu` or `fedramp`; empty = default |

> Find your Account ID and create/copy API token keys in the Cloudflare
> dashboard under **R2 → Manage R2 API Tokens**.

## Run

```bash
export R2_ACCOUNT_ID="your-account-id"
export R2_ACCESS_KEY="your-access-key-id"
export R2_SECRET_KEY="your-secret-access-key"
export R2_BUCKET="your-bucket-name"

# Uploads test-doc.txt by default
dotnet run

# Or upload a specific file (and optionally set the object key):
dotnet run -- path/to/file.ext custom-object-key
```

On success you'll see the upload confirmation and a verification line with the
object's size and ETag read back from R2.
