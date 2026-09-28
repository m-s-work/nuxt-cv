using CvApi.Pdf;

namespace CvApi.Tests;

public sealed class PdfFileNameTests
{
    [Theory]
    [InlineData("Max Mustermann", "en", "cv-max-mustermann-en.pdf")]
    [InlineData("Jürgen Müßig-Öztürk", "de", "cv-juergen-muessig-oeztuerk-de.pdf")]
    [InlineData("  José  O'Brien ", "en", "cv-jose-o-brien-en.pdf")]
    [InlineData(null, "en", "cv-en.pdf")]
    [InlineData("", "de", "cv-de.pdf")]
    [InlineData("***", "en", "cv-en.pdf")]
    public void File_name_contains_a_slug_of_the_name(string? name, string locale, string expected)
    {
        Assert.Equal(expected, PdfFileName.For(name, locale));
    }
}
