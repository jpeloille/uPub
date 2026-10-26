using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MyPublisher.Models;
using Xunit;

namespace MyPublisher.Tests.Interface;

/// <summary>
/// Le banc des gestes — le poste N1 : ce que la plate-forme sans affichage d'Avalonia 11.3.9
/// permet d'éprouver, joué par les aides de <see cref="Rendu"/>, avant que la phase N ne s'en
/// serve pour le clic droit, le presse-papiers, la sélection de texte et les raccourcis de
/// Publisher.
///
/// Ces tests n'éprouvent pas MyPublisher : ils éprouvent le banc. La plupart ouvrent une
/// fenêtre nue, un contrôle ou deux, pour que ce qui passe ou casse soit le banc et lui seul.
/// Les trois derniers ouvrent la vraie fenêtre, parce que c'est là que <c>SurLaPage</c> et
/// l'entrée en saisie ont un sens.
///
/// Ce qui ne s'éprouve pas ici, lu dans le code d'Avalonia : le presse-papiers du système (le
/// banc a le sien, en mémoire, <c>HeadlessClipboardImplStub</c>) ; les touches que prend le
/// bureau avant la fenêtre (Alt+F6, Ctrl+Alt+flèches) ; ce que X11 livre vraiment pour une
/// touche d'un clavier AZERTY — « &lt; » surtout, sur la touche qu'un QWERTY n'a pas — : le banc
/// livre ce qu'on lui dit ; l'allure d'un curseur.
/// </summary>
[Trait("poste", "N1")]
public class BancTests
{
    /// <summary>Une fenêtre nue autour d'un contenu, ouverte et mise en page.</summary>
    private static Window Fenetre(Control contenu)
    {
        var fenetre = new Window { Width = 400, Height = 300, Content = contenu };
        fenetre.Show();
        Rendu.UneImage();
        return fenetre;
    }

    /// <summary>Un aplat qu'on peut viser : un contrôle sans fond ne se touche pas au pointeur.</summary>
    private static Border Aplat() => new()
    {
        Width = 200, Height = 120, Background = Brushes.SteelBlue, Focusable = true,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };

    private static List<ContextMenu> MenusOuverts(Window fenetre) =>
        fenetre.GetVisualDescendants().OfType<ContextMenu>().Where(m => m.IsOpen).ToList();

    /// <summary>Une zone de texte d'une ligne, en tête de la fenêtre.</summary>
    private static TextBox Zone(string texte) => new()
    {
        Text = texte, Width = 300, FontSize = 16, AcceptsReturn = false,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
    };

    // ------------------------------------------------------------------ le menu contextuel

    [AvaloniaFact]
    public void Un_clic_droit_ouvre_le_menu_contextuel_et_son_entree_joue_sa_commande()
    {
        var jouee = 0;
        var entree = new MenuItem { Header = "Jouer", Command = new RelayCommand(() => jouee++) };
        var aplat = Aplat();
        aplat.ContextMenu = new ContextMenu { ItemsSource = new[] { entree } };
        var fenetre = Fenetre(aplat);

        try
        {
            Assert.Empty(MenusOuverts(fenetre));

            // CliquerDroit attend le menu dix images au plus, et échoue au-delà.
            var menu = Rendu.CliquerDroit(aplat);
            Assert.Same(aplat.ContextMenu, menu);
            Assert.True(menu.IsOpen);
            Assert.Equal(0, jouee);

            // Il est posé sur la fenêtre, en surimpression : un clic l'atteint comme un bouton.
            Assert.Contains(entree, fenetre.GetVisualDescendants().OfType<MenuItem>());
            Rendu.Cliquer(entree);
            Assert.Equal(1, jouee);
            Assert.False(menu.IsOpen, "le menu ne s'est pas refermé après son entrée");
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void La_touche_Menu_ouvre_le_menu_contextuel_mais_pas_Maj_F10()
    {
        var aplat = Aplat();
        aplat.ContextMenu = new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "Jouer" } } };
        var fenetre = Fenetre(aplat);

        try
        {
            aplat.Focus();
            Rendu.Battre();
            Assert.True(aplat.IsFocused);

            // Avalonia lit la touche Menu au relâché (Control.OnKeyUp), sur le contrôle qui a
            // le focus : pressée seule, elle n'ouvrirait rien.
            Rendu.Touche(fenetre, PhysicalKey.ContextMenu, relacher: true);
            var menu = Rendu.MenuOuvert(fenetre, "la touche Menu");
            Assert.Same(aplat.ContextMenu, menu);
            menu.Close();
            Rendu.UneImage();
            Assert.Empty(MenusOuverts(fenetre));

            // Maj+F10, que Windows lit comme la touche Menu, n'est pas dans la configuration
            // des touches d'Avalonia (PlatformHotkeyConfiguration.OpenContextMenu) : rien.
            aplat.Focus();
            Rendu.Touche(fenetre, PhysicalKey.F10, RawInputModifiers.Shift, relacher: true);
            for (var image = 0; image < 10; image++) Rendu.UneImage();
            Assert.Empty(MenusOuverts(fenetre));
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ le presse-papiers

    [AvaloniaFact]
    public void Un_texte_et_un_format_propre_font_l_aller_retour_par_le_presse_papiers()
    {
        var fenetre = Fenetre(Aplat());
        try
        {
            var pressePapiers = fenetre.Clipboard ?? throw new InvalidOperationException("aucun presse-papiers");
            var blocs = DataFormat.CreateStringApplicationFormat("MyPublisher.Blocs");

            var element = new DataTransferItem();
            element.Set(DataFormat.Text, "Deux blocs");
            element.Set(blocs, "[{\"Name\":\"Titre\"},{\"Name\":\"Corps\"}]");
            var transfert = new DataTransfer();
            transfert.Add(element);
            Rendu.Attendre(pressePapiers.SetDataAsync(transfert));

            Assert.Equal("Deux blocs", Rendu.TexteCopie(fenetre));
            Assert.Equal("[{\"Name\":\"Titre\"},{\"Name\":\"Corps\"}]", Rendu.Attendre(pressePapiers.TryGetValueAsync(blocs)));
            Assert.Contains(blocs, Rendu.Attendre(pressePapiers.GetDataFormatsAsync()));

            // Un texte seul remplace le tout : le format propre n'y est plus.
            Rendu.Attendre(pressePapiers.SetTextAsync("Un mot"));
            Assert.Equal("Un mot", Rendu.TexteCopie(fenetre));
            Assert.Null(Rendu.Attendre(pressePapiers.TryGetValueAsync(blocs)));
        }
        finally
        {
            fenetre.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_C_dans_une_zone_de_texte_met_sa_selection_au_presse_papiers()
    {
        var zone = Zone("Bonjour le monde");
        var fenetre = Fenetre(zone);
        try
        {
            Rendu.Attendre(fenetre.Clipboard!.SetTextAsync("rien encore"));

            // La sélection faite à la main : « le », caractères 8 à 10.
            Rendu.SelectionnerDuTexte(zone, 8, 10);
            Assert.Equal("le", zone.SelectedText);

            Rendu.Touche(fenetre, PhysicalKey.C, RawInputModifiers.Control);
            Assert.Equal("le", Rendu.TexteCopie(fenetre));
            Assert.Equal("Bonjour le monde", zone.Text);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ le glissé

    [AvaloniaFact]
    public void Un_glisse_du_4e_au_7e_caractere_selectionne_4_7_et_sans_le_bouton_tenu_rien()
    {
        var zone = Zone("Bonjour le monde");
        var fenetre = Fenetre(zone);
        try
        {
            Rendu.SelectionnerDuTexte(zone, 4, 7);
            Assert.Equal((4, 7), (zone.SelectionStart, zone.SelectionEnd));
            Assert.Equal("our", zone.SelectedText);
        }
        finally
        {
            fenetre.Close();
        }

        // Le même glissé, sans le bouton dit tenu pendant le mouvement — le défaut de deux des
        // trois Glisser recopiés. Dans une fenêtre neuve : chacune a sa souris, et une
        // pression au même point dans la même seconde ferait un double-clic, qui choisit un mot.
        zone = Zone("Bonjour le monde");
        fenetre = Fenetre(zone);
        try
        {
            Rendu.UneImage();
            var depart = Rendu.BordDuCaractere(zone, 4);
            var arrivee = Rendu.BordDuCaractere(zone, 7);
            fenetre.MouseDown(depart, MouseButton.Left);
            fenetre.MouseMove(depart + (arrivee - depart) / 2);
            fenetre.MouseMove(arrivee);
            fenetre.MouseUp(arrivee, MouseButton.Left);
            Rendu.Battre();

            Assert.Equal((4, 4), (zone.SelectionStart, zone.SelectionEnd));
            Assert.Equal(4, zone.CaretIndex);
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ le clavier AZERTY

    [AvaloniaFact]
    public void Key_A_sur_la_place_de_Q_sous_Ctrl_declenche_la_liaison_Ctrl_A()
    {
        var jouee = 0;
        var fenetre = Fenetre(Aplat());
        fenetre.KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.A, KeyModifiers.Control),
            Command = new RelayCommand(() => jouee++),
        });

        try
        {
            // Sur un AZERTY, la touche marquée A est à la place du Q d'un QWERTY.
            Rendu.ToucheAzerty(fenetre, PhysicalKey.Q, RawInputModifiers.Control);
            Assert.Equal(1, jouee);

            // La liaison lit la touche, non sa place : la place de A, sur un AZERTY, est Q.
            Rendu.ToucheAzerty(fenetre, PhysicalKey.A, RawInputModifiers.Control);
            Assert.Equal(1, jouee);

            // Et la même place jouée en QWERTY livre Q : rien non plus.
            Rendu.Touche(fenetre, PhysicalKey.Q, RawInputModifiers.Control);
            Assert.Equal(1, jouee);
            Rendu.Touche(fenetre, PhysicalKey.A, RawInputModifiers.Control);
            Assert.Equal(2, jouee);

            // Une place que le banc ne sait pas traduire est refusée, plutôt que livrée en QWERTY.
            Assert.Throws<ArgumentException>(() => Rendu.ToucheAzerty(fenetre, PhysicalKey.Digit1));
        }
        finally
        {
            fenetre.Close();
        }
    }

    // ------------------------------------------------------------------ le double-clic

    [AvaloniaFact]
    public void Deux_clics_a_3_px_ne_font_pas_un_double_clic_et_a_2_px_si()
    {
        foreach (var (ecart, attendu) in new[] { (3.0, 1), (2.0, 2) })
        {
            // Une fenêtre neuve par cas : le compte des clics est celui de sa souris.
            var aplat = Aplat();
            var comptes = new List<int>();
            aplat.PointerPressed += (_, e) => comptes.Add(e.ClickCount);
            var fenetre = Fenetre(aplat);

            try
            {
                var centre = Rendu.Centre(aplat);
                Rendu.Cliquer(fenetre, centre);
                Rendu.Cliquer(fenetre, centre + new Vector(ecart, 0));
                Assert.Equal([1, attendu], comptes);
            }
            finally
            {
                fenetre.Close();
            }
        }
    }

    // ------------------------------------------------------------------ la vraie fenêtre

    [AvaloniaFact]
    public void SurLaPage_suit_l_echelle_de_la_planche_et_echoue_sans_PageRoot()
    {
        var nue = Fenetre(Aplat());
        try
        {
            var refus = Assert.Throws<InvalidOperationException>(() => Rendu.SurLaPage(nue, 10, 10));
            Assert.Contains("PageRoot", refus.Message);
        }
        finally
        {
            nue.Close();
        }

        var bloc = new ShapeFrame { Name = "Carré", X = 100, Y = 100, Width = 80, Height = 60, Fill = new SolidFill("#FFCC0000") };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        vm.Zoom = 2 / Units.PointToDip;
        Rendu.UneImage();
        Assert.Equal(2, vm.RenderScale, precision: 9);

        // Cent points de page font deux cents pixels.
        var origine = Rendu.SurLaPage(fenetre, 0, 0);
        var loin = Rendu.SurLaPage(fenetre, 100, 50);
        Assert.Equal(200, loin.X - origine.X, precision: 6);
        Assert.Equal(100, loin.Y - origine.Y, precision: 6);

        // Le centre du bloc, à cette échelle, est bien sur lui : le clic le prend. Une copie
        // d'avant N1, qui ajoutait les points de page à l'origine comme des pixels, aurait
        // cliqué en (70, 65) de la page, à côté.
        Rendu.Cliquer(fenetre, Rendu.Centre(fenetre, bloc));
        Assert.Same(bloc, vm.SingleSelected);
    }

    [AvaloniaFact]
    public void EntrerEnSaisie_ouvre_la_zone_du_bloc_et_la_rend()
    {
        var bloc = new TextFrame { Name = "Texte", X = 100, Y = 100, Width = 240, Height = 60, Text = "Bonjour le monde" };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        Assert.Null(Rendu.ZoneDeSaisie(fenetre, bloc));

        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Assert.True(bloc.IsEditing);
        Assert.Same(bloc, zone.DataContext);
        Assert.Same(zone, Rendu.ZoneDeSaisie(fenetre, bloc));

        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing);
        Assert.Null(Rendu.ZoneDeSaisie(fenetre, bloc));
    }

    [AvaloniaFact]
    public void Dans_la_saisie_d_un_bloc_un_glisse_selectionne_et_Ctrl_C_copie()
    {
        // Les gestes du banc, joués là où la phase N les jouera : dans la zone de saisie d'un
        // bloc, sur la planche, sous la couche de saisie.
        var bloc = new TextFrame { Name = "Texte", X = 100, Y = 100, Width = 240, Height = 60, Text = "Bonjour le monde" };
        var (fenetre, vm) = Rendu.Ouvrir(Rendu.Feuille(bloc));
        var zone = Rendu.EntrerEnSaisie(fenetre, vm, bloc);
        Rendu.Attendre(fenetre.Clipboard!.SetTextAsync("rien encore"));

        Rendu.SelectionnerDuTexte(zone, 4, 7);
        Assert.True(bloc.IsEditing, "le glissé a refermé la saisie");
        Assert.Equal((4, 7), (zone.SelectionStart, zone.SelectionEnd));

        Rendu.Touche(fenetre, PhysicalKey.C, RawInputModifiers.Control);
        Assert.Equal("our", Rendu.TexteCopie(fenetre));
        Assert.Equal("Bonjour le monde", bloc.Text);
    }
}
