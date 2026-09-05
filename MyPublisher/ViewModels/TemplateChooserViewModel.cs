using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MyPublisher.Services;

namespace MyPublisher.ViewModels;

/// <summary>
/// L'écran de choix : le catalogue et ce qu'on y a désigné. Il ne crée rien lui-même —
/// il rend le choix, et c'est la fenêtre principale qui en fait un document.
/// </summary>
public partial class TemplateChooserViewModel : ViewModelBase
{
    public TemplateChooserViewModel(IReadOnlyList<TemplateChoice> choices)
    {
        Choices = choices;
        Selected = choices.FirstOrDefault();
    }

    public IReadOnlyList<TemplateChoice> Choices { get; }

    [ObservableProperty] private TemplateChoice? _selected;

    public bool HasTemplates => Choices.Count > 1;
}
