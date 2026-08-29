using System.Net.Http.Headers;
namespace PromptResponse.Desktop.Services;
public interface IHttpsSubmissionService { Task<HttpsSubmissionResult> SubmitAsync(string target, string aprJson, CancellationToken cancellationToken = default); }
public sealed record HttpsSubmissionResult(bool Succeeded, string Message);
public sealed class HttpsSubmissionService : IHttpsSubmissionService
{
 public async Task<HttpsSubmissionResult> SubmitAsync(string target,string aprJson,CancellationToken cancellationToken=default) {
  if(!Uri.TryCreate(target,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)) return new(false,"This is not a safe HTTPS submission target.");
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(30)};
  using var content=new StringContent(aprJson); content.Headers.ContentType=new MediaTypeHeaderValue("application/vnd.apr+json");
  try { using var response=await client.PostAsync(uri,content,cancellationToken); return response.IsSuccessStatusCode?new(true,$"Submitted to {uri}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}"):new(false,$"Submission failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. No redirect was followed."); }
  catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException) { return new(false,$"Submission failed: {ex.Message}"); }
 }
}
