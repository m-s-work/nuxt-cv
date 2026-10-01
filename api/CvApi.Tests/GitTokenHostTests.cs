using CvApi.Tenants;

namespace CvApi.Tests;

public sealed class GitTokenHostTests
{
    [Theory]
    [InlineData("https://github.com/alice/cv.git", null, "github.com")]
    [InlineData("https://GitHub.com/alice/cv.git", "", "github.com")]
    [InlineData("https://gitlab.example.org/alice/cv.git", "github.com, gitlab.example.org", "gitlab.example.org")]
    [InlineData("https://gitlab.example.org/alice/cv.git", "GITLAB.example.org", "gitlab.example.org")]
    public void Token_is_scoped_to_configured_hosts(string repo, string? hosts, string expected)
    {
        Assert.Equal(expected, GitRevisionFetcher.TokenHostFor(repo, hosts));
    }

    [Theory]
    [InlineData("https://evil.example.com/alice/cv.git", null)]
    [InlineData("https://github.com.evil.example/alice/cv.git", null)]
    [InlineData("https://evil.github.com/alice/cv.git", null)]
    [InlineData("https://gitlab.example.org/alice/cv.git", "github.com")]
    [InlineData("http://github.com/alice/cv.git", null)]
    [InlineData("https://github.com:8443/alice/cv.git", null)]
    [InlineData("https://user@github.com/alice/cv.git", null)]
    [InlineData("/srv/git/cv", null)]
    [InlineData("not a url", null)]
    public void Token_is_not_sent_to_other_hosts(string repo, string? hosts)
    {
        Assert.Null(GitRevisionFetcher.TokenHostFor(repo, hosts));
    }
}
