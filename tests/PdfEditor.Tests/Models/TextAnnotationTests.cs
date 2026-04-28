using Randnotiz.Models;

namespace Randnotiz.Tests.Models;

public class TextAnnotationTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var annotation = new TextAnnotation(100.0, 200.0, 0);

        Assert.Equal(100.0, annotation.X);
        Assert.Equal(200.0, annotation.Y);
        Assert.Equal(0, annotation.PageIndex);
        Assert.Equal("", annotation.Text);
        Assert.Equal(12.0, annotation.FontSize);
        Assert.Equal("Arial", annotation.FontFamily);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        var annotation = new TextAnnotation(0, 0, 0);
        annotation.Text = "Urlaubsantrag";
        annotation.FontSize = 16.0;
        annotation.FontFamily = "Times New Roman";
        annotation.X = 50.0;
        annotation.Y = 75.0;

        Assert.Equal("Urlaubsantrag", annotation.Text);
        Assert.Equal(16.0, annotation.FontSize);
        Assert.Equal("Times New Roman", annotation.FontFamily);
        Assert.Equal(50.0, annotation.X);
        Assert.Equal(75.0, annotation.Y);
    }
}
