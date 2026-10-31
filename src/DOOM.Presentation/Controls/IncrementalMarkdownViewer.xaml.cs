namespace DOOM.Presentation.Controls;

using System.Windows;
using System.Windows.Controls;
using DOOM.Presentation.Markdown;

/// <summary>
/// Custom WPF UserControl for rendering markdown responses incrementally during streaming.
/// Features text selection, auto-scroll, stripped links (SEC-11), and zero layout flicker.
/// </summary>
public partial class IncrementalMarkdownViewer : UserControl
{
    public static readonly DependencyProperty MarkdownTextProperty = DependencyProperty.Register(
        nameof(MarkdownText),
        typeof(string),
        typeof(IncrementalMarkdownViewer),
        new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    public System.Windows.Documents.FlowDocument Document => RichBox.Document;

    private string _renderedText = string.Empty;

    public IncrementalMarkdownViewer()
    {
        InitializeComponent();
        RichBox.Document = MarkdownFlowDocumentBuilder.CreateDocument(Array.Empty<MarkdownBlock>());
    }

    private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IncrementalMarkdownViewer viewer)
        {
            viewer.UpdateContent((string)e.NewValue);
        }
    }

    public void UpdateContent(string newText)
    {
        if (string.IsNullOrEmpty(newText))
        {
            _renderedText = string.Empty;
            RichBox.Document = MarkdownFlowDocumentBuilder.CreateDocument(Array.Empty<MarkdownBlock>());
            return;
        }

        var blocks = IncrementalMarkdownParser.ParseBlocks(newText);

        // If the new text is an extension of the previously rendered text, update incrementally
        if (!string.IsNullOrEmpty(_renderedText) && newText.StartsWith(_renderedText, StringComparison.Ordinal))
        {
            MarkdownFlowDocumentBuilder.UpdateDocumentIncremental(RichBox.Document, blocks);
        }
        else
        {
            // Reset and build document
            RichBox.Document = MarkdownFlowDocumentBuilder.CreateDocument(blocks);
        }

        _renderedText = newText;
        RichBox.ScrollToEnd();
    }
}
