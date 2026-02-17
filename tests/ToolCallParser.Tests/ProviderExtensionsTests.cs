namespace ToolCallParser.Tests;

public class ProviderExtensionsTests
{
    #region GetFormat

    [Theory]
    [InlineData(Provider.OpenAI, ProviderFormat.OpenAI)]
    [InlineData(Provider.AzureOpenAI, ProviderFormat.OpenAI)]
    [InlineData(Provider.XAI, ProviderFormat.OpenAI)]
    [InlineData(Provider.Mistral, ProviderFormat.OpenAI)]
    [InlineData(Provider.DeepSeek, ProviderFormat.OpenAI)]
    [InlineData(Provider.Ollama, ProviderFormat.OpenAI)]
    [InlineData(Provider.GpuStack, ProviderFormat.OpenAI)]
    [InlineData(Provider.VLLM, ProviderFormat.OpenAI)]
    [InlineData(Provider.Qwen, ProviderFormat.OpenAI)]
    [InlineData(Provider.LMStudio, ProviderFormat.OpenAI)]
    [InlineData(Provider.LocalAI, ProviderFormat.OpenAI)]
    [InlineData(Provider.TGI, ProviderFormat.OpenAI)]
    [InlineData(Provider.OpenAICompatible, ProviderFormat.OpenAI)]
    public void GetFormat_OpenAICompatibleProviders_ReturnsOpenAI(Provider provider, ProviderFormat expected)
    {
        Assert.Equal(expected, provider.GetFormat());
    }

    [Theory]
    [InlineData(Provider.Anthropic, ProviderFormat.Anthropic)]
    [InlineData(Provider.AnthropicCompatible, ProviderFormat.Anthropic)]
    public void GetFormat_AnthropicCompatibleProviders_ReturnsAnthropic(Provider provider, ProviderFormat expected)
    {
        Assert.Equal(expected, provider.GetFormat());
    }

    [Fact]
    public void GetFormat_Google_ReturnsGoogle()
    {
        Assert.Equal(ProviderFormat.Google, Provider.Google.GetFormat());
    }

    [Fact]
    public void GetFormat_Cohere_ReturnsCohere()
    {
        Assert.Equal(ProviderFormat.Cohere, Provider.Cohere.GetFormat());
    }

    [Fact]
    public void GetFormat_Bedrock_ReturnsBedrock()
    {
        Assert.Equal(ProviderFormat.Bedrock, Provider.Bedrock.GetFormat());
    }

    [Fact]
    public void GetFormat_Auto_ReturnsUnknown()
    {
        Assert.Equal(ProviderFormat.Unknown, Provider.Auto.GetFormat());
    }

    #endregion

    #region IsOpenAICompatible

    [Theory]
    [InlineData(Provider.OpenAI)]
    [InlineData(Provider.AzureOpenAI)]
    [InlineData(Provider.Mistral)]
    [InlineData(Provider.DeepSeek)]
    [InlineData(Provider.Ollama)]
    [InlineData(Provider.OpenAICompatible)]
    public void IsOpenAICompatible_OpenAIProviders_ReturnsTrue(Provider provider)
    {
        Assert.True(provider.IsOpenAICompatible());
    }

    [Theory]
    [InlineData(Provider.Anthropic)]
    [InlineData(Provider.Google)]
    [InlineData(Provider.Cohere)]
    [InlineData(Provider.Bedrock)]
    public void IsOpenAICompatible_NonOpenAIProviders_ReturnsFalse(Provider provider)
    {
        Assert.False(provider.IsOpenAICompatible());
    }

    #endregion

    #region IsAnthropicCompatible

    [Theory]
    [InlineData(Provider.Anthropic)]
    [InlineData(Provider.AnthropicCompatible)]
    public void IsAnthropicCompatible_AnthropicProviders_ReturnsTrue(Provider provider)
    {
        Assert.True(provider.IsAnthropicCompatible());
    }

    [Theory]
    [InlineData(Provider.OpenAI)]
    [InlineData(Provider.Google)]
    [InlineData(Provider.Cohere)]
    [InlineData(Provider.Bedrock)]
    public void IsAnthropicCompatible_NonAnthropicProviders_ReturnsFalse(Provider provider)
    {
        Assert.False(provider.IsAnthropicCompatible());
    }

    #endregion

    #region GetDocumentationUrl

    [Theory]
    [InlineData(Provider.OpenAI)]
    [InlineData(Provider.AzureOpenAI)]
    [InlineData(Provider.Anthropic)]
    [InlineData(Provider.Google)]
    [InlineData(Provider.XAI)]
    [InlineData(Provider.Mistral)]
    [InlineData(Provider.Cohere)]
    [InlineData(Provider.DeepSeek)]
    [InlineData(Provider.Bedrock)]
    [InlineData(Provider.Ollama)]
    [InlineData(Provider.GpuStack)]
    [InlineData(Provider.VLLM)]
    [InlineData(Provider.Qwen)]
    public void GetDocumentationUrl_SupportedProviders_ReturnsUrl(Provider provider)
    {
        var url = provider.GetDocumentationUrl();

        Assert.NotNull(url);
        Assert.StartsWith("https://", url);
    }

    [Theory]
    [InlineData(Provider.Auto)]
    [InlineData(Provider.OpenAICompatible)]
    [InlineData(Provider.AnthropicCompatible)]
    [InlineData(Provider.LMStudio)]
    [InlineData(Provider.LocalAI)]
    [InlineData(Provider.TGI)]
    public void GetDocumentationUrl_UnsupportedProviders_ReturnsNull(Provider provider)
    {
        Assert.Null(provider.GetDocumentationUrl());
    }

    #endregion
}
