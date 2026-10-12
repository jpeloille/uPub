using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// Poste Q17 — le texte composé suit une collection remplacée, parce qu'une collection
/// ne se remplace plus.
///
/// <c>ControleDeBloc</c> s'abonne aux fragments et aux écarts du bloc qu'il dessine. Tant
/// que <c>Spans</c> et <c>Paragraphs</c> avaient un accesseur public en écriture, un code
/// qui les aurait remplacées sur un bloc affiché aurait laissé le contrôle accroché à
/// l'ancienne collection et sourd à la nouvelle. Le poste a fermé l'accesseur plutôt que
/// d'écouter le remplacement : il n'existe plus qu'à la construction (<c>init</c>), pour
/// l'initialiseur et le sérialiseur. Le compilateur refuse donc le geste fautif — et
/// c'est ce que ces tests tiennent, avec ce que la fermeture ne devait pas casser.
/// </summary>
public class CollectionsFermeesTests
{
    /// <summary>
    /// Les collections qu'un contrôle de bloc écoute : celles du bloc de texte, et celles
    /// d'une case de tableau, que <c>CellFrames</c> prête au bloc qu'il construit pour
    /// elle. Découvertes par réflexion plutôt qu'énumérées : une collection ajoutée
    /// demain à l'un de ces types est tenue le jour où elle est écrite.
    /// </summary>
    public static TheoryData<Type, string> Collections()
    {
        var donnees = new TheoryData<Type, string>();
        foreach (var type in new[] { typeof(TextFrame), typeof(TableCell) })
        {
            foreach (var propriete in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (EstUneCollectionObservable(propriete.PropertyType)) donnees.Add(type, propriete.Name);
            }
        }

        return donnees;
    }

    [Fact]
    [Trait("poste", "Q17")]
    public void Les_collections_ecoutees_sont_bien_trouvees()
    {
        // Le témoin de la découverte : si elle ne trouvait plus rien, la théorie
        // suivante passerait sans rien éprouver.
        var trouvees = Collections().Select(ligne => $"{((Type)ligne[0]).Name}.{ligne[1]}").ToArray();

        Assert.Contains("TextFrame.Spans", trouvees);
        Assert.Contains("TextFrame.Paragraphs", trouvees);
        Assert.Contains("TableCell.Spans", trouvees);
    }

    [Theory]
    [Trait("poste", "Q17")]
    [MemberData(nameof(Collections))]
    public void Une_collection_ecoutee_ne_se_remplace_qu_a_la_construction(Type type, string nom)
    {
        var ecriture = type.GetProperty(nom)!.SetMethod;

        // Un accesseur « init » est un accesseur en écriture que le compilateur marque
        // d'un modificateur requis, « IsExternalInit » : c'est cette marque qui lui fait
        // refuser l'affectation hors d'un initialiseur (CS8852).
        Assert.NotNull(ecriture);
        Assert.Contains(typeof(IsExternalInit), ecriture!.ReturnParameter.GetRequiredCustomModifiers());
    }

    /// <summary>
    /// Ce que la fermeture ne devait pas casser : le sérialiseur pose les collections à
    /// la lecture d'un fichier, et l'annulation relit un instantané par le même chemin.
    /// </summary>
    [Fact]
    [Trait("poste", "Q17")]
    public void La_lecture_d_un_document_pose_encore_les_collections()
    {
        var bloc = new TextFrame { Text = "Un\nDeux", X = 10, Y = 10, Width = 100, Height = 40 };
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 2, Bold = true });
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 1, Length = 1, IndentLeft = 12 });

        var tableau = new TableFrame { X = 10, Y = 80 };
        tableau.Columns.Add(new TableColumn { Width = 80 });
        tableau.Rows.Add(new TableRow { Height = 20 });
        var cellule = new TableCell { Row = 0, Column = 0, Text = "Case" };
        cellule.Spans.Add(new StyleSpan { Start = 0, Length = 4, Italic = true });
        tableau.Cells.Add(cellule);

        var document = new Document { Title = "Q17" };
        document.Pages.Add(new Page { Name = "Page 1" });
        document.Pages[0].Frames.Add(bloc);
        document.Pages[0].Frames.Add(tableau);

        var relu = DocumentSerializer.FromJson(DocumentSerializer.ToJson(document));
        var bloc2 = (TextFrame)relu.Pages[0].Frames[0];
        var case2 = ((TableFrame)relu.Pages[0].Frames[1]).Cells[0];

        Assert.True(Assert.Single(bloc2.Spans).Bold);
        Assert.Equal(12, Assert.Single(bloc2.Paragraphs).IndentLeft);
        Assert.True(Assert.Single(case2.Spans).Italic);
    }

    /// <summary>
    /// L'autre écrivain légitime : le bloc qu'un tableau construit pour chaque case
    /// emprunte la collection de la case, par l'initialiseur. C'est la même instance, et
    /// c'est pourquoi le contrôle qui dessine la case entend un fragment ajouté à la case.
    /// </summary>
    [Fact]
    [Trait("poste", "Q17")]
    public void Une_case_prete_toujours_sa_collection_a_son_bloc()
    {
        var tableau = new TableFrame();
        tableau.Columns.Add(new TableColumn { Width = 80 });
        tableau.Rows.Add(new TableRow { Height = 20 });
        tableau.Cells.Add(new TableCell { Row = 0, Column = 0, Text = "Case" });

        Assert.Same(tableau.Cells[0].Spans, ((TextFrame)tableau.CellFrames.Single()).Spans);
    }

    private static bool EstUneCollectionObservable(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ObservableCollection<>)) return true;
        }

        return false;
    }
}
