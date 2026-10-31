namespace DOOM.UnitTests;

using DOOM.Core.Domain;

public class ModelIdTests
{
    [Theory]
    [InlineData("gemini-3.5-flash")]
    [InlineData("gemini-2.5-flash")]
    [InlineData("gemini-1.5-pro")]
    [InlineData("custom-model.v1")]
    public void Constructor_ValidPattern_InitializesSuccessfully(string modelString)
    {
        var model = new ModelId(modelString);
        Assert.Equal(modelString, model.Value);
        Assert.Equal(modelString, model.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid_Upper")]
    [InlineData("-starts-with-dash")]
    [InlineData("model with spaces")]
    public void Constructor_InvalidPattern_ThrowsArgumentException(string invalidModel)
    {
        Assert.Throws<ArgumentException>(() => new ModelId(invalidModel));
    }

    [Fact]
    public void Default_IsGemini35Flash()
    {
        Assert.Equal("gemini-3.5-flash", ModelId.Default.Value);
    }
}

