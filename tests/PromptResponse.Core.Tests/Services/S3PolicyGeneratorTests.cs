using System.Text;
using System.Text.Json;
using FluentAssertions;
using PromptResponse.Core.Services;
using Xunit;

namespace PromptResponse.Core.Tests.Services;

/// <summary>
/// Unit tests for S3PolicyGenerator.
/// Tests pre-signed POST policy generation for S3 uploads.
/// </summary>
public class S3PolicyGeneratorTests
{
    private readonly S3PolicyGenerator _generator;

    public S3PolicyGeneratorTests()
    {
        _generator = new S3PolicyGenerator();
    }

    #region GenerateSubmissionConfig Tests

    [Fact]
    public void GenerateSubmissionConfig_ReturnsCorrectType()
    {
        // Arrange
        var config = CreateBasicConfig();

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Type.Should().Be("s3-presigned-post");
    }

    [Fact]
    public void GenerateSubmissionConfig_SetsCorrectAwsEndpoint()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.BucketName = "my-bucket";
        config.Region = "us-west-2";

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Url.Should().Be("https://my-bucket.s3.us-west-2.amazonaws.com/");
    }

    [Fact]
    public void GenerateSubmissionConfig_WithCustomEndpoint_SetsCorrectUrl()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.CustomEndpoint = "http://localhost:9000";
        config.UsePathStyle = false;

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Url.Should().Be("http://localhost:9000/");
    }

    [Fact]
    public void GenerateSubmissionConfig_WithPathStyle_IncludesBucketInPath()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.CustomEndpoint = "http://localhost:9000";
        config.UsePathStyle = true;
        config.BucketName = "my-bucket";

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Url.Should().Be("http://localhost:9000/my-bucket/");
    }

    [Fact]
    public void GenerateSubmissionConfig_SetsExpirationDate()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.Expiration = TimeSpan.FromDays(7);
        var beforeTest = DateTime.UtcNow;

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.ExpiresAt.Should().BeOnOrAfter(beforeTest.AddDays(7).AddMinutes(-1));
        result.ExpiresAt.Should().BeOnOrBefore(beforeTest.AddDays(7).AddMinutes(1));
    }

    [Fact]
    public void GenerateSubmissionConfig_IncludesKeyFieldWithPrefix()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.KeyPrefix = "submissions/forms/";

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Fields.Should().ContainKey("key");
        result.Fields!["key"].Should().Be("submissions/forms/${filename}");
    }

    [Fact]
    public void GenerateSubmissionConfig_IncludesAclField()
    {
        // Arrange
        var config = CreateBasicConfig();

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Fields.Should().ContainKey("acl");
        result.Fields!["acl"].Should().Be("private");
    }

    [Fact]
    public void GenerateSubmissionConfig_IncludesContentTypeField()
    {
        // Arrange
        var config = CreateBasicConfig();

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Fields.Should().ContainKey("Content-Type");
        result.Fields!["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void GenerateSubmissionConfig_IncludesAwsFields()
    {
        // Arrange
        var config = CreateBasicConfig();

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Fields.Should().ContainKey("X-Amz-Algorithm");
        result.Fields!["X-Amz-Algorithm"].Should().Be("AWS4-HMAC-SHA256");

        result.Fields.Should().ContainKey("X-Amz-Credential");
        result.Fields["X-Amz-Credential"].Should().Contain(config.AccessKeyId);
        result.Fields["X-Amz-Credential"].Should().Contain(config.Region);
        result.Fields["X-Amz-Credential"].Should().Contain("s3/aws4_request");

        result.Fields.Should().ContainKey("X-Amz-Date");
        result.Fields["X-Amz-Date"].Should().MatchRegex(@"^\d{8}T\d{6}Z$");
    }

    [Fact]
    public void GenerateSubmissionConfig_IncludesPolicy()
    {
        // Arrange
        var config = CreateBasicConfig();

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Fields.Should().ContainKey("Policy");
        var policyBase64 = result.Fields!["Policy"];
        policyBase64.Should().NotBeNullOrEmpty();

        // Verify it's valid base64 and contains expected JSON
        var policyJson = Encoding.UTF8.GetString(Convert.FromBase64String(policyBase64));
        var policy = JsonDocument.Parse(policyJson);

        policy.RootElement.TryGetProperty("expiration", out _).Should().BeTrue();
        policy.RootElement.TryGetProperty("conditions", out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateSubmissionConfig_IncludesSignature()
    {
        // Arrange
        var config = CreateBasicConfig();

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Fields.Should().ContainKey("X-Amz-Signature");
        var signature = result.Fields!["X-Amz-Signature"];
        signature.Should().NotBeNullOrEmpty();
        signature.Should().MatchRegex("^[a-f0-9]{64}$"); // 64 hex characters for SHA256
    }

    [Fact]
    public void GenerateSubmissionConfig_DifferentSecrets_ProduceDifferentSignatures()
    {
        // Arrange
        var config1 = CreateBasicConfig();
        config1.SecretAccessKey = "secret1";

        var config2 = CreateBasicConfig();
        config2.SecretAccessKey = "secret2";

        // Act
        var result1 = _generator.GenerateSubmissionConfig(config1);
        var result2 = _generator.GenerateSubmissionConfig(config2);

        // Assert
        result1.Fields!["X-Amz-Signature"].Should().NotBe(result2.Fields!["X-Amz-Signature"]);
    }

    [Theory]
    [InlineData("us-east-1")]
    [InlineData("us-west-2")]
    [InlineData("eu-west-1")]
    [InlineData("ap-southeast-1")]
    public void GenerateSubmissionConfig_SupportsVariousRegions(string region)
    {
        // Arrange
        var config = CreateBasicConfig();
        config.Region = region;

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Url.Should().Contain($".s3.{region}.amazonaws.com");
        result.Fields!["X-Amz-Credential"].Should().Contain(region);
    }

    [Fact]
    public void GenerateSubmissionConfig_CustomEndpoint_TrimsTrailingSlash()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.CustomEndpoint = "http://localhost:9000/";
        config.UsePathStyle = false;

        // Act
        var result = _generator.GenerateSubmissionConfig(config);

        // Assert
        result.Url.Should().Be("http://localhost:9000/");
        // Extract path portion after host to check for double slashes
        var pathPortion = result.Url.Substring(result.Url.IndexOf("://") + 3);
        pathPortion.Should().NotContain("//", "path should not have double slashes");
    }

    #endregion

    #region GenerateCorsConfiguration Tests

    [Fact]
    public void GenerateCorsConfiguration_DefaultOrigins_UsesWildcard()
    {
        // Act
        var corsJson = _generator.GenerateCorsConfiguration();

        // Assert
        corsJson.Should().Contain("\"*\"");
    }

    [Fact]
    public void GenerateCorsConfiguration_WithCustomOrigins_IncludesAllOrigins()
    {
        // Arrange
        var origins = new[] { "https://example.com", "https://app.example.com" };

        // Act
        var corsJson = _generator.GenerateCorsConfiguration(origins);

        // Assert
        corsJson.Should().Contain("https://example.com");
        corsJson.Should().Contain("https://app.example.com");
    }

    [Fact]
    public void GenerateCorsConfiguration_IncludesRequiredMethods()
    {
        // Act
        var corsJson = _generator.GenerateCorsConfiguration();

        // Assert
        corsJson.Should().Contain("POST");
        corsJson.Should().Contain("GET");
        corsJson.Should().Contain("PUT");
    }

    [Fact]
    public void GenerateCorsConfiguration_IsValidJson()
    {
        // Act
        var corsJson = _generator.GenerateCorsConfiguration();

        // Assert
        var act = () => JsonDocument.Parse(corsJson);
        act.Should().NotThrow();
    }

    #endregion

    #region GenerateBucketPolicy Tests

    [Fact]
    public void GenerateBucketPolicy_IncludesBucketInResource()
    {
        // Arrange
        var bucketName = "my-test-bucket";

        // Act
        var policyJson = _generator.GenerateBucketPolicy(bucketName);

        // Assert
        policyJson.Should().Contain($"arn:aws:s3:::{bucketName}/*");
    }

    [Fact]
    public void GenerateBucketPolicy_WithKeyPrefix_RestrictsToPrefix()
    {
        // Arrange
        var bucketName = "my-test-bucket";
        var keyPrefix = "uploads/";

        // Act
        var policyJson = _generator.GenerateBucketPolicy(bucketName, keyPrefix);

        // Assert
        policyJson.Should().Contain($"arn:aws:s3:::{bucketName}/{keyPrefix}*");
    }

    [Fact]
    public void GenerateBucketPolicy_AllowsPutObject()
    {
        // Act
        var policyJson = _generator.GenerateBucketPolicy("bucket");

        // Assert
        policyJson.Should().Contain("s3:PutObject");
    }

    [Fact]
    public void GenerateBucketPolicy_RequiresPrivateAcl()
    {
        // Act
        var policyJson = _generator.GenerateBucketPolicy("bucket");

        // Assert
        policyJson.Should().Contain("s3:x-amz-acl");
        policyJson.Should().Contain("private");
    }

    [Fact]
    public void GenerateBucketPolicy_IsValidJson()
    {
        // Act
        var policyJson = _generator.GenerateBucketPolicy("bucket", "prefix/");

        // Assert
        var act = () => JsonDocument.Parse(policyJson);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateBucketPolicy_HasCorrectVersion()
    {
        // Act
        var policyJson = _generator.GenerateBucketPolicy("bucket");

        // Assert
        policyJson.Should().Contain("\"Version\": \"2012-10-17\"");
    }

    #endregion

    #region Helper Methods

    private static S3PolicyGenerator.S3Config CreateBasicConfig()
    {
        return new S3PolicyGenerator.S3Config
        {
            BucketName = "test-bucket",
            Region = "us-east-1",
            AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
            SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
            KeyPrefix = "",
            Expiration = TimeSpan.FromDays(7),
            MaxFileSizeBytes = 10 * 1024 * 1024
        };
    }

    #endregion
}
