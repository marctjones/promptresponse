using System.Text.Json;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Services;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Configures S3 bucket for form submissions and generates pre-signed POST policies.
/// </summary>
public class S3SetupCommand : ICommand
{
    private readonly IAprSerializer _serializer;
    private readonly S3PolicyGenerator _policyGenerator;

    public S3SetupCommand(IAprSerializer serializer, S3PolicyGenerator policyGenerator)
    {
        _serializer = serializer;
        _policyGenerator = policyGenerator;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        var options = ParseOptions(args);

        if (options.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        if (options.CorsOnly)
        {
            return ShowCorsConfig(options);
        }

        if (options.PolicyOnly)
        {
            return ShowBucketPolicy(options);
        }

        // Refresh mode - update existing templates with new expiration
        if (options.Refresh)
        {
            return await RefreshTemplates(options);
        }

        // Validate required options
        if (string.IsNullOrEmpty(options.Bucket))
        {
            Console.Error.WriteLine("Error: --bucket is required");
            Console.Error.WriteLine("Run 'apr s3-setup --help' for usage information.");
            return 1;
        }

        // Get credentials from environment or options
        var accessKeyId = options.AccessKeyId ?? Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretAccessKey = options.SecretAccessKey ?? Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
        {
            Console.Error.WriteLine("Error: AWS credentials required");
            Console.Error.WriteLine("Set AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables,");
            Console.Error.WriteLine("or use --access-key-id and --secret-access-key options.");
            return 1;
        }

        try
        {
            var config = new S3PolicyGenerator.S3Config
            {
                BucketName = options.Bucket,
                Region = options.Region ?? "us-east-1",
                KeyPrefix = options.Prefix ?? "",
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                Expiration = ParseExpiration(options.Expires),
                CustomEndpoint = options.Endpoint,
                UsePathStyle = options.PathStyle
            };

            var submissionConfig = _policyGenerator.GenerateSubmissionConfig(config);

            // Output mode
            if (options.JsonOutput)
            {
                var json = JsonSerializer.Serialize(submissionConfig, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                return 0;
            }

            // Embed in template
            if (!string.IsNullOrEmpty(options.Template))
            {
                return await EmbedInTemplate(options.Template, submissionConfig, options.Output);
            }

            // Default: show config
            Console.WriteLine("S3 Pre-signed POST Configuration");
            Console.WriteLine("=================================");
            Console.WriteLine();
            Console.WriteLine($"Endpoint URL: {submissionConfig.Url}");
            Console.WriteLine($"Expires: {submissionConfig.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();
            Console.WriteLine("Fields:");
            foreach (var (key, value) in submissionConfig.Fields!)
            {
                var displayValue = key == "Policy" || key == "X-Amz-Signature"
                    ? value[..Math.Min(40, value.Length)] + "..."
                    : value;
                Console.WriteLine($"  {key}: {displayValue}");
            }
            Console.WriteLine();
            Console.WriteLine("To embed in a template, use: apr s3-setup --bucket=... --template=myform.aprt");
            Console.WriteLine("To output as JSON, use: apr s3-setup --bucket=... --json");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> EmbedInTemplate(string templatePath, SubmissionConfig submissionConfig, string? outputPath)
    {
        if (!File.Exists(templatePath))
        {
            Console.Error.WriteLine($"Error: Template file not found: {templatePath}");
            return 1;
        }

        try
        {
            var json = await File.ReadAllTextAsync(templatePath);
            var document = _serializer.Deserialize(json);

            document.Metadata.SubmissionConfig = submissionConfig;
            document.Metadata.Modified = DateTime.UtcNow;

            var outputJson = _serializer.Serialize(document);
            var finalPath = outputPath ?? templatePath;

            await File.WriteAllTextAsync(finalPath, outputJson);

            Console.WriteLine($"S3 submission config embedded in: {finalPath}");
            Console.WriteLine($"Config expires: {submissionConfig.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();
            Console.WriteLine("Users can now submit filled forms directly to S3 from the desktop app.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating template: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Refreshes S3 submission config in templates by reading existing config and regenerating with new expiration.
    /// </summary>
    private async Task<int> RefreshTemplates(Options options)
    {
        // Get credentials from environment or options
        var accessKeyId = options.AccessKeyId ?? Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretAccessKey = options.SecretAccessKey ?? Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
        {
            Console.Error.WriteLine("Error: AWS credentials required for refresh");
            Console.Error.WriteLine("Set AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables,");
            Console.Error.WriteLine("or use --access-key-id and --secret-access-key options.");
            return 1;
        }

        // Get files to refresh
        var files = GetFilesToRefresh(options);
        if (files.Count == 0)
        {
            Console.Error.WriteLine("Error: No template files found to refresh");
            Console.Error.WriteLine("Specify files with --template=<file> or --template=<directory>");
            return 1;
        }

        var expiration = ParseExpiration(options.Expires);
        var refreshed = 0;
        var skipped = 0;
        var errors = 0;

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var document = _serializer.Deserialize(json);

                var existingConfig = document.Metadata.SubmissionConfig;
                if (existingConfig == null || existingConfig.Type != "s3-presigned-post")
                {
                    if (!options.Quiet)
                        Console.WriteLine($"Skipped (no S3 config): {file}");
                    skipped++;
                    continue;
                }

                // Extract bucket and region from existing config
                var (bucket, region, endpoint, pathStyle, prefix) = ParseExistingConfig(existingConfig);

                if (string.IsNullOrEmpty(bucket))
                {
                    if (!options.Quiet)
                        Console.WriteLine($"Skipped (could not parse bucket): {file}");
                    skipped++;
                    continue;
                }

                // Generate new config
                var config = new S3PolicyGenerator.S3Config
                {
                    BucketName = bucket,
                    Region = options.Region ?? region ?? "us-east-1",
                    KeyPrefix = options.Prefix ?? prefix ?? "",
                    AccessKeyId = accessKeyId,
                    SecretAccessKey = secretAccessKey,
                    Expiration = expiration,
                    CustomEndpoint = options.Endpoint ?? endpoint,
                    UsePathStyle = options.PathStyle || pathStyle
                };

                var newConfig = _policyGenerator.GenerateSubmissionConfig(config);
                document.Metadata.SubmissionConfig = newConfig;
                document.Metadata.Modified = DateTime.UtcNow;

                var outputJson = _serializer.Serialize(document);
                await File.WriteAllTextAsync(file, outputJson);

                if (!options.Quiet)
                    Console.WriteLine($"Refreshed: {file} (expires {newConfig.ExpiresAt:yyyy-MM-dd HH:mm} UTC)");
                refreshed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing {file}: {ex.Message}");
                errors++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Summary: {refreshed} refreshed, {skipped} skipped, {errors} errors");

        return errors > 0 ? 1 : 0;
    }

    private List<string> GetFilesToRefresh(Options options)
    {
        var files = new List<string>();

        if (string.IsNullOrEmpty(options.Template))
            return files;

        if (Directory.Exists(options.Template))
        {
            // Refresh all .aprt files in directory
            files.AddRange(Directory.GetFiles(options.Template, "*.aprt", SearchOption.TopDirectoryOnly));
        }
        else if (File.Exists(options.Template))
        {
            files.Add(options.Template);
        }
        else if (options.Template.Contains('*'))
        {
            // Glob pattern
            var dir = Path.GetDirectoryName(options.Template) ?? ".";
            var pattern = Path.GetFileName(options.Template);
            if (Directory.Exists(dir))
            {
                files.AddRange(Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly));
            }
        }

        return files;
    }

    private static (string? bucket, string? region, string? endpoint, bool pathStyle, string? prefix) ParseExistingConfig(SubmissionConfig config)
    {
        string? bucket = null;
        string? region = null;
        string? endpoint = null;
        bool pathStyle = false;
        string? prefix = null;

        // Parse URL to extract bucket and region
        // Standard AWS: https://bucket.s3.region.amazonaws.com/
        // Path-style: https://s3.region.amazonaws.com/bucket/ or http://localhost:9000/bucket/
        var url = config.Url;
        if (!string.IsNullOrEmpty(url))
        {
            var uri = new Uri(url);

            // Check for path-style (bucket in path)
            if (uri.AbsolutePath.Length > 1)
            {
                var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                if (pathParts.Length > 0 && !string.IsNullOrEmpty(pathParts[0]))
                {
                    bucket = pathParts[0];
                    pathStyle = true;
                    endpoint = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
                }
            }

            // Check for virtual-hosted style (bucket in subdomain)
            if (bucket == null && uri.Host.Contains(".s3."))
            {
                var hostParts = uri.Host.Split('.');
                bucket = hostParts[0];
                // Extract region from s3.region.amazonaws.com
                var regionIdx = Array.IndexOf(hostParts, "s3");
                if (regionIdx >= 0 && regionIdx + 1 < hostParts.Length)
                {
                    region = hostParts[regionIdx + 1];
                    if (region == "amazonaws") region = "us-east-1"; // Default region
                }
            }

            // Non-standard endpoint (MinIO, etc.)
            if (!uri.Host.Contains("amazonaws.com"))
            {
                endpoint = $"{uri.Scheme}://{uri.Host}";
                if (!uri.IsDefaultPort)
                    endpoint += $":{uri.Port}";
            }
        }

        // Extract prefix from key field
        if (config.Fields?.TryGetValue("key", out var keyTemplate) == true)
        {
            // key might be "submissions/${filename}" - extract prefix
            var idx = keyTemplate.IndexOf("${");
            if (idx > 0)
            {
                prefix = keyTemplate[..idx];
            }
        }

        // Try to extract region from credential
        if (region == null && config.Fields?.TryGetValue("X-Amz-Credential", out var credential) == true)
        {
            // Format: accesskey/date/region/s3/aws4_request
            var parts = credential.Split('/');
            if (parts.Length >= 3)
            {
                region = parts[2];
            }
        }

        return (bucket, region, endpoint, pathStyle, prefix);
    }

    private int ShowCorsConfig(Options options)
    {
        var origins = options.AllowedOrigins?.Split(',').Select(o => o.Trim()).ToList();
        var corsJson = _policyGenerator.GenerateCorsConfiguration(origins);

        Console.WriteLine("CORS Configuration for S3 Bucket");
        Console.WriteLine("================================");
        Console.WriteLine();
        Console.WriteLine(corsJson);
        Console.WriteLine();
        Console.WriteLine("Apply with AWS CLI:");
        Console.WriteLine($"  aws s3api put-bucket-cors --bucket {options.Bucket ?? "YOUR_BUCKET"} --cors-configuration file://cors.json");
        Console.WriteLine();
        Console.WriteLine("Or for MinIO:");
        Console.WriteLine($"  mc admin config set myminio cors '{corsJson}'");

        return 0;
    }

    private int ShowBucketPolicy(Options options)
    {
        if (string.IsNullOrEmpty(options.Bucket))
        {
            Console.Error.WriteLine("Error: --bucket is required for bucket policy");
            return 1;
        }

        var policyJson = _policyGenerator.GenerateBucketPolicy(options.Bucket, options.Prefix);

        Console.WriteLine("Bucket Policy for S3");
        Console.WriteLine("====================");
        Console.WriteLine();
        Console.WriteLine(policyJson);
        Console.WriteLine();
        Console.WriteLine("Apply with AWS CLI:");
        Console.WriteLine($"  aws s3api put-bucket-policy --bucket {options.Bucket} --policy file://policy.json");

        return 0;
    }

    private static TimeSpan ParseExpiration(string? expires)
    {
        if (string.IsNullOrEmpty(expires))
            return TimeSpan.FromDays(7);

        var value = expires.TrimEnd('d', 'h', 'm', 'D', 'H', 'M');
        if (!int.TryParse(value, out var num))
            return TimeSpan.FromDays(7);

        var unit = expires.ToLowerInvariant().Last();
        return unit switch
        {
            'd' => TimeSpan.FromDays(num),
            'h' => TimeSpan.FromHours(num),
            'm' => TimeSpan.FromMinutes(num),
            _ => TimeSpan.FromDays(num)
        };
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

        foreach (var arg in args)
        {
            if (arg == "--help" || arg == "-h")
                options.ShowHelp = true;
            else if (arg == "--json")
                options.JsonOutput = true;
            else if (arg == "--cors-only")
                options.CorsOnly = true;
            else if (arg == "--policy-only")
                options.PolicyOnly = true;
            else if (arg == "--path-style")
                options.PathStyle = true;
            else if (arg == "--refresh")
                options.Refresh = true;
            else if (arg == "--quiet" || arg == "-q")
                options.Quiet = true;
            else if (arg.StartsWith("--bucket="))
                options.Bucket = arg["--bucket=".Length..];
            else if (arg.StartsWith("--region="))
                options.Region = arg["--region=".Length..];
            else if (arg.StartsWith("--prefix="))
                options.Prefix = arg["--prefix=".Length..];
            else if (arg.StartsWith("--expires="))
                options.Expires = arg["--expires=".Length..];
            else if (arg.StartsWith("--template="))
                options.Template = arg["--template=".Length..];
            else if (arg.StartsWith("--output="))
                options.Output = arg["--output=".Length..];
            else if (arg.StartsWith("--endpoint="))
                options.Endpoint = arg["--endpoint=".Length..];
            else if (arg.StartsWith("--access-key-id="))
                options.AccessKeyId = arg["--access-key-id=".Length..];
            else if (arg.StartsWith("--secret-access-key="))
                options.SecretAccessKey = arg["--secret-access-key=".Length..];
            else if (arg.StartsWith("--allowed-origins="))
                options.AllowedOrigins = arg["--allowed-origins=".Length..];
        }

        return options;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("apr s3-setup - Configure S3 bucket for form submissions");
        Console.WriteLine();
        Console.WriteLine("Usage: apr s3-setup [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --bucket=<name>           S3 bucket name (required for most operations)");
        Console.WriteLine("  --region=<region>         AWS region (default: us-east-1)");
        Console.WriteLine("  --prefix=<path>           Object key prefix (e.g., 'submissions/')");
        Console.WriteLine("  --expires=<duration>      Policy expiration (e.g., '7d', '24h', '30m')");
        Console.WriteLine("  --template=<file>         Embed config in APR template file");
        Console.WriteLine("  --output=<file>           Output file (when using --template)");
        Console.WriteLine("  --json                    Output config as JSON");
        Console.WriteLine();
        Console.WriteLine("Credential Options:");
        Console.WriteLine("  --access-key-id=<key>     AWS Access Key ID (or set AWS_ACCESS_KEY_ID)");
        Console.WriteLine("  --secret-access-key=<key> AWS Secret Key (or set AWS_SECRET_ACCESS_KEY)");
        Console.WriteLine();
        Console.WriteLine("S3-Compatible Services (MinIO, etc.):");
        Console.WriteLine("  --endpoint=<url>          Custom S3 endpoint URL");
        Console.WriteLine("  --path-style              Use path-style URLs (required for MinIO)");
        Console.WriteLine();
        Console.WriteLine("Refresh Mode (for cron jobs):");
        Console.WriteLine("  --refresh                 Refresh existing templates with new expiration");
        Console.WriteLine("  --quiet, -q               Suppress per-file output (show summary only)");
        Console.WriteLine();
        Console.WriteLine("Helper Commands:");
        Console.WriteLine("  --cors-only               Output CORS configuration only");
        Console.WriteLine("  --policy-only             Output bucket policy only");
        Console.WriteLine("  --allowed-origins=<list>  Comma-separated allowed origins for CORS");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Generate config and embed in template");
        Console.WriteLine("  apr s3-setup --bucket=my-forms --template=myform.aprt --expires=30d");
        Console.WriteLine();
        Console.WriteLine("  # Output config as JSON");
        Console.WriteLine("  apr s3-setup --bucket=my-forms --region=us-west-2 --json");
        Console.WriteLine();
        Console.WriteLine("  # Configure for local MinIO testing");
        Console.WriteLine("  apr s3-setup --bucket=apr-test \\");
        Console.WriteLine("    --endpoint=http://localhost:9000 --path-style \\");
        Console.WriteLine("    --access-key-id=minioadmin --secret-access-key=minioadmin");
        Console.WriteLine();
        Console.WriteLine("  # Generate CORS configuration");
        Console.WriteLine("  apr s3-setup --cors-only --bucket=my-forms");
        Console.WriteLine();
        Console.WriteLine("  # Generate bucket policy");
        Console.WriteLine("  apr s3-setup --policy-only --bucket=my-forms --prefix=submissions/");
        Console.WriteLine();
        Console.WriteLine("  # Refresh a single template (extend expiration by 30 days)");
        Console.WriteLine("  apr s3-setup --refresh --template=myform.aprt --expires=30d");
        Console.WriteLine();
        Console.WriteLine("  # Refresh all templates in a directory (for cron)");
        Console.WriteLine("  apr s3-setup --refresh --template=/path/to/templates --expires=30d --quiet");
        Console.WriteLine();
        Console.WriteLine("Cron Example (refresh nightly):");
        Console.WriteLine("  0 2 * * * AWS_ACCESS_KEY_ID=xxx AWS_SECRET_ACCESS_KEY=xxx \\");
        Console.WriteLine("    apr s3-setup --refresh --template=/var/forms --expires=30d --quiet");
    }

    private class Options
    {
        public bool ShowHelp { get; set; }
        public bool JsonOutput { get; set; }
        public bool CorsOnly { get; set; }
        public bool PolicyOnly { get; set; }
        public bool PathStyle { get; set; }
        public bool Refresh { get; set; }
        public bool Quiet { get; set; }
        public string? Bucket { get; set; }
        public string? Region { get; set; }
        public string? Prefix { get; set; }
        public string? Expires { get; set; }
        public string? Template { get; set; }
        public string? Output { get; set; }
        public string? Endpoint { get; set; }
        public string? AccessKeyId { get; set; }
        public string? SecretAccessKey { get; set; }
        public string? AllowedOrigins { get; set; }
    }
}
