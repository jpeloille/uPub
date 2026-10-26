using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.VisualTree;
using MyPublisher.Models;
using MyPublisher.Services;
using MyPublisher.Views;

namespace MyPublisher.Banc;

/// <summary>
/// L'empreinte de la composition d'un document, telle que la vraie fenêtre la produit —
/// ce que <c>Scripts/empreinte-composition.sh</c> relève avant et après un poste qui
/// promet que « la sortie du compositeur ne change pas » (poste N3).
///
/// Pour chaque contrôle de texte de chaque page, groupes et cases compris : les lignes
/// que le noyau compose, à la règle de l'écran et à celle du PDF, morceau par morceau —
/// texte, style, abscisse et largeur, au bit près ; puis ce que l'écran dessine, par
/// <see cref="TexteCompose.Poser"/> et <see cref="TexteCompose.Souligner"/>, les
/// fonctions mêmes que le rendu appelle ; enfin l'empreinte des pixels de la page.
///
/// Elle ne lit des lignes que les membres qu'elles avaient avant N3 : l'empreinte d'avant
/// et celle d'après se comparent ligne à ligne, par <c>diff</c>.
/// </summary>
internal static class Empreinte
{
    public static int Lancer(string[] args)
    {
        var chemin = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : null;
        var i = Array.IndexOf(args, "--sortie");
        var sortie = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        if (chemin is null || sortie is null)
        {
            Console.Error.WriteLine("Usage : MyPublisher.Banc empreinte <doc> --sortie <empreinte.txt>");
            return 2;
        }

        Banc.Configurer().SetupWithoutStarting();
        var (fenetre, vm) = Banc.Ouvrir(chemin);
        vm.ShowGuides = false;
        vm.Selection.Clear();

        var ecran = new MetriquesAvalonia();
        // La règle du PDF, construite comme l'export la construit : la coupure s'y décide
        // sur la plus large d'elle et de celle de l'écran (poste Q13).
        var pdf = MetriquesPdf.HorsPage(ecran: new MetriquesAvalonia());

        var texte = new StringBuilder();
        var blocs = 0;
        var lignes = 0;
        for (var page = 1; page <= vm.Document.Pages.Count; page++)
        {
            vm.CurrentPage = vm.Document.Pages[page - 1];
            Banc.Battre();
            var planche = Banc.Page(fenetre);
            texte.Append("page ").Append(page).AppendLine();

            var rang = 0;
            foreach (var controle in planche.GetVisualDescendants().OfType<TexteCompose>())
            {
                if (controle.Bloc is not { } bloc) continue;
                var contenu = controle.Contenu ?? "";
                texte.Append("  bloc ").Append(rang++).Append(" « ").Append(Echapper(bloc.DisplayName))
                    .Append(" » visible=").Append(controle.IsEffectivelyVisible)
                    .Append(" contenu=« ").Append(Echapper(contenu)).AppendLine(" »");
                blocs++;
                if (contenu.Length == 0) continue;

                foreach (var (nom, regle) in new (string, Compositeur.IMetriques)[] { ("écran", ecran), ("pdf", pdf) })
                {
                    var composees = Composer(controle, bloc, contenu, regle);
                    texte.Append("    composition ").Append(nom).Append(" : ").Append(composees.Count).AppendLine(" lignes");
                    lignes += composees.Count;
                    foreach (var ligne in composees)
                    {
                        texte.Append("      ligne « ").Append(Echapper(ligne.Texte))
                            .Append(" » largeur=").Append(D(ligne.Largeur))
                            .Append(" naturelle=").Append(D(ligne.Naturelle))
                            .Append(" fin=").Append(ligne.FinDeParagraphe)
                            .Append(" fer=").Append(ligne.Fer)
                            .Append(" retrait=").Append(D(ligne.Retrait))
                            .Append(" disponible=").Append(D(ligne.Disponible))
                            .Append(" avant=").Append(D(ligne.EspaceAvant))
                            .Append(" apres=").Append(D(ligne.EspaceApres))
                            .Append(" colonne=").Append(ligne.Colonne)
                            .Append(" haut=").Append(D(ligne.Haut))
                            .Append(" avance=").Append(D(ligne.Avance))
                            .AppendLine();
                        foreach (var morceau in ligne.Morceaux)
                        {
                            texte.Append("        morceau « ").Append(Echapper(morceau.Texte))
                                .Append(" » x=").Append(D(morceau.X))
                                .Append(" largeur=").Append(D(morceau.Largeur))
                                .Append(' ').Append(Style(morceau.Style))
                                .AppendLine();
                        }

                        var decalages = Compositeur.Decalages(ligne);
                        texte.Append("        justification ").AppendJoin(' ', decalages.Select(D)).AppendLine();
                    }
                }

                foreach (var pose in controle.Poser(ecran, ecran))
                {
                    texte.Append("    pose « ").Append(Echapper(pose.Texte))
                        .Append(" » ancre=(").Append(D(pose.Ancre.X)).Append(", ").Append(D(pose.Ancre.Y))
                        .Append(") ligne=").Append(pose.Ligne)
                        .Append(" haut=").Append(D(pose.HautDeLigne))
                        .Append(" avance=").Append(D(pose.Avance))
                        .Append(" gauche=").Append(D(pose.Gauche))
                        .Append(" disponible=").Append(D(pose.Disponible))
                        .Append(" rendu=").Append(D(pose.Rendu.WidthIncludingTrailingWhitespace))
                        .AppendLine();
                }

                foreach (var trait in controle.Souligner(ecran))
                {
                    texte.Append("    trait ").Append(D(trait.Debut)).Append(" → ").Append(D(trait.Fin))
                        .Append(" axe=").Append(D(trait.Axe)).Append(" épaisseur=").Append(D(trait.Epaisseur))
                        .Append(" ligne=").Append(trait.Ligne).Append(" couleur=").Append(trait.Couleur)
                        .AppendLine();
                }
            }

            texte.Append("  pixels ").Append(Pixels(planche)).AppendLine();
        }

        File.WriteAllText(sortie, texte.ToString());
        Console.WriteLine(
            $"{vm.Document.Pages.Count} page(s), {blocs} contrôle(s) de texte, {lignes} ligne(s) composée(s), dans {sortie}");
        return 0;
    }

    /// <summary>
    /// Le bloc composé comme <c>ControleDeBloc.Mesurer</c> le compose : la place du modèle,
    /// le style et les fragments résolus par la charte héritée, et ni fragment ni écart de
    /// paragraphe pour un champ résolu.
    /// </summary>
    private static IReadOnlyList<LigneComposee> Composer(
        TexteCompose controle, TextFrame bloc, string contenu, Compositeur.IMetriques regle)
    {
        var charte = JeuAffiche.De(controle);
        var fragments = contenu == bloc.Text ? charte.Fragments(bloc.Spans) : (IReadOnlyList<StyleSpan>)[];
        var ecarts = contenu == bloc.Text ? bloc.Paragraphs : (IReadOnlyList<ParagraphSpan>)[];
        return Compositeur.Composer(
            contenu, fragments, charte.StyleCommun(bloc), ecarts, bloc.Paragraph,
            Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight),
            Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom),
            bloc.Colonnage, bloc.LineHeight, regle);
    }

    /// <summary>L'empreinte SHA-256 des pixels de la page, rendue à deux pixels par point.</summary>
    private static string Pixels(Avalonia.Controls.Panel planche)
    {
        using var image = Banc.Rendre(planche, 2);
        var taille = image.PixelSize;
        var octets = taille.Width * taille.Height * 4;
        var tampon = Marshal.AllocHGlobal(octets);
        try
        {
            image.CopyPixels(new PixelRect(0, 0, taille.Width, taille.Height), tampon, octets, taille.Width * 4);
            var pixels = new byte[octets];
            Marshal.Copy(tampon, pixels, 0, octets);
            return $"{taille.Width}x{taille.Height} {Convert.ToHexString(SHA256.HashData(pixels))}";
        }
        finally
        {
            Marshal.FreeHGlobal(tampon);
        }
    }

    private static string Style(StyleEffectif style) =>
        $"[{Echapper(style.FontFamily)} {D(style.FontSize)} gras={style.Bold} italique={style.Italic} " +
        $"souligné={style.Underline} couleur={style.Foreground} décalage={D(style.BaselineShift)}]";

    /// <summary>Un nombre au bit près, lisible partout pareil.</summary>
    private static string D(double valeur) => valeur.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// Un texte sur une ligne : les blancs autres que l'espace, les caractères de contrôle et
    /// les demi-caractères d'une paire écrits en « \uXXXX », pour qu'une tabulation, une
    /// insécable ou un saut se voient dans l'empreinte.
    /// </summary>
    private static string Echapper(string texte)
    {
        var resultat = new StringBuilder(texte.Length);
        foreach (var c in texte)
        {
            if (c == ' ' || !(char.IsWhiteSpace(c) || char.IsControl(c) || char.IsSurrogate(c) || c == '»' || c == '\\'))
            {
                resultat.Append(c);
            }
            else
            {
                resultat.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        return resultat.ToString();
    }
}
