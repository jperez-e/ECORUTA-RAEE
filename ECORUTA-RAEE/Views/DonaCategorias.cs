using System.Collections;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Views;

public sealed class DonaCategorias : ContentView
{
    private static readonly string[] Colores =
        ["StatusGreen", "StatusBlue", "StatusAmber", "StatusViolet", "PrimaryGreen", "InkBlue", "InkViolet", "StatusSlate"];
    private readonly VerticalStackLayout _detalle = new() { Spacing = 10 };
    private readonly GraphicsView _grafico = new() { HeightRequest = 136, HorizontalOptions = LayoutOptions.Fill };
    private readonly Anillo _anillo = new();
    private readonly VerticalStackLayout _leyenda = new() { Spacing = 6 };

    public static readonly BindableProperty CategoriasProperty =
        BindableProperty.Create(nameof(Categorias), typeof(IEnumerable), typeof(DonaCategorias),
            propertyChanged: (vista, _, valor) => ((DonaCategorias)vista).Actualizar(valor as IEnumerable));

    public IEnumerable? Categorias { get => (IEnumerable?)GetValue(CategoriasProperty); set => SetValue(CategoriasProperty, value); }

    public DonaCategorias()
    {
        _grafico.Drawable = _anillo;
        SemanticProperties.SetDescription(_grafico, "Distribución porcentual por unidades físicas. Los valores se detallan en la lista.");
        var resumen = new Grid { ColumnDefinitions = { new(new GridLength(2, GridUnitType.Star)), new(new GridLength(3, GridUnitType.Star)) }, ColumnSpacing = 10 };
        resumen.Add(_grafico, 0, 0);
        resumen.Add(_leyenda, 1, 0);
        Content = TarjetasDemo.Tarjeta(resumen, _detalle);
        Actualizar(null);
    }

    private void Actualizar(IEnumerable? origen)
    {
        var categorias = origen?.OfType<CategoriaAmbiental>().ToArray() ?? [];
        var positivas = categorias.Where(c => c.ProporcionEquipos > 0).ToArray();
        _detalle.Clear();
        _leyenda.Clear();
        _grafico.IsVisible = positivas.Length > 0;
        if (positivas.Length == 0)
            _detalle.Add(TarjetasDemo.Texto("Sin unidades físicas para graficar."));

        int visibles = positivas.Length > Colores.Length ? Colores.Length - 1 : positivas.Length;
        var colores = positivas.Take(visibles).Select((c, i) => (c, color: TarjetasDemo.ColorRecurso(Colores[i])))
            .ToDictionary(x => x.c, x => x.color);
        var otros = TarjetasDemo.ColorRecurso("StatusSlate");
        var segmentos = colores.Select(p => (p.Value, p.Key.ProporcionEquipos)).ToList();
        if (positivas.Length > visibles)
        {
            double proporcion = positivas.Skip(visibles).Sum(c => c.ProporcionEquipos);
            segmentos.Add((otros, proporcion));
            
        }
        foreach (var (categoria, color) in colores)
            AgregarLeyenda(categoria.Categoria, categoria.ProporcionEquipos, color);
        if (positivas.Length > visibles)
            AgregarLeyenda("Otras categorías", segmentos[^1].Item2, otros);

        _anillo.Segmentos = segmentos;
        _grafico.Invalidate();

        foreach (var c in categorias)
        {
            var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 9 };
            var muestra = new Border
            {
                WidthRequest = 12, HeightRequest = 12, StrokeThickness = 0, Margin = new Thickness(0, 5, 0, 0),
                VerticalOptions = LayoutOptions.Start, BackgroundColor = colores.GetValueOrDefault(c, otros)
            };
            var etiqueta = new VerticalStackLayout { Spacing = 2, Children =
            {
                TarjetasDemo.Texto(c.Categoria, "TituloVisual"),
                TarjetasDemo.Texto($"{c.Unidades} unidades · {c.ProporcionEquipos:P1}"),
                TarjetasDemo.Texto($"{c.Registros} registros · {c.Peso:0.##} kg registrados")
            } };
            fila.Add(muestra, 0, 0);
            fila.Add(etiqueta, 1, 0);
            _detalle.Add(fila);
        }
    }

    private void AgregarLeyenda(string categoria, double porcentaje, Color color)
    {
        var nombre = TarjetasDemo.Texto($"{categoria} · {porcentaje:P1}");
        nombre.FontSize = 12;
        var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 5 };
        fila.Add(new Border
        {
            WidthRequest = 8, HeightRequest = 8, VerticalOptions = LayoutOptions.Center,
            StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            BackgroundColor = color
        });
        fila.Add(nombre, 1, 0);
        _leyenda.Add(fila);
    }

    private sealed class Anillo : IDrawable
    {
        internal IReadOnlyList<(Color Color, double Proporcion)> Segmentos { get; set; } = [];

        public void Draw(ICanvas canvas, RectF zona)
        {
            float radio = Math.Min(zona.Width, zona.Height) * .36f;
            if (radio <= 0)
                return;

            canvas.StrokeSize = radio * .42f;
            canvas.StrokeLineCap = LineCap.Butt;
            double angulo = -Math.PI / 2;
            foreach (var (color, proporcion) in Segmentos)
            {
                double amplitud = Math.Clamp(proporcion, 0, 1) * Math.Tau;
                int pasos = Math.Max(2, (int)Math.Ceiling(amplitud * 32));
                var arco = new PathF();
                arco.MoveTo(zona.Center.X + radio * (float)Math.Cos(angulo),
                    zona.Center.Y + radio * (float)Math.Sin(angulo));
                for (int i = 1; i <= pasos; i++)
                {
                    double punto = angulo + amplitud * i / pasos;
                    arco.LineTo(zona.Center.X + radio * (float)Math.Cos(punto),
                        zona.Center.Y + radio * (float)Math.Sin(punto));
                }
                canvas.StrokeColor = color;
                canvas.DrawPath(arco);
                angulo += amplitud;
            }
        }
    }
}
