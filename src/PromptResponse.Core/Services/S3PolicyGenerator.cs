using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Services;

/// <summary>
/// Generates S3 pre-signed POST policies for direct browser uploads.
/// </summary>
public class S3PolicyGenerator
{
    /// <summary>
    /// Configuration for S3 bucket setup.
    /// </summary>
    public class S3Config
    {
        /// <summary>
        /// S3 bucket name.
        /// </summary>
        public required string BucketName { get; set; }

        /// <summary>
        /// AWS region (e.g., "us-east-1").
        /// </summary>
        public required string Region { get; set; }

        /// <summary>
        /// Object key prefix for uploads (e.g., "submissions/").
        /// </summary>
        public string KeyPrefix { get; set; } = "";

        /// <summary>
        /// AWS Access Key ID.
        /// </summary>
        public required string AccessKeyId { get; set; }

        /// <summary>
        /// AWS Secret Access Key.
        /// </summary>
        public required string SecretAccessKey { get; set; }

        /// <summary>
        /// Policy expiration duration.
        /// </summary>
        public TimeSpan Expiration { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Maximum file size in bytes (default 10MB).
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// Custom endpoint URL (for MinIO or other S3-compatible services).
        /// If null, uses standard AWS S3 endpoint.
        /// </summary>
        public string? CustomEndpoint { get; set; }

        /// <summary>
        /// Whether to use path-style URLs (bucket in path instead of subdomain).
        /// Required for MinIO and some S3-compatible services.
        /// </summary>
        public bool UsePathStyle { get; set; }
    }

    /// <summary>
    /// Generates S3 pre-signed POST policy and submission config.
    /// </summary>
    /// <param name="config">S3 configuration.</param>
    /// <returns>SubmissionConfig ready to embed in a template.</returns>
    public SubmissionConfig GenerateSubmissionConfig(S3Config config)
    {
        var expirationDate = DateTime.UtcNow.Add(config.Expiration);
        var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var credential = $"{config.AccessKeyId}/{dateStamp}/{config.Region}/s3/aws4_request";

        // Build policy document
        var policy = new
        {
            expiration = expirationDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            conditions = new object[]
            {
                new { bucket = config.BucketName },
                new[] { "starts-with", "$key", config.KeyPrefix },
                new { acl = "private" },
                new[] { "starts-with", "$Content-Type", "application/json" },
                new object[] { "content-length-range", 0, config.MaxFileSizeBytes },
                new { x_amz_algorithm = "AWS4-HMAC-SHA256" },
                new { x_amz_credential = credential },
                new { x_amz_date = amzDate }
            }
        };

        var policyJson = JsonSerializer.Serialize(policy);
        var policyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(policyJson));

        // Calculate signature
        var signature = CalculateSignature(
            config.SecretAccessKey,
            dateStamp,
            config.Region,
            policyBase64);

        // Build endpoint URL
        string endpointUrl;
        if (!string.IsNullOrEmpty(config.CustomEndpoint))
        {
            endpointUrl = config.UsePathStyle
                ? $"{config.CustomEndpoint.TrimEnd('/')}/{config.BucketName}/"
                : $"{config.CustomEndpoint.TrimEnd('/')}/";
        }
        else
        {
            endpointUrl = $"https://{config.BucketName}.s3.{config.Region}.amazonaws.com/";
        }

        return new SubmissionConfig
        {
            Type = "s3-presigned-post",
            Url = endpointUrl,
            ExpiresAt = expirationDate,
            Fields = new Dictionary<string, string>
            {
                ["key"] = config.KeyPrefix + "${filename}",
                ["acl"] = "private",
                ["Content-Type"] = "application/json",
                ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
                ["X-Amz-Credential"] = credential,
                ["X-Amz-Date"] = amzDate,
                ["Policy"] = policyBase64,
                ["X-Amz-Signature"] = signature
            }
        };
    }

    /// <summary>
    /// Generates CORS configuration for an S3 bucket.
    /// </summary>
    /// <param name="allowedOrigins">List of allowed origins (use "*" for any).</param>
    /// <returns>CORS configuration JSON.</returns>
    public string GenerateCorsConfiguration(IEnumerable<string>? allowedOrigins = null)
    {
        var origins = allowedOrigins?.ToList() ?? new List<string> { "*" };

        var corsConfig = new
        {
            CORSRules = new[]
            {
                new
                {
                    AllowedOrigins = origins,
                    AllowedMethods = new[] { "POST", "GET", "PUT" },
                    AllowedHeaders = new[] { "*" },
                    ExposeHeaders = new[] { "ETag" },
                    MaxAgeSeconds = 3000
                }
            }
        };

        return JsonSerializer.Serialize(corsConfig, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Generates bucket policy for public POST uploads.
    /// </summary>
    /// <param name="bucketName">S3 bucket name.</param>
    /// <param name="keyPrefix">Optional key prefix to restrict uploads.</param>
    /// <returns>Bucket policy JSON.</returns>
    public string GenerateBucketPolicy(string bucketName, string? keyPrefix = null)
    {
        var resource = string.IsNullOrEmpty(keyPrefix)
            ? $"arn:aws:s3:::{bucketName}/*"
            : $"arn:aws:s3:::{bucketName}/{keyPrefix}*";

        var policy = new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Sid = "AllowPresignedPostUploads",
                    Effect = "Allow",
                    Principal = "*",
                    Action = new[] { "s3:PutObject" },
                    Resource = resource,
                    Condition = new
                    {
                        StringEquals = new Dictionary<string, string>
                        {
                            ["s3:x-amz-acl"] = "private"
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CalculateSignature(
        string secretKey,
        string dateStamp,
        string region,
        string policyBase64)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, "s3");
        var kSigning = HmacSha256(kService, "aws4_request");
        var signature = HmacSha256(kSigning, policyBase64);

        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
}
