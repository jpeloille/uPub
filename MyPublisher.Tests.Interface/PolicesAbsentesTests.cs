using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.ViewModels;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Une police absente de la machine survit à la sélection — poste Q20.
///
/// La liste des polices du panneau ne proposait que celles de la machine. Un bloc dont
/// la police n'y est pas — un .pub composé ailleurs, un document écrit à la main, une
/// valeur gardée libre par la migration de C5b — laissait la liste sans sélection ; et
/// en passant d'un bloc à l'autre, la liste renvoyait null au bloc. La police était
/// perdue, et le rendu levait ensuite en composant le texte.
/// </summary>
[Trait("poste", "Q20")]
public class PolicesAbsentesTests
{
    private static ComboBox ListeDesPolices(Window fenetre, MainWindowViewModel vm, Frame bloc) =>
        fenetre.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => ReferenceEquals(c.DataContext, bloc) && c.SelectedItem is string
                         && c.ItemsSource is System.Collections.Generic.IEnumerable<string> polices
                         && polices.Contains(vm.FontFamilies[2]));

    [AvaloniaTheory]
    [InlineData("Police Absente De Cette Machine", "DejaVu Sans")]
    [InlineData("Police Absente De Cette Machine", "scheme:body")]
    [InlineData("!scheme:heading", "scheme:body")]
    public void Passer_dun_texte_a_lautre_ne_perd_pas_la_police(string absente, string presente)
    {
        var t = new TextFrame { X = 40, Y = 140, Width = 200, Height = 40, Text = "T", FontFamily = absente };
        var t2 = new TextFrame { X = 40, Y = 200, Width = 200, Height = 40, Text = "T2", FontFamily = presente };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(t, t2));

        foreach (var bloc in new[] { t, t2, t, t2, t })
        {
            vm.Selected = bloc;
            Rendu.Battre(8);
        }

        Assert.Equal(absente, t.FontFamily);
        Assert.Equal(presente, t2.FontFamily);
        Assert.False(vm.CanUndo, "la sélection a écrit dans le document");

        // La liste montre la police du bloc, et dit qu'elle manque.
        var liste = ListeDesPolices(fenetre, vm, t);
        Assert.Equal(absente, liste.SelectedItem);
        Assert.Contains(liste.GetVisualDescendants().OfType<TextBlock>(), b => b.Text == $"{absente} (absente)");

        // Et la page se compose encore : c'est là que le null finissait par lever.
        Rendu.Encre(fenetre);
    }

    private static TableFrame Tableau(double y, string police)
    {
        var tableau = new TableFrame { X = 40, Y = y, FontFamily = police };
        tableau.Columns.Add(new TableColumn(100));
        tableau.Rows.Add(new TableRow(30));
        tableau.Cells.Add(new TableCell { Row = 0, Column = 0, Text = "x" });
        return tableau;
    }

    [AvaloniaFact]
    public void Passer_dun_tableau_a_lautre_ne_perd_pas_la_police()
    {
        // Le tableau a sa propre liste au panneau : le défaut ne paraît qu'en passant d'un
        // tableau à un autre, jamais d'un tableau à un texte.
        var absent = Tableau(40, "Police Absente De Cette Machine");
        var present = Tableau(140, "DejaVu Sans");
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(absent, present));

        foreach (var bloc in new[] { absent, present, absent, present, absent })
        {
            vm.Selected = bloc;
            Rendu.Battre(8);
        }

        Assert.Equal("Police Absente De Cette Machine", absent.FontFamily);
        Assert.Equal("DejaVu Sans", present.FontFamily);
        Assert.False(vm.CanUndo, "la sélection a écrit dans le document");
        Rendu.Encre(fenetre);
    }

    [AvaloniaFact]
    public void Un_bloc_venu_apres_la_liste_garde_sa_police()
    {
        // La liste se fait à l'ouverture. Un bloc posé ensuite avec une police que ni la
        // machine ni la liste n'ont — aucun geste ne le fait aujourd'hui, un collage le
        // fera — laisse la liste sans sélection : le second garde-fou, qui ne renvoie
        // jamais null au bloc, est alors seul à tenir.
        var present = new TextFrame { X = 40, Y = 200, Width = 200, Height = 40, Text = "T2", FontFamily = "DejaVu Sans" };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(present));
        var venu = new TextFrame { X = 40, Y = 140, Width = 200, Height = 40, Text = "T", FontFamily = "Police Venue Apres" };
        vm.CurrentPage!.Frames.Add(venu);
        Rendu.Battre(8);
        Assert.DoesNotContain("Police Venue Apres", vm.FontFamilies);

        foreach (var bloc in new[] { venu, present, venu, present, venu })
        {
            vm.Selected = bloc;
            Rendu.Battre(8);
        }

        Assert.Equal("Police Venue Apres", venu.FontFamily);
        Rendu.Encre(fenetre);
    }

    [AvaloniaFact]
    public void Choisir_une_police_de_la_liste_lecrit_toujours()
    {
        // Le garde-fou ne doit pas empêcher le geste : une police choisie s'écrit.
        var t = new TextFrame { X = 40, Y = 140, Width = 200, Height = 40, Text = "T", FontFamily = "Police Absente De Cette Machine" };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(t));
        vm.Selected = t;
        Rendu.Battre(8);

        var liste = ListeDesPolices(fenetre, vm, t);
        liste.SelectedItem = "DejaVu Serif";
        Rendu.Battre();

        Assert.Equal("DejaVu Serif", t.FontFamily);
        Assert.Equal("DejaVu Serif", liste.SelectedItem);
        // La liste ne change pas sous le geste : la police absente y reste jusqu'à la
        // prochaine ouverture, et l'on peut y revenir.
        Assert.Contains("Police Absente De Cette Machine", vm.FontFamilies);
        liste.SelectedItem = "Police Absente De Cette Machine";
        Rendu.Battre();
        Assert.Equal("Police Absente De Cette Machine", t.FontFamily);
    }
}
