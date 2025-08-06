using Amazon.S3.Model;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Octokit;
using System.Net.Http.Headers;
using System.Text;

namespace ReleaseWebhook.Controllers;

[ApiController]
[Route("sync")]
public class SyncController(
    IOptions<GitHubSettings> ghOptions,
    IOptions<MinioSettings> minioOptions,
    GitHubClient ghClient,
    IAmazonS3 s3Client,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    async Task ResponseLine(string line)
    {
        await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(line));
        await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine));
        await Response.BodyWriter.FlushAsync();
    }

    async Task PrepareResponse()
    {
        Response.StatusCode = 200;
        Response.ContentType = "text/plain; charset=utf-8";
        await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes("Starting synchronization...\n"));
        await Response.BodyWriter.FlushAsync();
    }

    [HttpPost("{runId:long}/{artifactName}")]
    [HttpGet("{runId:long}/{artifactName}")]
    public async Task SyncOne(long runId, string artifactName)
    {
        await PrepareResponse();
        GitHubSettings ghSettings = ghOptions.Value;
        MinioSettings minio = minioOptions.Value;

        if (string.IsNullOrWhiteSpace(ghSettings.Owner) || string.IsNullOrWhiteSpace(ghSettings.Repo))
        {
            await ResponseLine("GitHub Owner or Repo is not configured!");
            return;
        }

        WorkflowRun run;
        try
        {
            run = await ghClient.Actions.Workflows.Runs.Get(ghSettings.Owner, ghSettings.Repo, runId);
        }
        catch (NotFoundException)
        {
            await ResponseLine($"Run {runId} does not exist");
            return;
        }

        long runNumber = run.RunNumber;
        await ResponseLine($"Run {runId} Run Number: {runNumber}");

        ListArtifactsResponse artifactsResp = await ghClient.Actions.Artifacts.ListWorkflowArtifacts(ghSettings.Owner, ghSettings.Repo, runId);
        if (artifactsResp.Artifacts.Count == 0)
        {
            await ResponseLine($"Run {runId} has no Artifacts");
            return;
        }

        using HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Sdcb-Chats-Sync/1.0");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghSettings.Token);
        httpClient.Timeout = TimeSpan.FromHours(2);

        Artifact? artifact = artifactsResp.Artifacts.FirstOrDefault(a => a.Name == artifactName);
        if (artifact == null)
        {
            await ResponseLine($"Artifact {artifactName} does not exist");
            return;
        }

        await ResponseLine($"Downloading Artifact {artifact.Name}...");
        HttpResponseMessage response = await httpClient.GetAsync(artifact.ArchiveDownloadUrl);
        if (!response.IsSuccessStatusCode)
        {
            await ResponseLine($"Failed to download Artifact {artifact.Name}: {response.StatusCode}");
            return;
        }

        using Stream zipStream = await response.Content.ReadAsStreamAsync();
        await ResponseLine($"Downloaded Artifact {artifact.Name}, size: {zipStream.Length}");

        string s3Key = $"r{runNumber}/{artifact.Name}.zip";
        PutObjectRequest putRequest = new()
        {
            BucketName = minio.BucketName,
            Key = s3Key,
            InputStream = zipStream
        };
        await s3Client.PutObjectAsync(putRequest);
        await ResponseLine($"Uploaded Artifact {artifact.Name} => {s3Key}");
    }

    [HttpPost("{runId:long}/{artifactName}/async")]
    [HttpGet("{runId:long}/{artifactName}/async")]
    public async Task SyncOneAsync(long runId, string artifactName)
    {
        await PrepareResponse();
        GitHubSettings ghSettings = ghOptions.Value;
        MinioSettings minio = minioOptions.Value;

        if (string.IsNullOrWhiteSpace(ghSettings.Owner) || string.IsNullOrWhiteSpace(ghSettings.Repo))
        {
            await ResponseLine("GitHub Owner or Repo is not configured!");
            return;
        }

        WorkflowRun run;
        try
        {
            run = await ghClient.Actions.Workflows.Runs.Get(ghSettings.Owner, ghSettings.Repo, runId);
        }
        catch (NotFoundException)
        {
            await ResponseLine($"Run {runId} does not exist");
            return;
        }

        long runNumber = run.RunNumber;
        await ResponseLine($"Run {runId} Run Number: {runNumber}");

        ListArtifactsResponse artifactsResp = await ghClient.Actions.Artifacts.ListWorkflowArtifacts(ghSettings.Owner, ghSettings.Repo, runId);
        if (artifactsResp.Artifacts.Count == 0)
        {
            await ResponseLine($"Run {runId} has no Artifacts");
            return;
        }

        Artifact? artifact = artifactsResp.Artifacts.FirstOrDefault(a => a.Name == artifactName);
        if (artifact == null)
        {
            await ResponseLine($"Artifact {artifactName} does not exist");
            return;
        }

        await ResponseLine($"Downloading Artifact {artifact.Name}...");
        await Response.BodyWriter.FlushAsync();

        _ = Task.Run(async () =>
        {
            using HttpClient httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Sdcb-Chats-Sync/1.0");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghSettings.Token);
            httpClient.Timeout = TimeSpan.FromHours(2);

            string artifactDownloadUrl = artifact.ArchiveDownloadUrl;
            string s3Key = $"r{runNumber}/{artifact.Name}.zip";

            try
            {
                HttpResponseMessage downloadResponse = await httpClient.GetAsync(artifactDownloadUrl);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"[Async] Failed to download Artifact {artifact.Name}: {downloadResponse.StatusCode}");
                    return;
                }

                using Stream zipStream = await downloadResponse.Content.ReadAsStreamAsync();
                PutObjectRequest putRequest = new()
                {
                    BucketName = minio.BucketName,
                    Key = s3Key,
                    InputStream = zipStream
                };
                await s3Client.PutObjectAsync(putRequest);
                Console.WriteLine($"[Async] Uploaded Artifact {artifact.Name} => {s3Key}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Async] Exception processing Artifact {artifact.Name}: {ex.Message}");
            }
        });
    }

    [HttpPost("latest/{runNumber:long}")]
    [HttpGet("latest/{runNumber:long}")]
    public async Task SyncLatest(long runNumber)
    {
        await PrepareResponse();
        GitHubSettings ghSettings = ghOptions.Value;
        MinioSettings minio = minioOptions.Value;

        if (string.IsNullOrWhiteSpace(ghSettings.Owner) || string.IsNullOrWhiteSpace(ghSettings.Repo))
        {
            await ResponseLine("GitHub Owner or Repo is not configured!");
            return;
        }

        ListObjectsV2Request listRequest = new()
        {
            BucketName = minio.BucketName,
            Prefix = "latest/"
        };

        ListObjectsV2Response listResponse = await s3Client.ListObjectsV2Async(listRequest);
        foreach (S3Object s3Obj in listResponse.S3Objects)
        {
            DeleteObjectRequest deleteRequest = new()
            {
                BucketName = minio.BucketName,
                Key = s3Obj.Key
            };
            await s3Client.DeleteObjectAsync(deleteRequest);
            await ResponseLine($"Deleted {s3Obj.Key}");
        }

        string copySrcPrefix = $"r{runNumber}/";
        ListObjectsV2Request copyListRequest = new()
        {
            BucketName = minio.BucketName,
            Prefix = copySrcPrefix
        };
        ListObjectsV2Response copyListResponse = await s3Client.ListObjectsV2Async(copyListRequest);
        foreach (S3Object s3Obj in copyListResponse.S3Objects)
        {
            string relativeKey = s3Obj.Key[copySrcPrefix.Length..];
            string destKey = $"latest/{relativeKey}";
            CopyObjectRequest copyRequest = new()
            {
                SourceBucket = minio.BucketName,
                SourceKey = s3Obj.Key,
                DestinationBucket = minio.BucketName,
                DestinationKey = destKey
            };
            await s3Client.CopyObjectAsync(copyRequest);
            await ResponseLine($"Copied {s3Obj.Key} => {destKey}");
        }
    }
}