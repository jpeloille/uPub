using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using MyPublisher.Models;
using MyPublisher.Services;
using Xunit;

namespace MyPublisher.Tests;

/// <summary>
/// <c>Clone()</c> est abstraite, et chaque type recopie ses champs à la main. Aucun
/// compilateur ne vérifie l'exhaustivité : une propriété oubliée disparaît à la
/// duplication d'un bloc ou d'une page, sans erreur, sans avertissement — et le
/// contrôle de non-régression ne duplique rien, donc ne le verrait jamais.
///
/// Plutôt que d'énumérer les propriétés à la main, ce qui reproduirait exactement
/// l'oubli qu'on cherche à empêcher, ces tests les découvrent par réflexion : toute
/// propriété qui s'écrit dans le fichier doit survivre à une copie. Une propriété
/// ajoutée demain est donc couverte le jour où elle est écrite.
/// </summary>
public class CloneTests
{
    [Theory]
    [InlineData(typeof(TextFrame))]
    [InlineData(typeof(ImageFrame))]
    [InlineData(typeof(ShapeFrame))]
    [InlineData(typeof(GroupFrame))]
    [InlineData(typeof(TableFrame))]
    public void Une_copie_conserve_tout_ce_qui_sécrit_dans_le_fichier(Type type)
    {
        var original = (Frame)Activator.CreateInstance(type)!;
        Distinguer(original);

        var copie = original.Clone();

        Assert.Equal(Ecrire(original), Ecrire(copie));
    }

    [Fact]
    public void Une_copie_de_groupe_emporte_ses_blocs()
    {
        var groupe = new GroupFrame { Name = "Groupe", X = 10, Y = 20, Width = 100, Height = 80 };
        groupe.Children.Add(new TextFrame { Name = "Dedans", Text = "Contenu", X = 5, Y = 5 });
        Distinguer(groupe);

        var copie = (GroupFrame)groupe.Clone();

        Assert.Equal(Ecrire(groupe), Ecrire(copie));
        Assert.Single(copie.Children);
        // Une copie de surface partagerait les blocs : retoucher la copie
        // modifierait l'original.
        Assert.NotSame(groupe.Children[0], copie.Children[0]);
    }

    /// <summary>
    /// Donne à chaque propriété sérialisée une valeur qui ne soit pas son défaut :
    /// une copie qui oublierait une propriété restée à sa valeur d'origine ne se
    /// verrait pas.
    /// </summary>
    private static void Distinguer(Frame frame)
    {
        var index = 1;

        foreach (var propriete in frame.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite)
                     .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                     .OrderBy(p => p.Name))
        {
            var valeur = Valeur(propriete.PropertyType, index++);
            if (valeur is not null) propriete.SetValue(frame, valeur);
        }
    }

    private static object? Valeur(Type type, int index) => type switch
    {
        _ when type == typeof(double) => 3.5 + index,
        _ when type == typeof(int) => 2 + index,
        _ when type == typeof(bool) => true,
        _ when type == typeof(string) => $"valeur {index}",
        _ when type.IsEnum => type.GetEnumValues().Cast<object>().Last(),
        // Les collections d'enfants sont éprouvées séparément, et les types qu'on ne
        // sait pas distinguer sont laissés tels quels plutôt que mal remplis.
        _ => null,
    };

    /// <summary>
    /// Le fichier fait foi : ce qui s'y écrit doit survivre à une copie, ce qui n'y
    /// figure pas ne regarde pas ce test.
    /// </summary>
    private static string Ecrire(Frame frame)
    {
        var document = Document.CreateDefault();
        document.Pages[0].Frames.Clear();
        document.Pages[0].Frames.Add(frame);
        return DocumentSerializer.ToJson(document);
    }
}
