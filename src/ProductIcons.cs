using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
namespace CodexTaskbar;
internal static class ProductIcons
{
    private static readonly Dictionary<string,Geometry[]> Shapes = new();
    internal static FrameworkElement Create(bool cursor, System.Windows.Media.Brush stroke)
        => Create(cursor?"mouse-pointer-2":"terminal", stroke, 16);
    internal static FrameworkElement Gauge(System.Windows.Media.Brush stroke, int size)
        => Create("circle-gauge", stroke, size);
    private static FrameworkElement Create(string name, System.Windows.Media.Brush stroke, int size)
    {
        if(!Shapes.TryGetValue(name,out var shapes))
        {
            using var stream=System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/assets/icons/{name}.svg"))!.Stream;
            var doc=XDocument.Load(stream);
            double Number(XElement element,string attribute) => double.Parse(element.Attribute(attribute)!.Value,System.Globalization.CultureInfo.InvariantCulture);
            shapes=doc.Root!.Elements().Select(e => e.Name.LocalName switch
            {
                "path" => Geometry.Parse(e.Attribute("d")!.Value),
                "circle" => (Geometry)new EllipseGeometry(new System.Windows.Point(Number(e,"cx"),Number(e,"cy")),Number(e,"r"),Number(e,"r")),
                _ => null
            }).OfType<Geometry>().ToArray();
            foreach(var shape in shapes) shape.Freeze();
            Shapes[name]=shapes;
        }
        var canvas=new Canvas { Width=24,Height=24 };
        foreach(var shape in shapes) canvas.Children.Add(new System.Windows.Shapes.Path { Data=shape,Stroke=stroke,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round });
        return new Viewbox { Width=size,Height=size,Stretch=Stretch.Uniform,Child=canvas,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false };
    }
}
